#!/usr/bin/env node
// S9 — dedup-findings: merges the reviewers' findings of one C5 round into ONE list the writer (or the
// owner) acts on. No LLM. Rules (D-9, schemas/README.md):
//   - every <reviewer>.findings.json must validate against `findings`; an invalid or STALE file (an input
//     hash in its provenance no longer matches) is rejected as a whole and listed in `rejected`
//   - severity above OPINION without `rule_ref` ⇒ demoted to OPINION (listed in `demoted`)
//   - round ≥ 2 without `previous_findings_status` ⇒ file rejected
//   - duplicates (same file, line within ±3, and same rule_ref or same type) are merged: highest severity
//     wins, reporters are unioned
//   - verdict: CLEAN (no BLOCKER/MAJOR) · FIX_REQUIRED · ESCALATE (round ≥ 2 and blockers did not
//     decrease, or round > 3 — "blockers must decrease")
//   - every BLOCKER/MAJOR gets a `route`: owner (UNSPECIFIED_BEHAVIOR), tester (WEAK_ASSERTION or a test
//     file — blind-pass hash ⇒ REBASELINE), writer (the rest); DECISIONS_REQUIRED when only owner/tester
//     items remain, so the writer is not dispatched to bounce them as BLOCKED_ON_DECISION
// Usage: node tools/fleet/dedup-findings.js --story NNN-slug --round N
// Exit 0 CLEAN · 3 FIX_REQUIRED · 5 DECISIONS_REQUIRED · 4 ESCALATE · 1 error
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; const round = +args.round;
if (!story || !round) { console.error('usage: dedup-findings --story NNN-slug --round N'); process.exit(1); }
const roundDir = path.join(REPO, 'runs', story, 'review', `round-${round}`);
if (!fs.existsSync(roundDir)) { console.error(`dedup-findings: no ${path.relative(REPO, roundDir)}`); process.exit(1); }
const SEV = { BLOCKER: 0, MAJOR: 1, MINOR: 2, OPINION: 3 };
const MAX_ROUNDS = 3;

const validate = file => spawnSync(process.execPath, [path.join(HERE, 'validate.js'), '--schema', 'findings', '--file', file], { cwd: REPO, encoding: 'utf8' });
const sha = rel => { try { return crypto.createHash('sha256').update(fs.readFileSync(path.join(REPO, rel))).digest('hex'); } catch { return null; } };

const rejected = [], demoted = [], reviewers = [], all = [];
for (const f of fs.readdirSync(roundDir).filter(f => f.endsWith('.findings.json')).sort()) {
  const file = path.join(roundDir, f); let j;
  try { j = JSON.parse(fs.readFileSync(file, 'utf8')); } catch (e) { rejected.push({ file: f, reason: `not JSON: ${e.message}` }); continue; }
  // D-9: no rule_ref ⇒ it is an opinion. Demote before schema validation so one missing ref does not sink the file.
  for (const x of j.findings || []) if (x.severity && x.severity !== 'OPINION' && !x.rule_ref) { demoted.push({ id: x.id, from: x.severity }); x.demoted_from = x.severity; x.severity = 'OPINION'; }
  const v = validate(file); if (v.status !== 0) { rejected.push({ file: f, reason: `schema: ${(v.stderr || v.stdout).trim().split('\n').slice(0, 5).join(' | ')}` }); continue; }
  if (round >= 2 && !Array.isArray(j.previous_findings_status)) { rejected.push({ file: f, reason: 'round ≥ 2 without previous_findings_status' }); continue; }
  const stale = (j.provenance?.inputs || []).filter(i => sha(i.path) !== i.sha256).map(i => i.path);
  if (stale.length) { rejected.push({ file: f, reason: `stale provenance: ${stale.join(', ')}` }); continue; }
  reviewers.push(j.reviewer || j.provenance?.author_agent || f.replace('.findings.json', ''));
  for (const x of j.findings || []) all.push({ ...x, reporters: [x.id] });
}

