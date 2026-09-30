#!/usr/bin/env node
// S3a — contract-compose: apply an OpenAPI Overlay 1.0 document (the story's contract-delta, D-10)
// onto generated/swagger.base.json and emit the FULL next contract (contract-next.json) that feeds
// the TS client generator and contract-diff. Also validates the overlay:
//   - overlay: "1.0.0", extends, actions[] with target (JSONPath subset) and update|remove
//   - every action whose target hits an EXISTING path/operation must carry x-evidence (fact id) —
//     "claims about existing behaviour need proof" (audit defect 5)
// Usage: node tools/fleet/contract-compose.js --story NNN-slug [--overlay runs/NNN-slug/contract-delta.overlay.json] [--base generated/swagger.base.json] [--out runs/NNN-slug/contract-next.json]
// Exit 0 ok · 3 validation failure · 1 usage/error
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story;
if (!story) { console.error('usage: contract-compose --story NNN-slug [--overlay …] [--base …] [--out …]'); process.exit(1); }

const basePath = path.resolve(REPO, args.base || 'generated/swagger.base.json');
const overlayPath = path.resolve(REPO, args.overlay || `runs/${story}/contract-delta.overlay.json`);
const outPath = path.resolve(REPO, args.out || `runs/${story}/contract-next.json`);
const fail = (msg) => { console.error(`contract-compose: ${msg}`); process.exit(3); };
for (const [p, what] of [[basePath, 'base swagger (run capability-map --refresh)'], [overlayPath, 'overlay']]) if (!fs.existsSync(p)) fail(`missing ${what}: ${path.relative(REPO, p)}`);

const baseRaw = fs.readFileSync(basePath, 'utf8');
const base = JSON.parse(baseRaw);
const overlay = JSON.parse(fs.readFileSync(overlayPath, 'utf8'));

// --- overlay structural validation -------------------------------------------------
const errors = [];
if (overlay.overlay !== '1.0.0') errors.push(`overlay.overlay must be "1.0.0" (got ${JSON.stringify(overlay.overlay)})`);
if (!overlay.info?.title) errors.push('overlay.info.title required');
if (!Array.isArray(overlay.actions) || overlay.actions.length === 0) errors.push('overlay.actions must be a non-empty array');

// JSONPath subset: $ . key  |  ['key with / and spaces']  |  ["key"]
function parseTarget(t) {
  if (typeof t !== 'string' || !t.startsWith('$')) return null;
  const segs = []; let i = 1;
  while (i < t.length) {
    if (t[i] === '.') { i++; let s = ''; while (i < t.length && t[i] !== '.' && t[i] !== '[') s += t[i++]; if (s === '*' || s === '') return null; segs.push(s); }
    else if (t[i] === '[') {
      const q = t[i + 1]; if (q !== "'" && q !== '"') return null;
      const end = t.indexOf(q + ']', i + 2); if (end < 0) return null;
      segs.push(t.slice(i + 2, end)); i = end + 2;
    } else return null;
  }
  return segs;
}
function getAt(obj, segs) { let cur = obj; for (const s of segs) { if (cur == null || typeof cur !== 'object' || !(s in cur)) return undefined; cur = cur[s]; } return cur; }
function ensureAt(obj, segs) { let cur = obj; for (const s of segs) { if (cur[s] == null || typeof cur[s] !== 'object') cur[s] = {}; cur = cur[s]; } return cur; }
function deepMerge(dst, src) {
  if (Array.isArray(src)) return src.slice();
  if (src && typeof src === 'object') { const out = (dst && typeof dst === 'object' && !Array.isArray(dst)) ? dst : {}; for (const [k, v] of Object.entries(src)) out[k] = deepMerge(out[k], v); return out; }
  return src;
}

const next = JSON.parse(baseRaw);
const applied = [];
(overlay.actions || []).forEach((a, idx) => {
  const segs = parseTarget(a.target);
  if (!segs) { errors.push(`action[${idx}] target unsupported: ${JSON.stringify(a.target)} (use $.paths['/route'].post, $.components.schemas.Name …)`); return; }
  const existed = getAt(base, segs) !== undefined;
  // "existing behaviour" = the route (and, if addressed, the operation) already exists in the base,
  // even when the action adds a brand-new leaf under it (a new response code, a new parameter…).
  const touchesExistingOp = segs[0] === 'paths' && segs.length >= 2 && getAt(base, segs.slice(0, Math.min(segs.length, 3))) !== undefined;
  if (touchesExistingOp && !a['x-evidence']) errors.push(`action[${idx}] modifies EXISTING ${a.target} but has no x-evidence (fact id proving current behaviour)`);
  if (a.remove === true) {
    if (!existed) { errors.push(`action[${idx}] removes non-existent ${a.target}`); return; }
    const parent = getAt(next, segs.slice(0, -1)); delete parent[segs[segs.length - 1]];
    applied.push({ idx, target: a.target, op: 'remove' });
  } else if (a.update !== undefined) {
    const parent = ensureAt(next, segs.slice(0, -1)); const key = segs[segs.length - 1];
    parent[key] = deepMerge(parent[key], a.update);
    applied.push({ idx, target: a.target, op: existed ? 'update' : 'add' });
  } else errors.push(`action[${idx}] has neither update nor remove`);
});
if (errors.length) { errors.forEach(e => console.error(' - ' + e)); fail(`${errors.length} overlay error(s)`); }

// --- provenance + write ---------------------------------------------------------------
const sha = s => crypto.createHash('sha256').update(s).digest('hex');
next['x-fleet'] = { story, base_sha256: sha(baseRaw), overlay_sha256: sha(fs.readFileSync(overlayPath, 'utf8')), actions: applied };
fs.mkdirSync(path.dirname(outPath), { recursive: true });
fs.writeFileSync(outPath, JSON.stringify(next, null, 2) + '\n');
const adds = applied.filter(a => a.op === 'add').length, upd = applied.filter(a => a.op === 'update').length, rem = applied.filter(a => a.op === 'remove').length;
console.log(`contract-compose: ${adds} added, ${upd} updated, ${rem} removed → ${path.relative(REPO, outPath)}`);
