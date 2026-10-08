#!/usr/bin/env node
// S7a — status: COMPUTES runs/<story>/state.json from artifacts + journal (schemas/state.schema.json).
// Never written by an agent; the conductor and the owner read it to know "where are we".
// Usage: node tools/fleet/status.js --story NNN-slug [--print]
// Exit 0 always (state is a fact, not a verdict), 1 on usage/branch mismatch.
import fs from 'node:fs';
import path from 'node:path';
import { sha256File } from './hash.js';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: status --story NNN-slug [--print]'); process.exit(1); }
if (!/^\d{3}-[a-z0-9-]+$/.test(story)) { console.error(`status: "${story}" is not NNN-slug`); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
const branch = (spawnSync('git', ['rev-parse', '--abbrev-ref', 'HEAD'], { cwd: REPO, encoding: 'utf8' }).stdout || '').trim();
if (branch !== story && !args['skip-branch-check']) { console.error(`status: current branch "${branch}" ≠ story "${story}" — SpecKit scripts would silently target another spec dir`); process.exit(1); }
// SpecKit trap: .specify/feature.json takes precedence over the branch name for every /speckit-* script. The fleet
// does not use that file — story id = branch = specs/NNN-slug — and since 2026-10-01 it is removed from the repo and
// git-ignored (a committed pointer went stale after every merge: 015 → 010, 016 → 015). A stray local copy (only
// /speckit-specify writes one) must be deleted, never "fixed": the branch is the single source of truth.
const featureJson = path.join(REPO, '.specify', 'feature.json');
if (fs.existsSync(featureJson)) {
  let fd = null; try { fd = JSON.parse(fs.readFileSync(featureJson, 'utf8')).feature_directory; } catch { /* unreadable → treat as mismatch */ }
  const same = String(fd || '').replace(/\\/g, '/').replace(/\/$/, '') === `specs/${story}`;
  if (!same) { console.error(`status: .specify/feature.json exists and points to "${fd}" (story "${story}") — delete the file; SpecKit must resolve the spec dir from the branch name (tools/fleet/README.md, journal 2026-10-01)`); process.exit(1); }
  console.error(`status: WARNING .specify/feature.json exists (ignored by git) — delete it; the branch name is the only source of truth for SpecKit`);
}

const sha = f => sha256File(f); // CRLF-normalised like every other fleet script (hash.js)
const exists = rel => fs.existsSync(path.join(runDir, rel));
const readJson = rel => { try { return JSON.parse(fs.readFileSync(path.join(runDir, rel), 'utf8')); } catch { return null; } };
const readJsonl = rel => { try { return fs.readFileSync(path.join(runDir, rel), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)); } catch { return []; } };

// --- artifacts with hashes -------------------------------------------------------------------
const artifacts = {};
(function walk(dir) {
  if (!fs.existsSync(dir)) return;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!['trx-red-first', 'trx-gate', 'coverage'].includes(e.name)) walk(p); }
    else if (e.name !== 'state.json') artifacts[path.relative(runDir, p).replace(/\\/g, '/')] = sha(p);
  }
})(runDir);
for (const g of ['swagger.base.json', 'capability-map.json', 'core-surface.json']) { const p = path.join(REPO, 'generated', g); if (fs.existsSync(p)) artifacts[`generated/${g}`] = sha(p); }
for (const s of ['spec.md', 'plan.md', 'tasks.md']) { const p = path.join(REPO, 'specs', story, s); if (fs.existsSync(p)) artifacts[`specs/${s}`] = sha(p); }

// --- journal-derived facts ------------------------------------------------------------------------
const journal = readJsonl('journal.jsonl');
const last = ev => [...journal].reverse().find(ev);
const gateAnswered = g => last(e => e.event === 'GATE_ANSWER' && e.phase === g);
const gateOpen = g => last(e => e.event === 'GATE_OPEN' && e.phase === g);
const gates = {};
for (const g of ['G1', 'G2', 'G2b', 'G3']) {
  const a = gateAnswered(g), o = gateOpen(g);
  // an answer counts only if nothing reopened the gate after it (migration-check reopens G2b for a regenerated migration)
  gates[g] = a && (!o || String(a.at) >= String(o.at)) ? { status: a.status === 'REJECTED' ? 'REJECTED' : 'PASSED', answer_ref: a.owner_answer?.tool_use_id || a.owner_answer?.prompt_id, at: a.at }
    : o ? { status: 'WAITING_OWNER', at: o.at } : { status: 'PENDING' };
}

// --- phase inference from artifacts (the order matters) -----------------------------------------------
const decisions = readJsonl('discovery/decisions.jsonl');
const assumptions = readJsonl('discovery/assumptions.jsonl');
const redFirst = readJson('tests/red-first.json');
const gate = readJson('gate.json');
const skeleton = readJson('skeleton-check.json');
// review rounds = the highest existing round-N directory (round-0 may hold conductor findings before C5; a count would then point at a missing round-1)
const reviewRounds = fs.existsSync(path.join(runDir, 'review')) ? Math.max(0, ...fs.readdirSync(path.join(runDir, 'review')).filter(d => /^round-\d+$/.test(d)).map(d => +d.slice(6))) : 0;
const closed = last(e => e.event === 'PHASE_END' && e.phase === 'C6' && e.status === 'PASSED');
const returned = last(e => e.event === 'PHASE_END' && ['RETURNED', 'ABANDONED'].includes(e.status));

