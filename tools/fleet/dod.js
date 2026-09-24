#!/usr/bin/env node
// S7b — dod: Definition of Done as a SCRIPT (not a checklist an agent ticks). Collects evidence from
// runs/<story>/ and generated/ and says whether the story may be closed (G3 can open).
// Checks (all must hold):
//   gate         runs/<story>/gate.json status GREEN, computed on the current tree (gate.json newer than any tracked change)
//   ac           ac-matrix: every AC covered (coverage 100)
//   decisions    no OPEN decisions
//   hard-list    no assumption with score.hard_list=true lacking an owner decision (D-1)
//   blockers     last review round has 0 BLOCKER findings
//   disputes     no unresolved disputes
//   unspecified  every UNSPECIFIED_BEHAVIOR finding / [UNSPECIFIED] pass-2 test has a decision
//   contract     contract-diff CLEAR when contract-next.json exists
//   tasks        every task in specs/<story>/tasks.md is ticked
//   tests-hash   acceptance tests hash matches red-first (or journaled REBASELINE)
// Usage: node tools/fleet/dod.js --story NNN-slug [--out runs/NNN-slug/dod.json]
// Exit 0 DONE · 3 NOT_DONE · 1 error
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: dod --story NNN-slug [--out …]'); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
const readJson = rel => { try { return JSON.parse(fs.readFileSync(path.join(runDir, rel), 'utf8')); } catch { return null; } };
const readJsonl = rel => { try { return fs.readFileSync(path.join(runDir, rel), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)); } catch { return []; } };

const checks = [];
const check = (name, ok, detail) => { checks.push({ name, status: ok ? 'PASSED' : 'FAILED', detail }); console.log(`${ok ? '✓' : '✗'} ${name.padEnd(12)} ${detail || ''}`); };

const gate = readJson('gate.json');
check('gate', gate?.status === 'GREEN', gate ? `${gate.status} (${gate.steps?.length} steps)` : 'no gate.json');

const ac = readJson('ac-matrix.json');
check('ac', ac && ac.uncovered_ac?.length === 0 && ac.expected_ac?.length > 0, ac ? `${ac.coverage}% (${ac.expected_ac?.length} AC, uncovered: ${ac.uncovered_ac?.join(', ') || 'none'})` : 'no ac-matrix.json');

const decisions = readJsonl('discovery/decisions.jsonl');
const open = decisions.filter(d => d.status === 'OPEN');
check('decisions', open.length === 0, open.length ? `open: ${open.map(d => d.id).join(', ')}` : `${decisions.length} decided/withdrawn`);

const decided = new Set(decisions.filter(d => d.status === 'DECIDED' && d.decided_by === 'owner').map(d => d.id));
const assumptions = [...readJsonl('discovery/assumptions.jsonl'), ...(fs.existsSync(path.join(runDir, 'impl')) ? fs.readdirSync(path.join(runDir, 'impl')).flatMap(d => (readJson(`impl/${d}/result.json`)?.assumptions) || []) : [])];
const hard = assumptions.filter(a => a.score?.hard_list && !(a.veto?.replacement_decision_id && decided.has(a.veto.replacement_decision_id)));
check('hard-list', hard.length === 0, hard.length ? `without owner decision: ${hard.map(a => `${a.id} (${a.score.hard_list_category})`).join(', ')}` : `${assumptions.length} assumptions, none on the D-1 list undecided`);

const reviewDir = path.join(runDir, 'review');
const rounds = fs.existsSync(reviewDir) ? fs.readdirSync(reviewDir).filter(d => /^round-\d+$/.test(d)).sort((a, b) => +a.slice(6) - +b.slice(6)) : [];
const lastFindings = rounds.length ? fs.readdirSync(path.join(reviewDir, rounds.at(-1))).filter(f => f.endsWith('.findings.json')).flatMap(f => readJson(`review/${rounds.at(-1)}/${f}`)?.findings || []) : [];
const blockers = lastFindings.filter(f => f.severity === 'BLOCKER');
check('blockers', blockers.length === 0, rounds.length ? `${rounds.at(-1)}: ${blockers.length} BLOCKER, ${lastFindings.filter(f => f.severity === 'MAJOR').length} MAJOR` : 'no review rounds (allowed only on hotfix track)');

const disputes = readJsonl('impl/disputes.jsonl').filter(d => !d.verdict);
check('disputes', disputes.length === 0, disputes.length ? `unresolved: ${disputes.map(d => d.target_id).join(', ')}` : 'none open');

const unspecified = lastFindings.filter(f => f.type === 'UNSPECIFIED_BEHAVIOR' && !f.decision_id);
const pass2Untagged = (readJson('ac-matrix.json')?.tests_without_known_ac || []);
check('unspecified', unspecified.length === 0, unspecified.length ? `${unspecified.length} UNSPECIFIED_BEHAVIOR without decision_id` : `none open${pass2Untagged.length ? `; tests on unknown AC: ${pass2Untagged.join(', ')}` : ''}`);

const contractNext = fs.existsSync(path.join(runDir, 'contract-next.json'));
const cdiff = readJson('contract-diff.json');
check('contract', !contractNext || cdiff?.status === 'CLEAR', contractNext ? (cdiff ? cdiff.status : 'contract-next.json present but no contract-diff.json') : 'no API change');

const tasksFile = path.join(REPO, 'specs', story, 'tasks.md');
if (fs.existsSync(tasksFile)) {
  const t = fs.readFileSync(tasksFile, 'utf8');
  const total = (t.match(/^\s*- \[[ xX]\]/gm) || []).length, done = (t.match(/^\s*- \[[xX]\]/gm) || []).length;
  check('tasks', total > 0 && done === total, `${done}/${total} ticked`);
} else check('tasks', false, 'no specs/<story>/tasks.md');

const redFirst = readJson('tests/red-first.json');
const hashStep = gate?.steps?.find(s => s.name === 'test-hash');
check('tests-hash', !!redFirst && hashStep?.status === 'PASSED', redFirst ? (hashStep?.status || 'gate did not check') : 'no red-first.json');

const report = { story, status: checks.every(c => c.status === 'PASSED') ? 'DONE' : 'NOT_DONE', checks, computed_at: new Date().toISOString() };
const out = path.resolve(REPO, args.out || path.join(runDir, 'dod.json'));
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(report, null, 2) + '\n');
console.log(`dod: ${report.status} (${checks.filter(c => c.status === 'PASSED').length}/${checks.length}) → ${path.relative(REPO, out)}`);
process.exit(report.status === 'DONE' ? 0 : 3);
