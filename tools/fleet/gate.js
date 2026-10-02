#!/usr/bin/env node
// S6 — gate (phase C4): the deterministic bar every implementation must clear before any LLM review.
// Steps (each recorded, first hard failure stops unless --all):
//   migration    migration-check.json is present, fresh (file hashes) and, when g2b_required, answered for THIS
//                migration (GATE_ANSWER G2b PASSED with the same migration_sha) — D-1; SKIPPED only when no migration
//   build        dotnet build backend/ThingsBooksy.slnx (analyzers run here)
//   format       dotnet format --verify-no-changes
//   arch-tests   dotnet test --filter Category=Architecture   (SKIPPED until the deferred analyzers epic lands)
//   tests        dotnet test (story tests by AC filter, or everything with --full)
//   swagger      re-export generated/swagger.base.json (Tooling test)   [--no-swagger to skip]
//   contract     contract-diff (contract-next vs re-exported swagger)   [needs runs/<story>/contract-next.json]
//   ac-matrix    every AC has ≥1 test
//   test-hash    acceptance-test sources unchanged since red-first proof (post-format)
// Usage: node tools/fleet/gate.js --story NNN-slug [--full] [--all] [--no-swagger] [--out runs/NNN-slug/gate.json]
// Exit 0 GREEN · 3 RED · 1 error
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { readAcIds } from './ac-ids.js';
import { sha256File, sha256Files } from './hash.js';
import { collectMigrationChanges, currentMigrationSha } from './migration-check.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: gate --story NNN-slug [--full] [--all] [--no-swagger] [--out …]'); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
const run = (cmd, a, extra = {}) => spawnSync(cmd, a, { cwd: REPO, encoding: 'utf8', shell: true, ...extra });
// shell:true → quote the node binary too: "C:\Program Files\nodejs\node.exe" has a space
const node = (script, a) => run(`"${process.execPath}"`, [`"${path.join(HERE, script)}"`, ...a]);

const acIds = readAcIds(REPO, story);

const steps = [];
const t0 = Date.now();
function step(name, fn) {
  const start = Date.now();
  let res;
  try { res = fn(); } catch (e) { res = { status: 'FAILED', detail: String(e.message || e) }; }
  steps.push({ name, ...res, ms: Date.now() - start });
  console.log(`${res.status === 'PASSED' ? '✓' : res.status === 'SKIPPED' ? '–' : '✗'} ${name.padEnd(11)} ${res.status}${res.detail ? ' — ' + String(res.detail).split('\n')[0].slice(0, 120) : ''}`);
  return res.status !== 'FAILED' || args.all;
}
const tail = (r, n = 15) => (r.stdout + '\n' + r.stderr).split('\n').filter(l => /error|fail|warn/i.test(l)).slice(0, n).join('\n');

