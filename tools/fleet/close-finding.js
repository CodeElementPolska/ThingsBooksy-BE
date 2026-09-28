#!/usr/bin/env node
// close-finding: closes ONE review finding with the provenance of an owner decision or an arbiter verdict —
// the only way a BLOCKER/MAJOR leaves the loop without a code fix. Writes `decision_id`/`closed_by` into the
// reviewer's findings file of that round AND into dedup.json, so dod.js (`unspecified` check) and the next
// round's reviewers see it. Refuses when the decision is not DECIDED by the owner.
// Usage: node tools/fleet/close-finding.js --story NNN-slug --round N --finding <id> (--decision DEC-n | --arbiter <verdict file>)
// Exit 0 · 3 refused · 1 usage
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const { story, round, finding } = args;
if (!story || !round || !finding || !(args.decision || args.arbiter)) { console.error('usage: close-finding --story NNN-slug --round N --finding <id> (--decision DEC-n | --arbiter <file>)'); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
const roundDir = path.join(runDir, 'review', `round-${round}`);

let closure;
if (args.decision) {
  const rows = fs.readFileSync(path.join(runDir, 'discovery', 'decisions.jsonl'), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
  const d = rows.find(r => r.id === args.decision);
  if (!d) { console.error(`close-finding: ${args.decision} not in decisions.jsonl`); process.exit(3); }
  if (d.status !== 'DECIDED' || d.decided_by !== 'owner') { console.error(`close-finding: ${args.decision} is ${d.status}${d.decided_by ? ' by ' + d.decided_by : ''} — only an owner-decided decision closes a finding`); process.exit(3); }
  closure = { decision_id: d.id, closed_by: 'owner', chosen: d.chosen, answer_ref: d.answer_ref, closed_at: new Date().toISOString() };
} else {
  const v = JSON.parse(fs.readFileSync(path.resolve(REPO, args.arbiter), 'utf8'));
  if (v.finding_id !== finding) { console.error(`close-finding: verdict is for ${v.finding_id}, not ${finding}`); process.exit(3); }
  if (v.verdict !== 'OVERTURN') { console.error(`close-finding: arbiter verdict ${v.verdict} does not close a finding (UPHOLD ⇒ writer fixes, ESCALATE ⇒ owner decides)`); process.exit(3); }
  closure = { closed_by: 'arbiter', arbiter_run_id: v.provenance?.run_id, rule_applied: v.rule_applied, closed_at: new Date().toISOString() };
}

let touched = 0;
for (const f of fs.readdirSync(roundDir).filter(f => f.endsWith('.findings.json') || f === 'dedup.json')) {
  const p = path.join(roundDir, f); const j = JSON.parse(fs.readFileSync(p, 'utf8'));
  const hit = (j.findings || []).filter(x => x.id === finding || (x.reporters || []).includes(finding));
  if (!hit.length) continue;
  for (const x of hit) Object.assign(x, closure);
  fs.writeFileSync(p, JSON.stringify(j, null, 2) + '\n'); touched++;
}
if (!touched) { console.error(`close-finding: ${finding} not found in ${path.relative(REPO, roundDir)}`); process.exit(3); }
fs.appendFileSync(path.join(runDir, 'journal.jsonl'), JSON.stringify({ at: closure.closed_at, event: 'DECISION', phase: 'C5', status: 'FINDING_CLOSED', reason: `${finding} ← ${closure.decision_id || 'arbiter OVERTURN'}` }) + '\n');
console.log(`close-finding: ${finding} closed by ${closure.closed_by}${closure.decision_id ? ` (${closure.decision_id}: ${closure.chosen})` : ''} in ${touched} file(s)`);
