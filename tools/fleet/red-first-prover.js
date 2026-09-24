#!/usr/bin/env node
// S4 — red-first-prover: after the skeleton (C3a) and the blind tester's pass (C2), the story's
// acceptance tests must (1) COMPILE and (2) FAIL. A test that passes before the behaviour exists
// proves nothing (audit defect 1). Also records the hash of acceptance-test sources for C4.
// BE: dotnet build <test projects> → dotnet test --filter "AC=AC-1|AC=AC-2…" --logger trx
// FE: (optional, --fe) npx vitest run -t "\[AC-n\]"
// Usage: node tools/fleet/red-first-prover.js --story NNN-slug [--ac AC-1,AC-2] [--projects a.csproj,b.csproj] [--fe] [--out runs/NNN-slug/tests/red-first.json]
// Exit 0 = RED (all story tests compiled and failed) · 3 = NOT_RED / NO_TESTS / BUILD_FAILED · 1 = usage
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: red-first-prover --story NNN-slug [--ac …] [--projects …] [--fe] [--out …]'); process.exit(1); }

// --- AC ids -------------------------------------------------------------------------------
let acIds = args.ac ? String(args.ac).split(',').map(s => s.trim()).filter(Boolean) : [];
if (!acIds.length) {
  const src = [path.join(REPO, 'runs', story, 'story.md'), path.join(REPO, 'specs', story, 'spec.md')].find(f => fs.existsSync(f));
  if (src) acIds = [...new Set([...fs.readFileSync(src, 'utf8').matchAll(/\bAC-\d+\b/g)].map(m => m[0]))];
}
if (!acIds.length) { console.error('red-first-prover: no AC ids (pass --ac or provide runs/<story>/story.md)'); process.exit(1); }

// --- test projects --------------------------------------------------------------------------
function walk(dir, pred, acc = []) {
  if (!fs.existsSync(dir)) return acc;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!['bin', 'obj', 'node_modules'].includes(e.name)) walk(p, pred, acc); } else if (pred(p)) acc.push(p);
  }
  return acc;
}
const projects = args.projects ? String(args.projects).split(',') : walk(path.join(REPO, 'backend', 'src', 'Modules'), p => /\.IntegrationTests\.csproj$/.test(p));
const run = (cmd, a, extra = {}) => spawnSync(cmd, a, { cwd: REPO, encoding: 'utf8', shell: true, ...extra });
const outPath = path.resolve(REPO, args.out || `runs/${story}/tests/red-first.json`);
const trxDir = path.join(path.dirname(outPath), 'trx-red-first');
fs.rmSync(trxDir, { recursive: true, force: true });
fs.mkdirSync(trxDir, { recursive: true });

const report = { story, ac: acIds, compiled: {}, tests: [], fe: null, verdict: null, acceptance_tests_hash: null };

// (1) compile
for (const proj of projects) {
  const r = run('dotnet', ['build', `"${proj}"`, '--nologo', '-v', 'q']);
  report.compiled[path.relative(REPO, proj).replace(/\\/g, '/')] = r.status === 0 ? 'OK' : 'FAILED';
  if (r.status !== 0) report.compile_errors = (report.compile_errors || '') + (r.stdout + r.stderr).split('\n').filter(l => /error/.test(l)).slice(0, 20).join('\n');
}
if (Object.values(report.compiled).includes('FAILED')) {
  report.verdict = 'BUILD_FAILED';
} else {
  // (2) run only the story's tests; expect failures
  const filter = acIds.map(id => `AC=${id}`).join('|');
  for (const proj of projects) {
    run('dotnet', ['test', `"${proj}"`, '--no-build', '--filter', `"${filter}"`, '--logger', 'trx', '--results-directory', `"${trxDir}"`, '--nologo', '-v', 'q']);
  }
  for (const trx of walk(trxDir, p => p.endsWith('.trx'))) {
    const xml = fs.readFileSync(trx, 'utf8');
    for (const m of xml.matchAll(/<UnitTestResult[^>]*testName="([^"]+)"[^>]*outcome="(\w+)"[\s\S]*?(?:<Message>([\s\S]*?)<\/Message>)?/g)) {
      report.tests.push({ id: m[1], outcome: m[2], message: (m[3] || '').replace(/\s+/g, ' ').slice(0, 200) });
    }
  }
  const passed = report.tests.filter(t => t.outcome === 'Passed');
  report.verdict = !report.tests.length ? 'NO_TESTS' : passed.length ? 'NOT_RED' : 'RED';
  report.passed_prematurely = passed.map(t => t.id);
}

// optional FE
if (args.fe) {
  const r = run('npx', ['vitest', 'run', '-t', `"${acIds.map(id => `\\[${id}\\]`).join('|')}"`, '--reporter=json'], { cwd: path.join(REPO, 'frontend') });
  try { const j = JSON.parse(r.stdout.slice(r.stdout.indexOf('{'))); report.fe = { total: j.numTotalTests, failed: j.numFailedTests, passed: j.numPassedTests }; if (j.numPassedTests > 0 && report.verdict === 'RED') report.verdict = 'NOT_RED'; }
  catch { report.fe = { error: 'could not parse vitest json output' }; }
}

// hash of acceptance-test sources (post-format is the gate's job; here = current state)
const testFiles = [...walk(path.join(REPO, 'backend', 'src', 'Modules'), p => /\.IntegrationTests[\\/].*\.cs$/.test(p)), ...walk(path.join(REPO, 'frontend', 'src'), p => p.endsWith('.spec.ts'))].sort();
const h = crypto.createHash('sha256'); for (const f of testFiles) { h.update(path.relative(REPO, f)); h.update(fs.readFileSync(f)); }
report.acceptance_tests_hash = h.digest('hex');

fs.mkdirSync(path.dirname(outPath), { recursive: true });
fs.writeFileSync(outPath, JSON.stringify(report, null, 2) + '\n');
console.log(`red-first-prover: ${report.verdict} — ${report.tests.length} story tests, ${(report.passed_prematurely || []).length} passed prematurely; hash ${report.acceptance_tests_hash.slice(0, 12)}… → ${path.relative(REPO, outPath)}`);
process.exit(report.verdict === 'RED' ? 0 : 3);
