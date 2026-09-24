// Prototype ACL hook for PreToolUse (ALLOWLIST model, fail-closed).
// A Read/Grep/Glob call is allowed only when every path it touches is provably
// inside one of ALLOW_ROOTS. Anything else (unknown scope, unparsable input,
// short names, junctions...) is denied.
const fs = require('fs');
const path = require('path');

const LOG = path.join(__dirname, 'proto-acl.log');
const REPO = 'D:\\Projects\\AI\\ThingsBooksy-BE';
const ALLOW_ROOTS = [
  path.join(REPO, 'docs'),
  path.join(REPO, 'specs'),
  path.join(REPO, 'backend', 'src', 'Modules', 'Resources', 'ThingsBooksy.Modules.Resources.IntegrationTests'),
];

let raw = '';
try { raw = fs.readFileSync(0, 'utf8'); } catch (e) { raw = ''; }
let input = null;
try { input = JSON.parse(raw); } catch (e) { input = null; }
fs.appendFileSync(LOG, JSON.stringify({ at: new Date().toISOString(), input: input || { parseError: true, raw } }) + '\n');

function deny(msg) { process.stderr.write(`ACL DENY: ${msg}\n`); process.exit(2); }
if (!input) deny('hook could not parse tool input');

// Canonicalize: strip \\?\ prefix, make absolute against cwd, normalize, then
// resolve the longest EXISTING prefix through the filesystem (kills 8.3 names,
// junctions, symlinks and case differences) and re-append the rest.
function canon(p, cwd) {
  let s = String(p);
  if (s.startsWith('\\\\?\\')) s = s.slice(4);
  s = s.replace(/\//g, '\\');
  if (!path.win32.isAbsolute(s)) s = path.win32.join(cwd, s);
  s = path.win32.normalize(s);
  let head = s, tail = [];
  while (head && !fs.existsSync(head)) { tail.unshift(path.win32.basename(head)); const up = path.win32.dirname(head); if (up === head) break; head = up; }
  let real = head;
  try { real = fs.realpathSync.native(head); } catch (e) { /* keep head */ }
  return path.win32.join(real, ...tail).toLowerCase();
}
const cwd = input.cwd || process.cwd();
const roots = ALLOW_ROOTS.map(r => canon(r, cwd));
const inside = c => roots.some(r => c === r || c.startsWith(r + '\\'));

const ti = input.tool_input || {};
const scopes = [];
switch (input.tool_name) {
  case 'Read': scopes.push(ti.file_path || ti.path); break;
  case 'Grep': scopes.push(ti.path || cwd); break;           // no path => whole cwd
  case 'Glob': {
    // absolute pattern => its non-glob prefix is the scope; else path or cwd
    const pat = String(ti.pattern || '');
    if (path.win32.isAbsolute(pat.replace(/\//g, '\\'))) scopes.push(pat.split(/[*?[{]/)[0]);
    else scopes.push(ti.path || cwd);
    break;
  }
  default: process.exit(0);                                    // not our concern
}
for (const s of scopes) {
  if (!s) deny(`${input.tool_name} without a resolvable path`);
  const c = canon(s, cwd);
  if (!inside(c)) deny(`${input.tool_name} scope "${s}" is outside this agent's allowed roots`);
}
process.exit(0);
