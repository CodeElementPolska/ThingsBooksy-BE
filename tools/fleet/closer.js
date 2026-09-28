#!/usr/bin/env node
// S7c — closer: after DoD = DONE and the owner's G3 answer, produce the closing artifacts:
//   - runs/<story>/metrics.json   (from journal + artifacts: interruptions, rounds, tokens, coverage, buried decisions)
//   - runs/<story>/close-report.md (one screen for the owner + the checklist text the conductor posts on the GitHub issue via MCP)
//   - journal PHASE_END C6 PASSED
// It does NOT commit and does NOT talk to GitHub (no gh CLI here; the conductor session owns MCP calls).
// Usage: node tools/fleet/closer.js --story NNN-slug [--issue 123]
// Exit 0 · 3 when DoD is not DONE · 1 error
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: closer --story NNN-slug [--issue N]'); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
const readJson = rel => { try { return JSON.parse(fs.readFileSync(path.join(runDir, rel), 'utf8')); } catch { return null; } };
const readJsonl = rel => { try { return fs.readFileSync(path.join(runDir, rel), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)); } catch { return []; } };

const dod = readJson('dod.json');
if (dod?.status !== 'DONE') { console.error(`closer: DoD is ${dod?.status || 'missing'} — run dod.js first`); process.exit(3); }

const journal = readJsonl('journal.jsonl');
const decisions = readJsonl('discovery/decisions.jsonl');
const assumptions = readJsonl('discovery/assumptions.jsonl');
const ac = readJson('ac-matrix.json');
const gate = readJson('gate.json');
const rounds = fs.existsSync(path.join(runDir, 'review')) ? fs.readdirSync(path.join(runDir, 'review')).filter(d => /^round-\d+$/.test(d)).length : 0;
const lastFindings = rounds ? fs.readdirSync(path.join(runDir, 'review', `round-${rounds}`)).filter(f => f.endsWith('.findings.json')).flatMap(f => readJson(`review/round-${rounds}/${f}`)?.findings || []) : [];
// every finding of the story (all review rounds + closing guards) — rule candidates and UNSPECIFIED are counted here,
// not on the last round, which is empty by construction when the loop converged
const allFindings = [
  ...Array.from({ length: rounds }, (_, i) => i + 1).flatMap(n => fs.readdirSync(path.join(runDir, 'review', `round-${n}`)).filter(f => f.endsWith('.findings.json')).flatMap(f => readJson(`review/round-${n}/${f}`)?.findings || [])),
  ...(fs.existsSync(path.join(runDir, 'closing')) ? fs.readdirSync(path.join(runDir, 'closing')).filter(f => f.endsWith('.findings.json')).flatMap(f => readJson(`closing/${f}`)?.findings || []) : []),
];
const implAssumptions = fs.existsSync(path.join(runDir, 'impl')) ? fs.readdirSync(path.join(runDir, 'impl')).flatMap(d => readJson(`impl/${d}/result.json`)?.assumptions || []) : [];
const unspecifiedTests = (ac?.tests_without_known_ac || []).length;
// wall clock of DELIVERY = from the first session-C event (baseline/skeleton) to now; discovery is session B
const firstC = journal.find(e => /^C/.test(e.phase || '') || e.event === 'AGENT_START')?.at || journal[0]?.at;
const first = firstC, lastAt = new Date().toISOString();

// metrics per docs/agent-fleet-v4/workflow.md §6
const metrics = {
  story,
  owner_interruptions: journal.filter(e => e.event === 'GATE_OPEN').length,
  owner_answers: journal.filter(e => e.event === 'OWNER_ANSWER').length,
  decisions: { total: decisions.length, by_owner: decisions.filter(d => d.decided_by === 'owner').length, escalated_from_impl: decisions.filter(d => /writer|arbiter/.test(d.asked_by || '')).length },
  assumptions: { total: assumptions.length + implAssumptions.length, discovery: assumptions.length, delivery: implAssumptions.length, vetoed: assumptions.filter(a => a.veto).length, hard_list: [...assumptions, ...implAssumptions].filter(a => a.score?.hard_list).length },
  repair_rounds: { C4: journal.filter(e => e.event === 'PHASE_END' && e.phase === 'C4').length, C5: rounds },
  review: { findings_total: allFindings.length, findings_last_round: lastFindings.length, closed_by_decision: allFindings.filter(f => f.decision_id).length, closed_by_fix: allFindings.filter(f => f.closed_by === 'fix').length, rule_candidates: allFindings.filter(f => f.rule_candidate).length, unspecified_behaviour: allFindings.filter(f => f.type === 'UNSPECIFIED_BEHAVIOR').length + unspecifiedTests },
  agent_runs: journal.filter(e => e.event === 'AGENT_END').length,
  ac_coverage_percent: ac?.coverage ?? null,
  gate_seconds: gate ? Math.round(gate.ms / 1000) : null,
  tokens_spent: journal.filter(e => e.tokens).reduce((s, e) => s + e.tokens, 0),
  wall_clock_hours: first && lastAt ? +(((new Date(lastAt) - new Date(first)) / 3.6e6).toFixed(2)) : null,
  rebaselines: journal.filter(e => e.event === 'REBASELINE').length,
  escalations: journal.filter(e => e.event === 'ESCALATION').length,
};
fs.writeFileSync(path.join(runDir, 'metrics.json'), JSON.stringify(metrics, null, 2) + '\n');

// close report + GitHub checklist text (conductor posts it)
const tasksFile = path.join(REPO, 'specs', story, 'tasks.md');
const tasks = fs.existsSync(tasksFile) ? (fs.readFileSync(tasksFile, 'utf8').match(/^\s*- \[[xX]\] .*/gm) || []).map(l => l.replace(/^\s*- \[[xX]\]\s*/, '')) : [];
const report = `# Zamknięcie story ${story}

| | |
|---|---|
| DoD | DONE (${dod.checks.length} kontroli) |
| AC pokryte testami | ${metrics.ac_coverage_percent ?? '–'}% |
| Decyzje właściciela | ${metrics.decisions.by_owner} / ${metrics.decisions.total} |
| Założenia (zawetowane) | ${metrics.assumptions.total} (${metrics.assumptions.vetoed}) |
| Rundy napraw C4 / C5 | ${metrics.repair_rounds.C4} / ${metrics.repair_rounds.C5} |
| Zachowanie niezamówione (UNSPECIFIED) | ${metrics.review.unspecified_behaviour} |
| Kandydaci na reguły deterministyczne | ${metrics.review.rule_candidates} |
| Przerwania właściciela | ${metrics.owner_interruptions} |
| Tokeny / czas | ${metrics.tokens_spent} / ${metrics.wall_clock_hours ?? '–'} h |

## Checklista na issue${args.issue ? ` #${args.issue}` : ''} (wkleja dyrygent przez MCP)

${tasks.length ? tasks.map(t => `- [x] ${t}`).join('\n') : '- [x] (brak tasks.md — tor hotfix/średni)'}

Artefakty: \`runs/${story}/\` · spec: \`specs/${story}/\` (od teraz niemutowalna)
`;
fs.writeFileSync(path.join(runDir, 'close-report.md'), report);
fs.appendFileSync(path.join(runDir, 'journal.jsonl'), JSON.stringify({ at: new Date().toISOString(), event: 'PHASE_END', phase: 'C6', status: 'PASSED', artifact: `runs/${story}/close-report.md` }) + '\n');
console.log(`closer: metrics.json + close-report.md written; journal PHASE_END C6 PASSED (${story})`);
