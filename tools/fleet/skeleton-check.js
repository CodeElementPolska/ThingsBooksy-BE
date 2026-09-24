#!/usr/bin/env node
// S4c — skeleton-check: in phase C3a (D-4a) the implementer may add ONLY skeleton files:
// domain entities, EF configuration/DbContext, read models, migrations. No handlers, no
// features, no endpoints, no event handlers, no Api project changes, no frontend.
// Compares the working tree (staged + unstaged + untracked) against --base (default: merge-base with main).
// Usage: node tools/fleet/skeleton-check.js --story NNN-slug [--base main] [--out runs/NNN-slug/skeleton-check.json]
// Exit 0 ok · 3 violations · 1 error
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story; if (!story) { console.error('usage: skeleton-check --story NNN-slug [--base ref] [--out …]'); process.exit(1); }
const baseRef = args.base || 'main';

const git = (...a) => { const r = spawnSync('git', a, { cwd: REPO, encoding: 'utf8' }); if (r.status !== 0) { console.error(r.stderr); process.exit(1); } return r.stdout; };
const mergeBase = git('merge-base', baseRef, 'HEAD').trim();
const changed = new Set([
  ...git('diff', '--name-only', mergeBase).split('\n'),
  ...git('ls-files', '--others', '--exclude-standard').split('\n'),
].map(s => s.trim()).filter(Boolean));

// what a skeleton may touch
const ALLOWED = [
  /^backend\/src\/Modules\/[^/]+\/[^/]+\.Core\/(Domain|DAL|ReadModels|Exceptions)\//,
  // command/query/result RECORDS are data, not behaviour (entity factories take them as parameters);
  // handlers and validators in the same folders remain forbidden (see FORBIDDEN)
  /^backend\/src\/Modules\/[^/]+\/[^/]+\.Core\/Features\/[^/]+\/[^/]*(Command|Query|Result|Dto)\.cs$/,
  /^backend\/src\/Modules\/[^/]+\/[^/]+\.Migrations\//,
  /^backend\/src\/Modules\/[^/]+\/[^/]+\.Core\/[^/]+\.csproj$/,
  /^backend\/src\/Shared\/ThingsBooksy\.Shared\.Abstractions\//,     // BE↔BE contracts (serial step) — see workflow.md §8a
  /^backend\/ThingsBooksy\.slnx$/,
  /^backend\/src\/Bootstrapper\//,                                    // module registration for a NEW module
  /^backend\/src\/Shared\/ThingsBooksy\.Shared\.IntegrationTests\/ThingsBooksyWebAppFactory\.cs$/,
  /^(runs|generated|specs|docs)\//,
  /^tools\/fleet\//,
];
// what is definitely behaviour
const FORBIDDEN = [
  [/\.Core\/Features\/.*(Handler|Validator|Service)\.cs$/, 'feature behaviour (handler/validator/service)'],
  [/\.Core\/Events\/Handlers\//, 'event handler'],
  [/\.Api\//, 'Api project (endpoints, requests)'],
  [/Handler\.cs$/, 'handler class'],
  [/Endpoints?\.cs$/, 'endpoint class'],
  [/^frontend\//, 'frontend'],
  [/\.IntegrationTests\//, 'tests (belong to test-designer, not the skeleton writer)'],
];

const violations = [], accepted = [];
for (const f of [...changed].sort()) {
  const p = f.replace(/\\/g, '/');
  const forb = FORBIDDEN.find(([re]) => re.test(p));
  if (forb) { violations.push({ file: p, reason: forb[1] }); continue; }
  if (ALLOWED.some(re => re.test(p))) accepted.push(p);
  else violations.push({ file: p, reason: 'outside skeleton scope' });
}
const report = { story, base: mergeBase, status: violations.length ? 'VIOLATIONS' : 'OK', accepted, violations };
const out = path.resolve(REPO, args.out || `runs/${story}/skeleton-check.json`);
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(report, null, 2) + '\n');
console.log(`skeleton-check: ${report.status} — ${accepted.length} skeleton files, ${violations.length} violation(s)`);
violations.forEach(v => console.log(`  ✗ ${v.file} — ${v.reason}`));
process.exit(violations.length ? 3 : 0);