// --- merge duplicates ------------------------------------------------------------------------------------
const norm = s => String(s || '').replace(/\\/g, '/').toLowerCase();
const merged = [];
for (const x of all.sort((a, b) => SEV[a.severity] - SEV[b.severity])) {
  const dup = merged.find(m => norm(m.file) === norm(x.file) && Math.abs(m.line - x.line) <= 3 && ((m.rule_ref && m.rule_ref === x.rule_ref) || m.type === x.type));
  if (dup) { dup.reporters.push(x.id); if (x.evidence && !dup.evidence.includes(x.evidence)) dup.evidence += `\n---\n${x.evidence}`; if (x.rule_candidate && !dup.rule_candidate) dup.rule_candidate = x.rule_candidate; }
  else merged.push(x);
}
merged.sort((a, b) => SEV[a.severity] - SEV[b.severity] || norm(a.file).localeCompare(norm(b.file)) || a.line - b.line);
// Who acts on a finding (workflow §1.3 C5 / D-4b): behaviour without an AC is the OWNER's call, a weak or
// wrong acceptance test is the TESTER's (blind-pass hash ⇒ REBASELINE), everything else goes to the writer.
const TEST_PATH = /IntegrationTests|Tests\.Unit|\.spec\.ts$/;
for (const m of merged) m.route = m.type === 'UNSPECIFIED_BEHAVIOR' ? 'owner' : (m.type === 'WEAK_ASSERTION' || TEST_PATH.test(m.file)) ? 'tester' : 'writer';
const count = s => merged.filter(m => m.severity === s).length;
const actionable = merged.filter(m => m.severity === 'BLOCKER' || m.severity === 'MAJOR');
const summary = { blockers: count('BLOCKER'), majors: count('MAJOR'), minors: count('MINOR'), opinions: count('OPINION'), merged_duplicates: all.length - merged.length, routes: { writer: actionable.filter(m => m.route === 'writer').length, tester: actionable.filter(m => m.route === 'tester').length, owner: actionable.filter(m => m.route === 'owner').length } };

// --- verdict ---------------------------------------------------------------------------------------------
// CLEAN: nothing above MINOR · FIX_REQUIRED: the writer has work · DECISIONS_REQUIRED: only owner/tester items left
const prev = round >= 2 ? (() => { try { return JSON.parse(fs.readFileSync(path.join(REPO, 'runs', story, 'review', `round-${round - 1}`, 'dedup.json'), 'utf8')).summary; } catch { return null; } })() : null;
let verdict = actionable.length === 0 ? 'CLEAN' : summary.routes.writer > 0 ? 'FIX_REQUIRED' : 'DECISIONS_REQUIRED';
let escalation_reason = null;
if (verdict === 'FIX_REQUIRED' && round >= 2 && prev && summary.blockers > 0 && summary.blockers >= prev.blockers) { verdict = 'ESCALATE'; escalation_reason = `blockers did not decrease (round ${round - 1}: ${prev.blockers}, round ${round}: ${summary.blockers})`; }
if (verdict === 'FIX_REQUIRED' && round > MAX_ROUNDS) { verdict = 'ESCALATE'; escalation_reason = `round limit ${MAX_ROUNDS} exceeded`; }
if (!reviewers.length) { verdict = 'ESCALATE'; escalation_reason = 'no valid findings file in this round'; }

const report = { story, round, reviewers, verdict, escalation_reason, summary, previous_summary: prev, findings: merged, demoted, rejected, computed_at: new Date().toISOString() };
fs.writeFileSync(path.join(roundDir, 'dedup.json'), JSON.stringify(report, null, 2) + '\n');
for (const m of merged) console.log(`${m.severity.padEnd(8)} ${m.type.padEnd(20)} → ${m.route.padEnd(6)} ${m.file}:${m.line}  ${m.rule_ref || '-'}  [${m.reporters.join(', ')}]`);
for (const r of rejected) console.error(`rejected ${r.file}: ${r.reason}`);
console.log(`dedup-findings: round ${round} ${verdict}${escalation_reason ? ` (${escalation_reason})` : ''} — ${summary.blockers} BLOCKER, ${summary.majors} MAJOR, ${summary.minors} MINOR, ${summary.opinions} OPINION (routes: writer ${summary.routes.writer}, tester ${summary.routes.tester}, owner ${summary.routes.owner}); ${summary.merged_duplicates} merged, ${demoted.length} demoted, ${rejected.length} files rejected → ${path.relative(REPO, path.join(roundDir, 'dedup.json'))}`);
process.exit(verdict === 'CLEAN' ? 0 : verdict === 'FIX_REQUIRED' ? 3 : verdict === 'DECISIONS_REQUIRED' ? 5 : 4);
