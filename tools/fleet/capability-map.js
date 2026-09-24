#!/usr/bin/env node
// S2 — capability-map: what the application can do TODAY, generated, no LLM.
// Inputs : generated/swagger.base.json, generated/modules.json (from the SwaggerExport tooling test),
//          frontend/src/app/**/*.routes.ts (Angular routes, parsed textually)
// Output : generated/capability-map.json — the ONLY view of the app the business session may read.
// Usage  : node tools/fleet/capability-map.js [--refresh]   (--refresh re-runs the tooling test first)
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const GEN = path.join(REPO, 'generated');
const SWAGGER = path.join(GEN, 'swagger.base.json');
const MODULES = path.join(GEN, 'modules.json');
const OUT = path.join(GEN, 'capability-map.json');

if (process.argv.includes('--refresh')) {
  const r = spawnSync('dotnet', ['test', 'backend/src/Shared/ThingsBooksy.Shared.IntegrationTests', '--filter', 'Category=Tooling', '--nologo', '-v', 'q'],
    { cwd: REPO, stdio: 'inherit', shell: true });
  if (r.status !== 0) { console.error('capability-map: swagger export failed'); process.exit(r.status || 1); }
}
for (const f of [SWAGGER, MODULES]) {
  if (!fs.existsSync(f)) { console.error(`capability-map: missing ${path.relative(REPO, f)} — run with --refresh (Docker required)`); process.exit(1); }
}

const sha = s => crypto.createHash('sha256').update(s).digest('hex');
const swaggerRaw = fs.readFileSync(SWAGGER, 'utf8');
const swagger = JSON.parse(swaggerRaw);
const modules = JSON.parse(fs.readFileSync(MODULES, 'utf8'));

// --- endpoints ---------------------------------------------------------------
const endpoints = [];
for (const [route, ops] of Object.entries(swagger.paths || {})) {
  for (const [method, op] of Object.entries(ops)) {
    if (!['get', 'post', 'put', 'patch', 'delete'].includes(method)) continue;
    const modulePrefix = route.split('/').filter(Boolean)[0] || '';
    endpoints.push({
      method: method.toUpperCase(),
      route,
      module: modulePrefix,
      operationId: op.operationId || null,
      tags: op.tags || [],
      summary: op.summary || null,
      requiresAuth: Array.isArray(op.security) ? op.security.length > 0 : Array.isArray(swagger.security) && swagger.security.length > 0,
      responses: Object.keys(op.responses || {}),
      requestSchema: op.requestBody?.content?.['application/json']?.schema?.$ref?.split('/').pop() || null,
    });
  }
}

// --- Angular routes (textual parse; good enough for a capability list) ---------
function walk(dir, acc = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== 'node_modules') walk(p, acc); }
    else if (e.name.endsWith('.routes.ts')) acc.push(p);
  }
  return acc;
}
const feRoot = path.join(REPO, 'frontend', 'src', 'app');
const screens = [];
if (fs.existsSync(feRoot)) {
  for (const file of walk(feRoot)) {
    const src = fs.readFileSync(file, 'utf8');
    const feature = path.basename(file).replace('.routes.ts', '');
    const re = /path:\s*['"`]([^'"`]*)['"`][\s\S]*?(?:loadComponent|component|loadChildren)\s*:\s*([^,\n}]+)/g;
    let m;
    while ((m = re.exec(src))) {
      const target = m[2].trim();
      const comp = (target.match(/\.then\(\s*\(?\w*\)?\s*=>\s*\w*\.?(\w+)\)/) || target.match(/(\w+Component)/) || [null, target])[1];
      const guards = (src.slice(m.index, m.index + 400).match(/canActivate:\s*\[([^\]]*)\]/) || [null, ''])[1].split(',').map(s => s.trim()).filter(Boolean);
      screens.push({ feature, path: m[1], component: comp, guards, file: path.relative(REPO, file).replace(/\\/g, '/') });
    }
  }
}

// --- assemble ------------------------------------------------------------------
const map = {
  generated_at_commit: (spawnSync('git', ['rev-parse', '--short', 'HEAD'], { cwd: REPO, encoding: 'utf8' }).stdout || '').trim(),
  swagger_sha256: sha(swaggerRaw),
  modules: Array.isArray(modules) ? modules.map(m => (typeof m === 'string' ? { name: m } : m)) : modules,
  endpoints_by_module: Object.fromEntries(
    [...new Set(endpoints.map(e => e.module))].sort().map(mod => [mod, endpoints.filter(e => e.module === mod).map(({ module, ...rest }) => rest)])
  ),
  screens_by_feature: Object.fromEntries(
    [...new Set(screens.map(s => s.feature))].sort().map(f => [f, screens.filter(s => s.feature === f).map(({ feature, ...rest }) => rest)])
  ),
  counts: { endpoints: endpoints.length, screens: screens.length, modules: Array.isArray(modules) ? modules.length : 0 },
};
fs.mkdirSync(GEN, { recursive: true });
const out = JSON.stringify(map, null, 2) + '\n';
fs.writeFileSync(OUT, out);
console.log(`capability-map: ${map.counts.endpoints} endpoints, ${map.counts.screens} screens, ${map.counts.modules} modules → ${path.relative(REPO, OUT)} (sha256 ${sha(out).slice(0, 12)}…)`);
