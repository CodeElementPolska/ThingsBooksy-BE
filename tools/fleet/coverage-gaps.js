#!/usr/bin/env node
// S4b — coverage-gaps: which lines/branches of the story's NEW production code are not exercised by
// the story's acceptance tests. Input for the test-designer's second (sighted) pass (D-4b).
//   1. git diff -U0 <merge-base>  → added/modified line ranges per production file
//   2. dotnet test --collect:"XPlat Code Coverage" (coverlet, Cobertura) for the story's tests
//   3. intersect: uncovered lines + partially covered branches inside those ranges
// Usage: node tools/fleet/coverage-gaps.js --story NNN-slug [--base main] [--ac AC-1,…] [--projects …] [--out runs/NNN-slug/coverage-gaps.json]
// Exit 0 = no gaps · 3 = gaps found (pass 2 required) · 1 = error
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { readAcIds } from './ac-ids.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: coverage-gaps --story NNN-slug [--base ref] [--ac …] [--projects …] [--out …]'); process.exit(1); }
const run = (cmd, a, extra = {}) => spawnSync(cmd, a, { cwd: REPO, encoding: 'utf8', shell: true, ...extra });

// --- 1. changed production lines ------------------------------------------------------------
const baselineFile = path.join(REPO, 'runs', story, 'baseline.json');
const baseline = fs.existsSync(baselineFile) ? JSON.parse(fs.readFileSync(baselineFile, 'utf8')).commit : null;
const mergeBase = baseline && !args.base ? baseline : run('git', ['merge-base', args.base || 'main', 'HEAD']).stdout.trim();
const diff = run('git', ['diff', '-U0', mergeBase, '--', 'backend/src/Modules', 'backend/src/Shared/ThingsBooksy.Shared.Abstractions']).stdout;
const untracked = run('git', ['ls-files', '--others', '--exclude-standard', '--', 'backend/src/Modules']).stdout.split('\n').filter(f => f.endsWith('.cs'));
const changed = {}; // relPath → Set(lines)
let cur = null;
for (const line of diff.split('\n')) {
  const f = line.match(/^\+\+\+ b\/(.+)$/); if (f) { cur = f[1]; continue; }
  const h = line.match(/^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@/);
  if (h && cur && /\.cs$/.test(cur) && !/IntegrationTests|Tests\.Unit|Migrations/.test(cur)) {
    const start = +h[1], count = h[2] === undefined ? 1 : +h[2];
    changed[cur] ||= new Set(); for (let i = 0; i < count; i++) changed[cur].add(start + i);
  }
}
for (const f of untracked) if (!/IntegrationTests|Tests\.Unit|Migrations/.test(f)) { const n = fs.readFileSync(path.join(REPO, f), 'utf8').split('\n').length; changed[f] = new Set(Array.from({ length: n }, (_, i) => i + 1)); }
if (!Object.keys(changed).length) { console.log('coverage-gaps: no changed production code'); process.exit(0); }

// --- 2. coverage run ------------------------------------------------------------------------
let acIds = args.ac ? String(args.ac).split(',') : [];
if (!acIds.length) acIds = readAcIds(REPO, story);
function walk(dir, pred, acc = []) { if (!fs.existsSync(dir)) return acc; for (const e of fs.readdirSync(dir, { withFileTypes: true })) { const p = path.join(dir, e.name); if (e.isDirectory()) { if (!['bin', 'obj', 'node_modules'].includes(e.name)) walk(p, pred, acc); } else if (pred(p)) acc.push(p); } return acc; }
const projects = args.projects ? String(args.projects).split(',') : walk(path.join(REPO, 'backend', 'src', 'Modules'), p => /\.IntegrationTests\.csproj$/.test(p));
const covDir = path.join(REPO, 'runs', story, 'tests', 'coverage');
fs.rmSync(covDir, { recursive: true, force: true }); fs.mkdirSync(covDir, { recursive: true });
for (const proj of projects) {
  const a = ['test', `"${proj}"`, '--collect:"XPlat Code Coverage"', '--results-directory', `"${covDir}"`, '--nologo', '-v', 'q'];
  if (acIds.length) a.push('--filter', `"${acIds.map(id => `AC=${id}`).join('|')}"`);
  const r = run('dotnet', a); if (r.status !== 0 && !/No test is available/.test(r.stdout)) console.error(`coverage-gaps: tests in ${path.basename(proj)} exited ${r.status} (failing tests still yield coverage)`);
}

// --- 3. intersect with Cobertura ---------------------------------------------------------------
const gaps = []; const seen = new Set();
for (const xmlFile of walk(covDir, p => p.endsWith('coverage.cobertura.xml'))) {
  const xml = fs.readFileSync(xmlFile, 'utf8');
  for (const cls of xml.matchAll(/<class [^>]*filename="([^"]+)"[^>]*>([\s\S]*?)<\/class>/g)) {
    const rel = path.relative(REPO, path.resolve(REPO, cls[1])).replace(/\\/g, '/');
    const lines = changed[rel]; if (!lines) continue;
    for (const ln of cls[2].matchAll(/<line number="(\d+)" hits="(\d+)"(?: branch="(true|True)"[^>]*condition-coverage="(\d+)% \((\d+)\/(\d+)\)")?/g)) {
      const n = +ln[1]; if (!lines.has(n)) continue;
      const key = rel + ':' + n; if (seen.has(key)) continue;
      const hits = +ln[2], branch = ln[3] !== undefined, covered = ln[5] !== undefined ? +ln[5] : null, total = ln[6] !== undefined ? +ln[6] : null;
      if (hits === 0) { gaps.push({ file: rel, line: n, kind: 'uncovered-line' }); seen.add(key); }
      else if (branch && covered !== null && covered < total) { gaps.push({ file: rel, line: n, kind: 'partial-branch', branches: `${covered}/${total}` }); seen.add(key); }
    }
  }
}
const report = { story, base: mergeBase, changed_files: Object.keys(changed), gaps, summary: { uncovered_lines: gaps.filter(g => g.kind === 'uncovered-line').length, partial_branches: gaps.filter(g => g.kind === 'partial-branch').length } };
const out = path.resolve(REPO, args.out || `runs/${story}/coverage-gaps.json`);
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(report, null, 2) + '\n');
console.log(`coverage-gaps: ${report.summary.uncovered_lines} uncovered lines, ${report.summary.partial_branches} partial branches in ${Object.keys(changed).length} changed files → ${path.relative(REPO, out)}`);
process.exit(gaps.length ? 3 : 0);
