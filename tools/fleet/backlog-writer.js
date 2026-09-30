#!/usr/bin/env node
// backlog-writer — the deterministic half of the GitHub hand-off (workflow §1.1, D-6, D-11). Validates the
// story front matter of runs/<story>/story.md against schemas/story.schema.json and renders the issue body
// (runs/<story>/issue-body.md) that the scrum persona shows to the owner (dry run) and, after G1, posts through
// the GitHub MCP tool. This script never talks to GitHub itself (no gh here; MCP calls stay in the persona).
// Usage: node tools/fleet/backlog-writer.js --story NNN-slug [--issue <n>  # after posting: writes source_issue into story.md]
// Exit 0 · 3 invalid story · 1 usage
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: backlog-writer --story NNN-slug [--issue n]'); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
const storyFile = path.join(runDir, 'story.md');
if (!fs.existsSync(storyFile)) { console.error(`backlog-writer: no ${path.relative(REPO, storyFile)}`); process.exit(3); }

// --- front matter (YAML subset: scalars, lists of scalars, lists of maps, nested maps one level) --------------
const text = fs.readFileSync(storyFile, 'utf8').replace(/\r\n/g, '\n');
const fm = text.match(/^---\n([\s\S]*?)\n---\n?([\s\S]*)$/);
if (!fm) { console.error('backlog-writer: story.md has no front matter'); process.exit(3); }
const unq = s => { s = s.trim(); if ((s.startsWith('"') && s.endsWith('"')) || (s.startsWith("'") && s.endsWith("'"))) s = s.slice(1, -1); if (s === 'true') return true; if (s === 'false') return false; return s; };
function parseYaml(lines) {
  // minimal recursive-descent YAML for the story shape; good enough for what scrum writes, refuses the rest
  const out = {}; let i = 0;
  const indentOf = l => l.match(/^ */)[0].length;
  function block(ind) {
    const obj = {};
    while (i < lines.length) {
      const l = lines[i]; if (!l.trim() || l.trim().startsWith('#')) { i++; continue; }
      const li = indentOf(l); if (li < ind) break; if (li > ind) throw new Error(`unexpected indent at line: ${l}`);
      const m = l.match(/^ *([\w-]+):\s*(.*)$/); if (!m) throw new Error(`cannot parse: ${l}`);
      i++;
      if (m[2] !== '') { obj[m[1]] = unq(m[2]); continue; }
      // nested: list or map
      const next = lines[i]; if (next === undefined) { obj[m[1]] = null; continue; }
      const ni = indentOf(next);
      if (next.trim().startsWith('- ')) obj[m[1]] = list(ni); else obj[m[1]] = block(ni);
    }
    return obj;
  }
  function list(ind) {
    const arr = [];
    while (i < lines.length) {
      const l = lines[i]; if (!l.trim()) { i++; continue; }
      const li = indentOf(l); if (li < ind || !l.trim().startsWith('- ')) break;
      const body = l.trim().slice(2); i++;
      const km = body.match(/^([\w-]+):\s*(.*)$/);
      if (km) { // list of maps: first key on the dash line, rest indented by 2
        const item = {}; if (km[2] !== '') item[km[1]] = unq(km[2]); else { const ni = indentOf(lines[i] || ''); item[km[1]] = (lines[i] || '').trim().startsWith('- ') ? list(ni) : block(ni); }
        Object.assign(item, block(ind + 2)); arr.push(item);
      } else arr.push(unq(body));
    }
    return arr;
  }
  Object.assign(out, block(0));
  return out;
}
let front; try { front = parseYaml(fm[1].split('\n')); } catch (e) { console.error(`backlog-writer: front matter: ${e.message}`); process.exit(3); }
if (args.issue) front.source_issue = `#${String(args.issue).replace(/^#/, '')}`;

// --- validate --------------------------------------------------------------------------------------------------
const tmp = path.join(runDir, '.story-front.json'); fs.writeFileSync(tmp, JSON.stringify(front));
const v = spawnSync(process.execPath, [path.join(HERE, 'validate.js'), '--schema', 'story', '--file', tmp], { cwd: REPO, encoding: 'utf8' });
fs.unlinkSync(tmp);
if (v.status !== 0) { console.error(`backlog-writer: story.md front matter is invalid:\n${(v.stderr || v.stdout).trim()}`); process.exit(3); }

// --- issue body -------------------------------------------------------------------------------------------------
const ac = front.acceptance_criteria || [];
const body = [
  `**Epic:** ${front.epic}`, '', `## Why`, '', front.why, '',
  `## Acceptance criteria`, '',
  ...ac.map(a => `- **${a.id}**${a.role ? ` _(${a.role})_` : ''} Given ${a.given}, when ${a.when}, then ${a.then}${a.exact_message ? ` — message: \`${a.exact_message}\`` : ''}`),
  '', `## Out of scope`, '', ...(front.out_of_scope || []).map(s => `- ${s}`),
  '', `## Rejected alternatives`, '', ...(front.rejected_alternatives || []).map(r => `- **${r.option}** — ${r.reason}`),
  '', `## Blast radius`, '', `Modules: ${(front.blast_radius?.modules || []).join(', ') || '–'}; contract: ${front.blast_radius?.touches_contract ?? '?'}, authz: ${front.blast_radius?.touches_authz ?? '?'}, schema: ${front.blast_radius?.touches_schema ?? '?'}, UI: ${front.blast_radius?.touches_ui ?? '?'}`,
  ...(front.depends_on?.length ? ['', `Depends on: ${front.depends_on.join(', ')}`] : []),
  '', `<sub>Shaped by the fleet (session A) against capability map \`${String(front.capability_map_version).slice(0, 12)}…\`; story id \`${story}\`; critiques: ${(front.critique_refs || []).join(', ') || '–'}. Tasks live in \`specs/${story}/tasks.md\` (D-6).</sub>`,
].join('\n');
fs.writeFileSync(path.join(runDir, 'issue-body.md'), body + '\n');

if (args.issue) { // write source_issue back into the front matter (append a line if absent)
  const updated = /^source_issue:/m.test(fm[1]) ? text.replace(/^source_issue:.*$/m, `source_issue: "${front.source_issue}"`) : text.replace(/^---\n([\s\S]*?)\n---/, (m, f) => `---\n${f}\nsource_issue: "${front.source_issue}"\n---`);
  fs.writeFileSync(storyFile, updated);
}
console.log(`backlog-writer: story ${story} valid (${ac.length} AC, modules: ${(front.blast_radius?.modules || []).join(', ')}) → ${path.relative(REPO, path.join(runDir, 'issue-body.md'))}${args.issue ? `; source_issue ${front.source_issue} written` : ' (dry run — post via MCP after G1, then re-run with --issue <n>)'}`);
