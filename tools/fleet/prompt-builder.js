#!/usr/bin/env node
// S8 — prompt-builder: assembles an agent prompt from a TEMPLATE + explicit input files, never from an
// LLM's summary. This is what makes "the orchestrator passes paths, not context" enforceable:
//   - template:  tools/fleet/templates/<agent_type>.md  (frontmatter: inputs, output_schema, conventions)
//   - inputs:    resolved against runs/<story>/ and generated/; each must be inside the agent's
//                read_allow in fleet-acl.json (an agent may never be handed a file it could not read)
//   - output:    prompt text with the file LIST (paths + sha256), the output JSON schema inlined,
//                and a provenance block the agent must copy into its artifact
// Writes runs/<story>/prompts/<agent_type>[.<instance>].md and .json (the latter is what a Workflow
// script passes to agent()). Nothing else than paths, hashes and schema goes in — no summaries.
// Usage: node tools/fleet/prompt-builder.js --story NNN-slug --agent <agent_type> [--instance <name>] [--var key=value …] [--phase C2]
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { sha256File } from './hash.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const argv = process.argv.slice(2);
const args = {}; const vars = {};
for (let i = 0; i < argv.length; i++) {
  if (argv[i] === '--var') { const [k, ...v] = argv[++i].split('='); vars[k] = v.join('='); }
  else if (argv[i].startsWith('--')) args[argv[i].slice(2)] = argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[++i] : true;
}
const { story, agent } = args;
if (!story || !agent) { console.error('usage: prompt-builder --story NNN-slug --agent <agent_type> [--instance x] [--var k=v] [--phase C2]'); process.exit(1); }
vars.story = story; vars.instance = args.instance || '';
// default artifact id for single-fact workers: F-<instance> when the instance is numeric (F-3b is not a valid id → F-3)
if (!vars.fact_id && vars.instance) { const n = String(vars.instance).match(/^\d+/); if (n) vars.fact_id = `F-${n[0]}`; }

// --template lets one agent type run different phases (be-writer-skeleton vs be-writer-behaviour)
const tplPath = path.join(HERE, 'templates', `${args.template || agent}.md`);
if (!fs.existsSync(tplPath)) { console.error(`prompt-builder: no template ${path.relative(REPO, tplPath)}`); process.exit(1); }
// core.autocrlf=true checks templates out with CRLF on Windows — normalise before parsing the front matter
const raw = fs.readFileSync(tplPath, 'utf8').replace(/\r\n/g, '\n');
const fm = raw.match(/^---\n([\s\S]*?)\n---\n([\s\S]*)$/);
if (!fm) { console.error('prompt-builder: template needs YAML-ish front matter'); process.exit(1); }

// minimal front matter parser: key: value | key:\n  - item
const meta = {}; let cur = null;
for (const line of fm[1].split('\n')) {
  const kv = line.match(/^(\w[\w-]*):\s*(.*)$/); const li = line.match(/^\s+-\s+(.*)$/);
  if (kv) { cur = kv[1]; meta[cur] = kv[2] === '' ? [] : kv[2]; }
  else if (li && cur && Array.isArray(meta[cur])) meta[cur].push(li[1]);
}
const body = fm[2];
const subst = s => String(s).replace(/\{(\w+)\}/g, (_, k) => (k in vars ? vars[k] : `{${k}}`));