let phase, phaseStatus = 'NOT_STARTED';
if (returned) phase = returned.status;
else if (closed && gates.G3.status === 'PASSED') { phase = 'CLOSED'; phaseStatus = 'PASSED'; }
else if (closed) { phase = 'C6'; phaseStatus = 'WAITING_OWNER'; } // closer ran, G3 not answered yet
else if (gates.G3.status !== 'PENDING') phase = 'C6';
else if (reviewRounds) { phase = 'C5'; const dedup = readJson(`review/round-${reviewRounds}/dedup.json`); phaseStatus = !dedup ? 'RUNNING' : dedup.verdict === 'CLEAN' ? 'PASSED' : dedup.verdict === 'ESCALATE' ? 'FAILED' : 'RUNNING'; }
else if (gate) { phase = 'C4'; phaseStatus = gate.status === 'GREEN' ? 'PASSED' : 'FAILED'; }
else if (redFirst) { phase = redFirst.verdict === 'RED' ? 'C3b' : 'C2'; phaseStatus = redFirst.verdict === 'RED' ? 'NOT_STARTED' : 'FAILED'; }
else if (skeleton) { phase = 'C3a'; phaseStatus = skeleton.status === 'OK' ? 'PASSED' : 'FAILED'; }
else if (gates.G2.status === 'PASSED') phase = 'C1';
else if (exists('discovery') || decisions.length) { phase = 'B'; phaseStatus = gates.G2.status === 'WAITING_OWNER' ? 'WAITING_OWNER' : 'RUNNING'; }
else if (exists('story.md')) { phase = 'A'; phaseStatus = gates.G1.status === 'PASSED' ? 'PASSED' : gates.G1.status === 'WAITING_OWNER' ? 'WAITING_OWNER' : 'RUNNING'; }
else phase = 'A';
const lastPhaseEv = last(e => e.event === 'PHASE_START' || e.event === 'PHASE_END');
if (lastPhaseEv && lastPhaseEv.phase === phase && phaseStatus === 'NOT_STARTED') phaseStatus = lastPhaseEv.event === 'PHASE_START' ? 'RUNNING' : (lastPhaseEv.status || 'PASSED');

// --- open items -----------------------------------------------------------------------------------------
const decisionsOpen = decisions.filter(d => d.status === 'OPEN').map(d => d.id);
const decidedIds = new Set(decisions.filter(d => d.status === 'DECIDED').map(d => d.id));
const hardListWithoutDecision = assumptions.filter(a => a.status !== 'WITHDRAWN' && a.score?.hard_list && !(a.veto?.replacement_decision_id && decidedIds.has(a.veto.replacement_decision_id))).map(a => a.id);
const findings = [];
if (reviewRounds) for (const f of fs.readdirSync(path.join(runDir, 'review', `round-${reviewRounds}`)).filter(f => f.endsWith('.findings.json'))) { const j = readJson(`review/round-${reviewRounds}/${f}`); if (j) findings.push(...(j.findings || [])); }
const disputes = readJsonl('impl/disputes.jsonl').filter(d => !d.verdict).map(d => d.target_id);
const tokens = journal.filter(e => e.tokens).reduce((s, e) => s + e.tokens, 0);
const budgetEv = last(e => e.event === 'BUDGET_STOP' || e.budget_tokens);

// G2b (D-1): a migration flagged by migration-check (S9b) needs an owner answer FOR THIS migration content; the answer
// carries migration_sha, so a regenerated migration reopens the question. The blocker is a constant id (blockers are ids).
const migrationCheck = readJson('migration-check.json');
const g2bAnswer = gateAnswered('G2b');
const g2bPending = !!migrationCheck?.g2b_required && !(g2bAnswer?.status === 'PASSED' && g2bAnswer.migration_sha === migrationCheck.migration_sha);

const state = {
  story, branch, computed_at: new Date().toISOString(),
  track: readJson('story.meta.json')?.track,
  phase, phase_status: phaseStatus,
  gates, artifacts,
  acceptance_tests_hash: redFirst?.acceptance_tests_hash,
  rounds: { C4: journal.filter(e => e.event === 'PHASE_END' && e.phase === 'C4').length, C5: reviewRounds },
  open_items: {
    decisions_open: decisionsOpen,
    hard_list_assumptions_without_decision: hardListWithoutDecision,
    blockers: [...findings.filter(f => f.severity === 'BLOCKER').map(f => f.id), ...(g2bPending ? ['G2B-MIGRATION'] : [])],
    disputes,
    unspecified_behaviour: findings.filter(f => f.type === 'UNSPECIFIED_BEHAVIOR').map(f => f.id),
  },
  last_run_id: last(e => e.run_id)?.run_id,
  tokens_spent: tokens,
  budget_tokens: budgetEv?.budget_tokens,
};
fs.mkdirSync(runDir, { recursive: true });
// idempotent: rewrite state.json only when something other than computed_at changed — a fresh timestamp alone
// would dirty the tree after every status run (commits happen only with the owner's consent, so churn is noise)
const statePath = path.join(runDir, 'state.json');
const strip = o => JSON.stringify({ ...o, computed_at: undefined });
let previous = null; try { previous = JSON.parse(fs.readFileSync(statePath, 'utf8')); } catch { /* first run */ }
if (!previous || strip(previous) !== strip(state)) fs.writeFileSync(statePath, JSON.stringify(state, null, 2) + '\n'); else state.computed_at = previous.computed_at;
if (args.print) console.log(JSON.stringify(state, null, 2));
else console.log(`status: ${story} @ ${phase} (${phaseStatus}) — gates ${Object.entries(gates).map(([g, v]) => `${g}:${v.status}`).join(' ')} — open: ${decisionsOpen.length} decisions, ${hardListWithoutDecision.length} hard-list assumptions, ${state.open_items.blockers.length} blockers`);
