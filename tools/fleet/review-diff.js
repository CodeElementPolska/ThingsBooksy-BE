#!/usr/bin/env node
// S9 — review-diff: prepares the INPUT of a review round (C5) without an LLM.
//   runs/<story>/review/round-N/diff.patch  — the code diff the reviewers see (backend/, frontend/src only)
//   runs/<story>/review/round-N/scope.json  — {round, base, tree, head, files[]}: `tree` is a git tree object
//                                             of the CURRENT working tree (tracked + untracked, .gitignore
//                                             respected) written through a temporary index, so a round can
//                                             be diffed against the previous one without committing.
// Round 1 diffs the story baseline (runs/<story>/baseline.json) → working tree; round N ≥ 2 diffs the
// previous round's tree → working tree (reviewers see only the fixes, D-9 / workflow §1.3 C5).
// Usage: node tools/fleet/review-diff.js --story NNN-slug [--round N]
// Exit 0 · 3 = empty diff (nothing to review) · 1 = error
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: review-diff --story NNN-slug [--round N]'); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
const reviewDir = path.join(runDir, 'review');
// EF-generated files are noise for a reviewer (the migration's Up/Down itself stays in scope)
const CODE_PATHS = ['backend', 'frontend/src', ':(glob,exclude)**/*.Designer.cs', ':(glob,exclude)**/*ModelSnapshot.cs'];

const git = (a, env = {}) => { const r = spawnSync('git', a, { cwd: REPO, encoding: 'utf8', env: { ...process.env, ...env } }); if (r.status !== 0) { console.error(`review-diff: git ${a.join(' ')} failed:\n${r.stderr}`); process.exit(1); } return r.stdout; };

// --- round & base --------------------------------------------------------------------------------
const existing = fs.existsSync(reviewDir) ? fs.readdirSync(reviewDir).filter(d => /^round-\d+$/.test(d)).map(d => +d.slice(6)).sort((a, b) => a - b) : [];
const round = args.round ? +args.round : (existing.at(-1) || 0) + 1;
let base;
if (round === 1) {
  const bl = path.join(runDir, 'baseline.json');
  if (!fs.existsSync(bl)) { console.error('review-diff: no runs/<story>/baseline.json (run baseline.js before C3a)'); process.exit(1); }
  base = JSON.parse(fs.readFileSync(bl, 'utf8')).commit;
} else {
  const prev = path.join(reviewDir, `round-${round - 1}`, 'scope.json');
  if (!fs.existsSync(prev)) { console.error(`review-diff: round ${round} needs ${path.relative(REPO, prev)}`); process.exit(1); }
  base = JSON.parse(fs.readFileSync(prev, 'utf8')).tree;
}

// --- snapshot the working tree as a git tree object (temporary index, real index untouched) --------------
const idx = path.join(os.tmpdir(), `fleet-review-index-${process.pid}-${Date.now()}`);
const env = { GIT_INDEX_FILE: idx };
try {
  git(['read-tree', 'HEAD'], env);
  git(['add', '-A', '--', ...CODE_PATHS], env);
  var tree = git(['write-tree'], env).trim();
} finally { try { fs.unlinkSync(idx); } catch { /* ignore */ } }
const head = git(['rev-parse', 'HEAD']).trim();

// --- diff ------------------------------------------------------------------------------------------------
const patch = git(['diff', '--no-color', base, tree, '--', ...CODE_PATHS]);
const files = git(['diff', '--name-only', base, tree, '--', ...CODE_PATHS]).split('\n').filter(Boolean);
const numstat = git(['diff', '--numstat', base, tree, '--', ...CODE_PATHS]).split('\n').filter(Boolean).map(l => l.split('\t'));
const stats = { files: files.length, insertions: numstat.reduce((s, [a]) => s + (+a || 0), 0), deletions: numstat.reduce((s, [, d]) => s + (+d || 0), 0) };

const outDir = path.join(reviewDir, `round-${round}`);
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, 'diff.patch'), patch);
const scope = { story, round, base, tree, head, diff_sha256: crypto.createHash('sha256').update(patch).digest('hex'), files, stats, created_at: new Date().toISOString() };
fs.writeFileSync(path.join(outDir, 'scope.json'), JSON.stringify(scope, null, 2) + '\n');
console.log(`review-diff: round ${round} — ${stats.files} files, +${stats.insertions} −${stats.deletions} (base ${base.slice(0, 7)} → tree ${tree.slice(0, 7)}) → ${path.relative(REPO, outDir)}/{diff.patch,scope.json}`);
process.exit(files.length ? 0 : 3);
