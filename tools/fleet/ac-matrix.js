#!/usr/bin/env node
// S5 — ac-matrix: acceptance-criterion → tests traceability, built from test SOURCES (static) and
// optionally from a TRX run report (pass/fail). No LLM.
//   BE: methods carrying [Trait("AC", "AC-n")]  (D-14)
//   FE: it("[AC-n] …") / test("[AC-n] …") / describe("[AC-n] …")
// Usage: node tools/fleet/ac-matrix.js --story 014-slug [--ac AC-1,AC-2 | --story-file runs/014-slug/story.md] [--trx runs/014-slug/tests/*.trx] [--out runs/014-slug/ac-matrix.json]
// Exit 0 = every AC has ≥1 test; exit 3 = uncovered AC (gate fails); exit 1 = usage/error.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { readAcIds } from './ac-ids.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));

const story = args.story;
if (!story) { console.error('usage: ac-matrix --story NNN-slug [--ac …] [--story-file …] [--trx …] [--out …]'); process.exit(1); }

// --- expected AC ids -----------------------------------------------------------
let expected = [];
if (args.ac) expected = String(args.ac).split(',').map(s => s.trim()).filter(Boolean);
else expected = readAcIds(REPO, story, args['story-file']);

// --- scan test sources -----------------------------------------------------------
function walk(dir, pred, acc = []) {
  if (!fs.existsSync(dir)) return acc;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!['node_modules', 'bin', 'obj', '.angular', 'dist'].includes(e.name)) walk(p, pred, acc); }
    else if (pred(p)) acc.push(p);
  }
  return acc;
}
const tests = []; // {ac, id, kind, file, line}

// C#: attributes may stack above the method; collect [Trait("AC","…")] until the method signature.
for (const file of walk(path.join(REPO, 'backend', 'src'), p => p.endsWith('.cs') && /IntegrationTests|Tests\.Unit/.test(p))) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
  const ns = (lines.find(l => /^\s*namespace\s+/.test(l)) || '').replace(/^\s*namespace\s+([\w.]+).*/, '$1');
  let cls = '';
  let pendingAc = [];
  lines.forEach((line, i) => {
    const c = line.match(/^\s*public\s+(?:sealed\s+|abstract\s+)?class\s+(\w+)/); if (c) cls = c[1];
    for (const m of line.matchAll(/\[Trait\(\s*"AC"\s*,\s*"(AC-\d+)"\s*\)\]/g)) pendingAc.push(m[1]);
    const meth = line.match(/^\s*public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(/);
    if (meth) {
      for (const ac of pendingAc) tests.push({ ac, id: `${ns}.${cls}.${meth[1]}`, kind: 'be', file: path.relative(REPO, file).replace(/\\/g, '/'), line: i + 1 });
      pendingAc = [];
    } else if (/^\s*(public|private|internal)\s+/.test(line) && !/\[/.test(line)) pendingAc = []; // attribute block ended on a non-method member
  });
}
// TS: it/test/describe titles starting with [AC-n]
for (const file of walk(path.join(REPO, 'frontend', 'src'), p => p.endsWith('.spec.ts'))) {
  const src = fs.readFileSync(file, 'utf8');
  for (const m of src.matchAll(/\b(?:it|test|describe)\(\s*['"`]((?:\[AC-\d+\]\s*)+)([^'"`]*)['"`]/g)) {
    const line = src.slice(0, m.index).split('\n').length;
    for (const ac of m[1].match(/AC-\d+/g)) tests.push({ ac, id: `${path.basename(file)}::${m[2].trim()}`, kind: 'fe', file: path.relative(REPO, file).replace(/\\/g, '/'), line });
  }
}

// --- optional TRX results ----------------------------------------------------------
const results = {}; // testMethodFullName → outcome
if (args.trx) {
  const files = String(args.trx).includes('*') ? walk(path.dirname(String(args.trx)), p => p.endsWith('.trx')) : [String(args.trx)];
  for (const f of files) {
    const xml = fs.readFileSync(f, 'utf8');
    for (const m of xml.matchAll(/<UnitTestResult[^>]*testName="([^"]+)"[^>]*outcome="(\w+)"/g)) results[m[1]] = m[2];
  }
}

// --- matrix --------------------------------------------------------------------------
const byAc = {};
for (const t of tests) (byAc[t.ac] ||= []).push({ ...t, outcome: results[t.id] || (Object.keys(results).length ? 'NotRun' : undefined) });
const allAc = [...new Set([...expected, ...Object.keys(byAc)])].sort((a, b) => +a.slice(3) - +b.slice(3));
const uncovered = expected.filter(ac => !byAc[ac]);
const unexpected = Object.keys(byAc).filter(ac => expected.length && !expected.includes(ac));
const matrix = {
  story, expected_ac: expected, uncovered_ac: uncovered, tests_without_known_ac: unexpected,
  coverage: expected.length ? Math.round((expected.length - uncovered.length) / expected.length * 100) : null,
  matrix: Object.fromEntries(allAc.map(ac => [ac, byAc[ac] || []])),
};
const out = args.out || path.join(REPO, 'runs', story, 'ac-matrix.json');
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(matrix, null, 2) + '\n');
console.log(`ac-matrix: ${expected.length} AC expected, ${tests.length} tagged tests, uncovered: ${uncovered.length ? uncovered.join(', ') : 'none'} → ${path.relative(REPO, out)}`);
process.exit(uncovered.length ? 3 : 0);