let go = true;
go = go && step('migration', () => {
  const bl = path.join(runDir, 'baseline.json');
  if (!fs.existsSync(bl)) return { status: 'FAILED', detail: 'no baseline.json — run baseline.js before C3a (migration-check diffs from the story start commit)' };
  const baseline = JSON.parse(fs.readFileSync(bl, 'utf8'));
  const { migrations, snapshots } = collectMigrationChanges(REPO, baseline.commit);
  const mcPath = path.join(runDir, 'migration-check.json');
  const mc = fs.existsSync(mcPath) ? JSON.parse(fs.readFileSync(mcPath, 'utf8')) : null;
  if (!migrations.length) {
    if (snapshots.length) return { status: 'FAILED', detail: 'model snapshot changed but there is no migration — run dotnet ef migrations add, then migration-check.js' };
    if (!mc?.g2b_required) return { status: 'SKIPPED', detail: 'no migration since baseline' };
    // be-writer forecast DESTRUCTIVE and no migration exists: the owner must confirm that (the answer binds to the empty set's sha)
  }
  if (!mc) return { status: 'FAILED', detail: `migration present (${migrations.map(m => path.basename(m.path)).join(', ')}) but no migration-check.json — run migration-check.js` };
  if (mc.commit !== baseline.commit) return { status: 'FAILED', detail: `migration-check.json was computed from ${String(mc.commit).slice(0, 8)}, baseline is ${String(baseline.commit).slice(0, 8)} — rerun migration-check.js` };
  const nowSha = currentMigrationSha(REPO, migrations);
  if (nowSha !== mc.migration_sha) return { status: 'FAILED', detail: 'migration-check.json is stale (migration files added, edited or deleted since the report) — rerun migration-check.js (and G2b if it is required)' };
  if (mc.g2b_required) {
    const jp = path.join(runDir, 'journal.jsonl');
    const journal = fs.existsSync(jp) ? fs.readFileSync(jp, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) : [];
    const a = [...journal].reverse().find(e => e.event === 'GATE_ANSWER' && e.phase === 'G2b');
    if (!a) return { status: 'FAILED', detail: `migration-check ${mc.verdict}: G2b not answered — AskUserQuestion, then decide.js --gate G2b` };
    if (a.status !== 'PASSED') return { status: 'FAILED', detail: 'G2b REJECTED — regenerate the migration and rerun migration-check' };
    if (a.migration_sha !== mc.migration_sha) return { status: 'FAILED', detail: 'G2b answer is for another migration (migration_sha mismatch) — ask again for this one' };
  }
  return { status: 'PASSED', detail: `migration-check ${mc.verdict}${mc.g2b_required ? ' (G2b PASSED for this migration)' : ''}` };
});
go = go && step('build', () => { const r = run('dotnet', ['build', 'backend/ThingsBooksy.slnx', '--nologo', '-v', 'q']); return r.status === 0 ? { status: 'PASSED' } : { status: 'FAILED', detail: tail(r) }; });
go = go && step('format', () => { const r = run('dotnet', ['format', 'backend/ThingsBooksy.slnx', '--verify-no-changes', '--no-restore']); return r.status === 0 ? { status: 'PASSED' } : { status: 'FAILED', detail: tail(r) || 'formatting differences' }; });
go = go && step('arch-tests', () => ({ status: 'SKIPPED', detail: 'deferred epic: docs/backlog/deferred-static-analysis-sonarqube.md' }));
go = go && step('tests', () => {
  const a = ['test', 'backend/ThingsBooksy.slnx', '--no-build', '--nologo', '-v', 'q', '--logger', 'trx', '--results-directory', `"${path.join(runDir, 'tests', 'trx-gate')}"`];
  if (!args.full && acIds.length) a.push('--filter', `"${acIds.map(id => `AC=${id}`).join('|')}|Category!=Tooling"`);
  else a.push('--filter', '"Category!=Tooling"');
  const r = run('dotnet', a);
  const m = (r.stdout.match(/Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)/g) || []).map(s => s.match(/Failed:\s+(\d+),\s+Passed:\s+(\d+)/)).reduce((acc, x) => ({ failed: acc.failed + +x[1], passed: acc.passed + +x[2] }), { failed: 0, passed: 0 });
  const failedAny = r.status !== 0 || /Failed!/.test(r.stdout);
  return failedAny ? { status: 'FAILED', detail: tail(r, 25) || `exit ${r.status}` } : { status: 'PASSED', detail: `${m.passed} passed` };
});
go = go && step('swagger', () => {
  if (args['no-swagger']) return { status: 'SKIPPED' };
  const r = run('dotnet', ['test', 'backend/src/Shared/ThingsBooksy.Shared.IntegrationTests', '--no-build', '--filter', 'Category=Tooling', '--nologo', '-v', 'q']);
  return r.status === 0 ? { status: 'PASSED', detail: 'generated/ refreshed' } : { status: 'FAILED', detail: tail(r) };
});
go = go && step('contract', () => {
  if (!fs.existsSync(path.join(runDir, 'contract-next.json'))) return { status: 'SKIPPED', detail: 'no contract-next.json (story without API change)' };
  const r = node('contract-diff.js', ['--story', story]);
  return r.status === 0 ? { status: 'PASSED', detail: r.stdout.trim() } : { status: 'FAILED', detail: `${r.stdout}\n${r.stderr}`.trim() };
});
go = go && step('ac-matrix', () => {
  if (!acIds.length) return { status: 'SKIPPED', detail: 'no AC ids found' };
  const r = node('ac-matrix.js', ['--story', story]);
  return r.status === 0 ? { status: 'PASSED', detail: r.stdout.trim() } : { status: 'FAILED', detail: `${r.stdout}\n${r.stderr}`.trim() };
});
go = go && step('test-hash', () => {
  const rf = path.join(runDir, 'tests', 'red-first.json');
  if (!fs.existsSync(rf)) return { status: 'SKIPPED', detail: 'no red-first.json' };
  const redFirst = JSON.parse(fs.readFileSync(rf, 'utf8'));
  const expected = redFirst.acceptance_tests_hash;
  function walk(dir, pred, acc = []) { if (!fs.existsSync(dir)) return acc; for (const e of fs.readdirSync(dir, { withFileTypes: true })) { const p = path.join(dir, e.name); if (e.isDirectory()) { if (!['bin', 'obj', 'node_modules'].includes(e.name)) walk(p, pred, acc); } else if (pred(p)) acc.push(p); } return acc; }
  // D-4b: the hash protects the blind pass's files — pass 2 may ADD test files, so re-hash exactly the set recorded
  // by red-first-prover (a removed file fails); fall back to "all test files" for reports without the list.
  let files;
  if (Array.isArray(redFirst.acceptance_test_files)) {
    files = redFirst.acceptance_test_files.map(rel => path.join(REPO, rel));
    const missing = files.filter(f => !fs.existsSync(f));
    if (missing.length) return { status: 'FAILED', detail: `blind-pass test file(s) removed: ${missing.map(f => path.relative(REPO, f)).join(', ')}` };
  } else {
    files = [...walk(path.join(REPO, 'backend', 'src', 'Modules'), p => /\.IntegrationTests[\\/].*\.cs$/.test(p)), ...walk(path.join(REPO, 'frontend', 'src'), p => p.endsWith('.spec.ts'))].sort();
  }
  // hash_version 2 (hash.js): CRLF-normalised content, forward-slash paths; legacy reports (no version) = raw bytes
  let actual;
  if (redFirst.hash_version >= 2) actual = sha256Files(REPO, files, f => path.relative(REPO, f).replace(/\\/g, '/'));
  else { const h = crypto.createHash('sha256'); for (const f of files) { h.update(path.relative(REPO, f)); h.update(fs.readFileSync(f)); } actual = h.digest('hex'); }
  const rebaselined = fs.existsSync(path.join(runDir, 'journal.jsonl')) && fs.readFileSync(path.join(runDir, 'journal.jsonl'), 'utf8').includes(`"REBASELINE"`) && fs.readFileSync(path.join(runDir, 'journal.jsonl'), 'utf8').includes(actual);
  return actual === expected || rebaselined ? { status: 'PASSED' } : { status: 'FAILED', detail: 'acceptance tests changed since red-first proof (no REBASELINE in journal)' };
});

const report = { story, status: steps.some(s => s.status === 'FAILED') ? 'RED' : 'GREEN', steps, ms: Date.now() - t0 };
const out = path.resolve(REPO, args.out || path.join(runDir, 'gate.json'));
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(report, null, 2) + '\n');
console.log(`gate: ${report.status} in ${Math.round(report.ms / 1000)}s → ${path.relative(REPO, out)}`);
process.exit(report.status === 'GREEN' ? 0 : 3);
