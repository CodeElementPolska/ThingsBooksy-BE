#!/usr/bin/env node
// S3b — contract-diff: compare the agreed contract (contract-next.json) with the swagger of the
// IMPLEMENTED backend (generated/swagger.base.json after re-export) and classify drift.
// Uses openapi-diff (breaking / non-breaking / unclassified) + our own path-level summary.
// Usage: node tools/fleet/contract-diff.js --story NNN-slug [--expected runs/NNN-slug/contract-next.json] [--actual generated/swagger.base.json] [--out runs/NNN-slug/contract-diff.json]
// Exit 0 = identical on paths/operations/responses · 3 = drift (gate fails) · 1 = error
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import openapiDiff from 'openapi-diff';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const story = args.story;
if (!story) { console.error('usage: contract-diff --story NNN-slug [--expected …] [--actual …] [--out …]'); process.exit(1); }
const expectedPath = path.resolve(REPO, args.expected || `runs/${story}/contract-next.json`);
const actualPath = path.resolve(REPO, args.actual || 'generated/swagger.base.json');
const outPath = path.resolve(REPO, args.out || `runs/${story}/contract-diff.json`);
for (const p of [expectedPath, actualPath]) if (!fs.existsSync(p)) { console.error(`contract-diff: missing ${path.relative(REPO, p)}`); process.exit(1); }

const load = p => { const j = JSON.parse(fs.readFileSync(p, 'utf8')); delete j['x-fleet']; return j; };
const expected = load(expectedPath), actual = load(actualPath);

// --- path/operation/response level summary (readable for the owner) --------------------
const ops = doc => { const s = new Set(); for (const [r, o] of Object.entries(doc.paths || {})) for (const m of Object.keys(o)) if (/^(get|post|put|patch|delete)$/.test(m)) s.add(`${m.toUpperCase()} ${r}`); return s; };
const E = ops(expected), A = ops(actual);
const missing = [...E].filter(x => !A.has(x));           // in contract, not implemented
const extra = [...A].filter(x => !E.has(x));             // implemented, not in contract → UNSPECIFIED surface
const responseDrift = [];
for (const op of [...E].filter(x => A.has(x))) {
  const [m, r] = op.split(' '); const e = expected.paths[r][m.toLowerCase()], a = actual.paths[r][m.toLowerCase()];
  const er = Object.keys(e.responses || {}).sort().join(','), ar = Object.keys(a.responses || {}).sort().join(',');
  if (er !== ar) responseDrift.push({ op, expected: er, actual: ar });
}

// --- semantic diff via openapi-diff -----------------------------------------------------
let semantic = { breaking: [], nonBreaking: [], unclassified: [] };
try {
  const r = await openapiDiff.diffSpecs({
    sourceSpec: { content: JSON.stringify(expected), location: 'contract-next.json', format: 'openapi3' },
    destinationSpec: { content: JSON.stringify(actual), location: 'swagger.base.json', format: 'openapi3' },
  });
  const slim = d => ({ code: d.code, action: d.action, entity: d.entity, source: d.sourceSpecEntityDetails?.map(x => x.location), destination: d.destinationSpecEntityDetails?.map(x => x.location) });
  semantic = { breaking: (r.breakingDifferences || []).map(slim), nonBreaking: (r.nonBreakingDifferences || []).map(slim), unclassified: (r.unclassifiedDifferences || []).map(slim) };
} catch (e) { semantic.error = String(e.message || e); }

const report = {
  story,
  status: (missing.length || extra.length || responseDrift.length || semantic.breaking.length) ? 'DRIFT' : 'CLEAR',
  missing_in_implementation: missing,
  unspecified_in_contract: extra,
  response_code_drift: responseDrift,
  semantic,
};
fs.mkdirSync(path.dirname(outPath), { recursive: true });
fs.writeFileSync(outPath, JSON.stringify(report, null, 2) + '\n');
console.log(`contract-diff: ${report.status} — missing ${missing.length}, unspecified ${extra.length}, response drift ${responseDrift.length}, breaking ${semantic.breaking.length} → ${path.relative(REPO, outPath)}`);
process.exit(report.status === 'CLEAR' ? 0 : 3);
