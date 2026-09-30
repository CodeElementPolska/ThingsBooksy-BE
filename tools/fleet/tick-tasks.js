#!/usr/bin/env node
// tick-tasks: the "script ticks these in tasks.md" promised by schemas/result.schema.json. Collects
// `tasks_completed` from every runs/<story>/impl/*/result.json with status DONE (a FAILED run's tasks are
// not ticked), adds the conductor-owned tasks passed with --also (build/gate/migration/format steps that
// scripts or the owner performed), and flips `- [ ] Tnnn` to `- [x] Tnnn` in specs/<story>/tasks.md.
// Refuses when the gate is not GREEN (tasks are only "done" on a green tree). Prints what stays unticked.
// Usage: node tools/fleet/tick-tasks.js --story NNN-slug [--also T001,T008] [--dry-run]
// Exit 0 · 3 = gate not GREEN · 1 = usage
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: tick-tasks --story NNN-slug [--also T001,T008] [--dry-run]'); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
const tasksFile = path.join(REPO, 'specs', story, 'tasks.md');
if (!fs.existsSync(tasksFile)) { console.error(`tick-tasks: no ${path.relative(REPO, tasksFile)}`); process.exit(1); }
const gate = JSON.parse(fs.readFileSync(path.join(runDir, 'gate.json'), 'utf8'));
if (gate.status !== 'GREEN') { console.error(`tick-tasks: gate is ${gate.status} — tasks are ticked only on a green tree`); process.exit(3); }

const done = new Map(); // Tnnn → who
const implDir = path.join(runDir, 'impl');
for (const d of fs.existsSync(implDir) ? fs.readdirSync(implDir) : []) {
  const p = path.join(implDir, d, 'result.json'); if (!fs.existsSync(p)) continue;
  const r = JSON.parse(fs.readFileSync(p, 'utf8'));
  if (r.status !== 'DONE') continue;
  for (const t of r.tasks_completed || []) done.set(t, `${r.provenance?.author_agent || d} (${d})`);
}
for (const t of String(args.also || '').split(',').map(s => s.trim()).filter(Boolean)) if (!done.has(t)) done.set(t, 'conductor/owner (--also)');

let text = fs.readFileSync(tasksFile, 'utf8');
const ticked = [], already = [], unticked = [];
text = text.replace(/^(\s*)- \[( |x|X)\] (T\d+)\b/gm, (m, ind, mark, id) => {
  if (mark !== ' ') { already.push(id); return m; }
  if (done.has(id)) { ticked.push(id); return `${ind}- [x] ${id}`; }
  unticked.push(id); return m;
});
if (!args['dry-run']) fs.writeFileSync(tasksFile, text);
for (const id of ticked) console.log(`[x] ${id}  ← ${done.get(id)}`);
console.log(`tick-tasks: ${ticked.length} ticked now, ${already.length} already, ${unticked.length} still open${unticked.length ? `: ${unticked.join(', ')}` : ''}${args['dry-run'] ? ' (dry run)' : ''}`);
