#!/usr/bin/env node
// S1 — fleet ACL hook (PreToolUse). Registered once in the PROJECT .claude/settings.json (D-13).
// Reads the hook JSON from stdin; if `agent_type` is listed in fleet-acl.json, enforces:
//   Read/Grep/Glob  → scope must canonicalize inside a read_allow root (Grep/Glob without `path` = cwd)
//   Edit/Write/NotebookEdit → target must be inside a write_allow root
//   Bash/PowerShell → command must start with an allowed `shell` prefix and never match deny_always
// Unknown agent_type (or none = main session) → allow. Unparsable input → deny (fail-closed).
// Exit 2 + stderr = deny. Zero dependencies, so it stays fast (~50 ms).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const CONFIG = path.join(HERE, 'fleet-acl.json');
const LOG = process.env.FLEET_ACL_LOG; // optional: append raw stdin here (debug)

function deny(msg) { process.stderr.write(`ACL DENY: ${msg}\n`); process.exit(2); }

let raw = '';
try { raw = fs.readFileSync(0, 'utf8'); } catch { raw = ''; }
if (LOG) { try { fs.appendFileSync(LOG, raw.replace(/\s+$/, '') + '\n'); } catch { /* ignore */ } }

let input;
try { input = JSON.parse(raw); } catch { deny('hook could not parse tool input'); }

const agentType = input.agent_type;
if (!agentType) process.exit(0);

let cfg;
try { cfg = JSON.parse(fs.readFileSync(CONFIG, 'utf8')); } catch (e) { deny(`fleet-acl.json unreadable: ${e.message}`); }
const acl = cfg.agents?.[agentType];
if (!acl) process.exit(0);

const cwd = input.cwd || process.cwd();

// --- path canonicalisation -------------------------------------------------
// strip \\?\, unify slashes, absolutize, normalize, resolve the longest EXISTING
// prefix through the filesystem (kills 8.3 names, junctions, case differences),
// re-append the non-existing tail, lowercase.
function canon(p) {
  let s = String(p);
  if (s.startsWith('\\\\?\\')) s = s.slice(4);
  s = s.replace(/\//g, '\\');
  if (!path.win32.isAbsolute(s)) s = path.win32.join(cwd, s);
  s = path.win32.normalize(s);
  let head = s; const tail = [];
  while (head && !fs.existsSync(head)) {
    tail.unshift(path.win32.basename(head));
    const up = path.win32.dirname(head);
    if (up === head) break;
    head = up;
  }
  let real = head;
  try { real = fs.realpathSync.native(head); } catch { /* keep head */ }
  return path.win32.join(real, ...tail).toLowerCase();
}

// A root may contain `*` or `**` segments (e.g. backend/src/Modules/*/X.IntegrationTests,
// frontend/src/app/**/*.spec.ts). We match canonical path segments against root segments.
function rootMatches(root, c) {
  const rootSegs = path.win32.normalize(root.replace(/\//g, '\\')).split('\\').filter(Boolean);
  const abs = canon(path.win32.join(REPO, '')); // canonical repo
  const cSegs = c.split('\\').filter(Boolean);
  const repoSegs = abs.split('\\').filter(Boolean);
  if (cSegs.length < repoSegs.length) return false;
  for (let i = 0; i < repoSegs.length; i++) if (cSegs[i] !== repoSegs[i]) return false;
  const rest = cSegs.slice(repoSegs.length);
  return segMatch(rootSegs.map(s => s.toLowerCase()), rest, 0, 0);
}
function segMatch(pat, segs, i, j) {
  if (i === pat.length) return true;                 // root fully matched => inside
  if (pat[i] === '**') {
    for (let k = j; k <= segs.length; k++) if (segMatch(pat, segs, i + 1, k)) return true;
    return false;
  }
  if (j >= segs.length) return false;
  if (!globSeg(pat[i], segs[j])) return false;
  return segMatch(pat, segs, i + 1, j + 1);
}
function globSeg(pat, seg) {
  if (pat === '*') return true;
  if (!pat.includes('*')) return pat === seg;
  const re = new RegExp('^' + pat.split('*').map(s => s.replace(/[.+?^${}()|[\]\\]/g, '\\$&')).join('.*') + '$');
  return re.test(seg);
}
const inside = (roots, p) => roots.some(r => rootMatches(r, canon(p)));

// --- tool dispatch ----------------------------------------------------------
const ti = input.tool_input || {};
const tool = input.tool_name;

switch (tool) {
  case 'Read':
  case 'Grep':
  case 'Glob': {
    let scope;
    if (tool === 'Read') scope = ti.file_path || ti.path;
    else if (tool === 'Grep') scope = ti.path || cwd;
    else {
      const pat = String(ti.pattern || '');
      scope = path.win32.isAbsolute(pat.replace(/\//g, '\\')) ? pat.split(/[*?[{]/)[0] : (ti.path || cwd);
    }
    if (!scope) deny(`${tool} without a resolvable path`);
    if (!inside(acl.read_allow || [], scope)) deny(`${tool} scope "${scope}" is outside read_allow for agent "${agentType}"`);
    break;
  }
  case 'Edit':
  case 'Write':
  case 'NotebookEdit': {
    const target = ti.file_path || ti.notebook_path;
    if (!target) deny(`${tool} without a target path`);
    if (!inside(acl.write_allow || [], target)) deny(`${tool} target "${target}" is outside write_allow for agent "${agentType}"`);
    break;
  }
  case 'Bash':
  case 'PowerShell': {
    const cmd = String(ti.command || '').trim();
    const denyAlways = [...(cfg.deny_always || []), ...(acl.deny_always || [])];
    const hit = denyAlways.find(d => cmd.includes(d));
    if (hit) deny(`command contains forbidden fragment "${hit}"`);
    if (!Array.isArray(acl.shell)) deny(`agent "${agentType}" has no shell allowlist — ${tool} is not permitted`);
    // every chained segment must be allowed (defeats `allowed && evil`)
    // split outside quotes only: `dotnet test --filter "AC=AC-3|AC=AC-4"` is ONE segment (015: the tester could not run
    // the very filter its template prescribed), while `allowed && evil` and `a | b` are still split
    const segments = []; let cur = '', q = null;
    for (let i = 0; i < cmd.length; i++) {
      const ch = cmd[i];
      if (q) { cur += ch; if (ch === q) q = null; continue; }
      if (ch === '"' || ch === "'") { q = ch; cur += ch; continue; }
      const two = cmd.slice(i, i + 2);
      if (two === '&&' || two === '||') { segments.push(cur); cur = ''; i++; continue; }
      if (ch === ';' || ch === '|') { segments.push(cur); cur = ''; continue; }
      cur += ch;
    }
    segments.push(cur);
    const segs = segments.map(s => s.trim()).filter(Boolean);
    for (const seg of segs) {
      if (!acl.shell.some(prefix => seg.startsWith(prefix))) deny(`command segment "${seg}" does not start with an allowed prefix`);
    }
    break;
  }
  default:
    // Agent, Skill, AskUserQuestion, WebFetch… — governed by the agent's tool list, not by paths.
    break;
}
process.exit(0);
