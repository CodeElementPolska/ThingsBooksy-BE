#!/usr/bin/env node
// baseline — records the commit a story's CODE work starts from (runs/<story>/baseline.json).
// skeleton-check and coverage-gaps diff against this commit, not against the merge-base with main:
// a story branch may carry unrelated commits (fleet tooling, discovery artifacts) that must not be
// mistaken for skeleton or behaviour changes. Run once by the conductor right before C3a.
// Usage: node tools/fleet/baseline.js --story NNN-slug [--commit <sha>] [--force]
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: baseline --story NNN-slug [--commit sha] [--force]'); process.exit(1); }
const out = path.join(REPO, 'runs', story, 'baseline.json');
if (fs.existsSync(out) && !args.force) { const b = JSON.parse(fs.readFileSync(out, 'utf8')); console.log(`baseline: already ${b.commit} (${b.at}) — use --force to overwrite`); process.exit(0); }
const commit = (spawnSync('git', ['rev-parse', args.commit || 'HEAD'], { cwd: REPO, encoding: 'utf8' }).stdout || '').trim();
if (!/^[0-9a-f]{40}$/.test(commit)) { console.error('baseline: cannot resolve commit'); process.exit(1); }
const dirty = (spawnSync('git', ['status', '--porcelain', '--', 'backend', 'frontend'], { cwd: REPO, encoding: 'utf8' }).stdout || '').trim();
if (dirty && !args.force) { console.error('baseline: backend/ or frontend/ already has uncommitted changes — the baseline would not be the pre-story state:\n' + dirty.split('\n').slice(0, 10).join('\n')); process.exit(3); }
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify({ story, commit, at: new Date().toISOString() }, null, 2) + '\n');
console.log(`baseline: ${story} → ${commit.slice(0, 7)}`);