// --- resolve inputs & enforce ACL -----------------------------------------------------------------
const acl = JSON.parse(fs.readFileSync(path.join(HERE, 'fleet-acl.json'), 'utf8')).agents?.[agent];
const globToRe = g => new RegExp('^' + g.replace(/[.+^${}()|[\]\\]/g, '\\$&').replace(/\*\*\//g, '(?:.*/)?').replace(/\*\*/g, '.*').replace(/\*/g, '[^/]*') + '(/|$)');
const insideAcl = rel => !acl || (acl.read_allow || []).some(r => globToRe(r.replace(/\\/g, '/')).test(rel));
function expand(pattern) {
  const p = subst(pattern).replace(/\\/g, '/');
  if (!p.includes('*')) return fs.existsSync(path.join(REPO, p)) ? [p] : [];
  const dir = p.split('/').slice(0, p.split('/').findIndex(s => s.includes('*'))).join('/');
  const re = globToRe(p); const out = [];
  (function walk(d) { if (!fs.existsSync(d)) return; for (const e of fs.readdirSync(d, { withFileTypes: true })) { const q = path.join(d, e.name); const rel = path.relative(REPO, q).replace(/\\/g, '/'); if (e.isDirectory()) { if (!['node_modules', 'bin', 'obj'].includes(e.name)) walk(q); } else if (re.test(rel)) out.push(rel); } })(path.join(REPO, dir));
  return out.sort();
}
const sha = rel => sha256File(path.join(REPO, rel)); // CRLF-normalised (hash.js) — stable across autocrlf checkouts
const inputs = []; const missing = []; const denied = [];
for (const spec of meta.inputs || []) {
  const optional = spec.endsWith('?'); const pat = optional ? spec.slice(0, -1) : spec;
  const files = expand(pat);
  if (!files.length && !optional) missing.push(subst(pat));
  for (const f of files) { if (!insideAcl(f)) denied.push(f); else inputs.push({ path: f, sha256: sha(f) }); }
}
if (missing.length) { console.error(`prompt-builder: missing required inputs:\n - ${missing.join('\n - ')}`); process.exit(3); }
// the agent is handed a PATH to its prompt — an ACL that cannot read runs/<story>/prompts/ blocks it on turn 1
const promptRel = `runs/${story}/prompts/${args.template || agent}${vars.instance ? '.' + vars.instance : ''}.md`;
if (!insideAcl(promptRel)) { console.error(`prompt-builder: ${agent}'s read_allow does not cover its own prompt ${promptRel} — add "runs/*/prompts" to fleet-acl.json`); process.exit(3); }
if (denied.length) { console.error(`prompt-builder: inputs outside ${agent}'s read_allow (fix fleet-acl.json or the template):\n - ${denied.join('\n - ')}`); process.exit(3); }

// --- schema & conventions -------------------------------------------------------------------------------
const schemaName = meta.output_schema;
const SCHEMAS = path.join(REPO, 'docs', 'agent-fleet-v4', 'schemas');
const loadSchema = name => JSON.parse(fs.readFileSync(path.join(SCHEMAS, `${name}.schema.json`), 'utf8'));
const schema = schemaName ? loadSchema(schemaName) : null;
// `$ref: "fleet-v4/<name>"` inside the output schema (assumption, decision, provenance…) — the agent must see
// those shapes too, or it invents its own (015: an `assumptions[]` entry without id/bucket/score failed validation)
const referenced = {}; const collectRefs = node => { if (!node || typeof node !== 'object') return; for (const [k, v] of Object.entries(node)) { if (k === '$ref' && typeof v === 'string' && v.startsWith('fleet-v4/')) { const n = v.slice(9); if (!referenced[n] && n !== schemaName && fs.existsSync(path.join(SCHEMAS, `${n}.schema.json`))) { referenced[n] = loadSchema(n); collectRefs(referenced[n]); } } else collectRefs(v); } };
collectRefs(schema);
const conventions = (meta.conventions || []).map(c => subst(c)).filter(c => fs.existsSync(path.join(REPO, c)));

// --- assemble --------------------------------------------------------------------------------------------
const runId = args['run-id'] || `${story}-${agent}${vars.instance ? '-' + vars.instance : ''}-${Date.now().toString(36)}`;
const provenance = { author_agent: agent, run_id: runId, story, phase: args.phase || meta.phase || null, inputs, schema_version: '1' };
const prompt = [
  subst(body).trim(),
  '',
  '## Input files handed to you (paths + hashes)',
  'These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.',
  ...inputs.map(i => `- \`${i.path}\`  (sha256 ${i.sha256.slice(0, 12)}…)`),
  ...(conventions.length ? ['', '## Conventions that apply to your output', ...conventions.map(c => `- \`${c}\``)] : []),
  ...(schema ? ['', '## Your output MUST validate against this JSON Schema', '```json', JSON.stringify(schema, null, 2), '```'] : []),
  ...Object.entries(referenced).flatMap(([n, s]) => ['', `### Referenced schema \`fleet-v4/${n}\` (items of the fields that \`$ref\` it must have this shape)`, '```json', JSON.stringify(s, null, 2), '```']),
  '',
  '## Provenance block — copy VERBATIM into the `provenance` field of your output',
  '```json', JSON.stringify(provenance, null, 2), '```',
  '',
].join('\n');

const outDir = path.join(REPO, 'runs', story, 'prompts');
fs.mkdirSync(outDir, { recursive: true });
const base = path.join(outDir, `${args.template || agent}${vars.instance ? '.' + vars.instance : ''}`);
// a prompt is provenance: never silently overwrite one that was already handed to an agent (015: round-2 prompts
// replaced round-1's). Re-run with --instance <name> or --force.
if (fs.existsSync(base + '.md') && !args.force) { console.error(`prompt-builder: ${path.relative(REPO, base)}.md exists — use --instance <name> (e.g. r2) to keep both, or --force to overwrite`); process.exit(3); }
fs.writeFileSync(base + '.md', prompt);
fs.writeFileSync(base + '.json', JSON.stringify({ agent_type: agent, run_id: runId, prompt, schema, provenance, tools_hint: meta.tools || null, max_turns: meta.max_turns ? +meta.max_turns : null }, null, 2) + '\n');
console.log(`prompt-builder: ${agent}${vars.instance ? '/' + vars.instance : ''} — ${inputs.length} inputs, schema ${schemaName || 'none'} → ${path.relative(REPO, base)}.{md,json}`);
