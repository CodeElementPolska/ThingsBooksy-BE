#!/usr/bin/env node
// S9b — migration-check: the deterministic trigger for G2b (D-1: data & migrations are always the owner's decision).
// Classifies every statement of Up()/Down() in the story's NEW or CHANGED EF Core migrations (since the commit in
// runs/<story>/baseline.json) into SAFE / REVIEW / DESTRUCTIVE. Fail-safe by construction: an unknown operation, a
// statement that is not a migrationBuilder call, an unparsable file or method body, and an unbalanced statement are
// all REVIEW; raw Sql(...) in Up is DESTRUCTIVE. The script never decides that a migration is harmless on data it
// cannot see — it shows the owner the exact statements instead, and the owner answers G2b.
// REVIEW or DESTRUCTIVE (or be-writer's own `schema_changes: DESTRUCTIVE` from C3a) ⇒ g2b_required: the script opens
// G2b in the journal (GATE_OPEN, idempotent per migration_sha). The conductor asks the owner with AskUserQuestion
// (a chat answer is NOT journaled) and closes the gate with `decide.js --gate G2b`, which binds the answer to the same
// migration_sha — a regenerated migration is a new question. gate.js refuses a migration without a fresh report.
// Why a script and not the planned `migration-reviewer` agent: the check must run right after
// `dotnet ef migrations add` (before C2), and a grep over operation names is not enough — 015's only migration has a
// DropIndex (harmless) while its real risk sat in Down() (an unfiltered unique index). Hence buckets with context.
// Known, documented simplification: a Down() that drops an object Up() created is SAFE even though a rollback would
// lose rows written after deployment — rollbacks are the owner's manual, local operation (constitution VI).
// Usage: node tools/fleet/migration-check.js --story NNN-slug [--commit <sha>] [--dry-run] [--print]
// Exit 0 NO_MIGRATION/CLEAR · 5 G2B_REQUIRED (REVIEW or DESTRUCTIVE) · 1 usage / no baseline / MODEL_CHANGED_NO_MIGRATION
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { sha256File, HASH_VERSION } from './hash.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');

// --- which files are migrations --------------------------------------------------------------------------
export const MIGRATION_RE = /^backend\/src\/Modules\/[^/]+\/ThingsBooksy\.Modules\.[^/]+\.Migrations\/Migrations\/[^/]+\.cs$/;
const fwd = p => String(p).replace(/\\/g, '/');
export const isMigrationFile = rel => MIGRATION_RE.test(fwd(rel)) && !/\.Designer\.cs$/.test(fwd(rel)) && !/ModelSnapshot\.cs$/.test(fwd(rel));
export const isSnapshotFile = rel => /ThingsBooksy\.Modules\.[^/]+\.Migrations\/Migrations\/[^/]*ModelSnapshot\.cs$/.test(fwd(rel));

// --- scrub: same length, same newlines; comments and preprocessor lines → spaces, string/char contents → 'x' ----
// (delimiters stay) so that parentheses, braces, semicolons and "migrationBuilder." inside strings or comments never
// count. Interpolated strings ($"…{expr}…") are scanned recursively: a quote inside a hole does not end the string.
export function scrub(src) {
  const out = src.split('');
  const n = src.length;
  const fill = (a, b, ch) => { for (let k = a; k < b && k < n; k++) if (src[k] !== '\n' && src[k] !== '\r') out[k] = ch; };
  // returns the index just past the string literal that starts at p (p points at the first '$' or '@' or '"')
  function scanString(p) {
    let q = p; let verbatim = false, interp = false;
    while (src[q] === '$' || src[q] === '@') { if (src[q] === '@') verbatim = true; else interp = true; q++; }
    if (src[q] !== '"') return -1;
    if (src[q + 1] === '"' && src[q + 2] === '"') { // raw string literal """…""" (≥ 3 quotes)
      let r = q; while (src[r] === '"') r++; const closer = '"'.repeat(r - q);
      let j = src.indexOf(closer, r); j = j < 0 ? n : j; fill(r, j, 'x'); return Math.min(j + (r - q), n);
    }
    let j = q + 1;
    while (j < n) {
      const c = src[j];
      if (c === '"') { if (verbatim && src[j + 1] === '"') { j += 2; continue; } break; }
      if (!verbatim && c === '\\') { j += 2; continue; }
      if (interp && c === '{') {
        if (src[j + 1] === '{') { j += 2; continue; }
        // interpolation hole: skip nested literals, count braces; everything inside is masked too
        let depth = 0; let k = j;
        while (k < n) {
          const d = src[k];
          if (d === '"' || ((d === '$' || d === '@') && /["$@]/.test(src[k + 1] || ''))) { const e = scanString(k); if (e > k) { k = e; continue; } }
          if (d === "'") { let e = k + 1; if (src[e] === '\\') e += 2; else e += 1; if (src[e] === "'") e++; k = e; continue; }
          if (d === '{') depth++; else if (d === '}') { depth--; if (depth === 0) { k++; break; } }
          k++;
        }
        fill(j, k, 'x'); j = k; continue;
      }
      j++;
    }
    fill(q + 1, Math.min(j, n), 'x');
    return Math.min(j + 1, n);
  }
  let i = 0;
  while (i < n) {
    const c = src[i], d = src[i + 1];
    if (c === '/' && d === '/') { let j = i; while (j < n && src[j] !== '\n') j++; fill(i, j, ' '); i = j; continue; }
    if (c === '/' && d === '*') { let j = src.indexOf('*/', i + 2); j = j < 0 ? n : j + 2; fill(i, j, ' '); i = j; continue; }
    if (c === '#' && /^[ \t]*$/.test(src.slice(src.lastIndexOf('\n', i - 1) + 1, i))) { let j = i; while (j < n && src[j] !== '\n') j++; fill(i, j, ' '); i = j; continue; }
    if (c === "'") { let j = i + 1; if (src[j] === '\\') j += 2; else j += 1; if (src[j] === "'") j++; fill(i + 1, j - 1, 'x'); i = j; continue; }
    if (c === '"' || ((c === '$' || c === '@') && /["$@]/.test(d || ''))) { const e = scanString(i); if (e > i) { i = e; continue; } }
    i++;
  }
  return out.join('');
}

// --- method bodies and statements (on the scrubbed text) -------------------------------------------------
// Returns { start, end, expression } or null when the method is missing or has no recognisable body.
function methodBody(scr, name) {
  const m = new RegExp(`(?:\\b(?:protected|override|sealed|public|internal|private|virtual)\\s+)+void\\s+${name}\\s*\\(`).exec(scr);
  if (!m) return null;
  let k = m.index + m[0].length - 1; let depth = 0;
  for (; k < scr.length; k++) { if (scr[k] === '(') depth++; else if (scr[k] === ')') { depth--; if (depth === 0) break; } }
  if (depth !== 0) return null;
  let p = k + 1; while (p < scr.length && /\s/.test(scr[p])) p++;
  if (scr[p] === '{') {
    depth = 0;
    for (let q = p; q < scr.length; q++) { if (scr[q] === '{') depth++; else if (scr[q] === '}') { depth--; if (depth === 0) return { start: p + 1, end: q }; } }
    return null;
  }
  if (scr[p] === '=' && scr[p + 1] === '>') { // expression-bodied: one statement up to the top-level ';'
    let q = p + 2; depth = 0;
    for (; q < scr.length; q++) { const c = scr[q]; if (c === '(' || c === '{' || c === '[') depth++; else if (c === ')' || c === '}' || c === ']') depth--; else if (c === ';' && depth === 0) break; }
    return { start: p + 2, end: q, expression: true };
  }
  return null;
}
function statements(scr, start, end) {
  const out = []; let depth = 0; let s = start;
  for (let k = start; k < end; k++) {
    const c = scr[k];
    if (c === '(' || c === '{' || c === '[') depth++;
    else if (c === ')' || c === '}' || c === ']') depth--;
    else if (c === ';' && depth === 0) { out.push({ start: s, end: k }); s = k + 1; }
  }
  if (scr.slice(s, end).trim()) out.push({ start: s, end, unbalanced: depth !== 0 });
  return out;
}
const OP_RE = /^\s*migrationBuilder\s*\.\s*(\w+)\s*(?:<[^>()]*>)?\s*\(/;
const KEY_ARG_RE = /^\s*(filter|unique|nullable|defaultValue|defaultValueSql|oldClrType|type|principalTable|principalColumn|principalColumns)\s*:/;
function snippetOf(text) {
  const lines = text.split('\n').map(l => l.trim()).filter(Boolean);
  if (lines.length <= 12) return lines.join('\n');
  const head = lines.slice(0, 12); const rest = lines.slice(12).filter(l => KEY_ARG_RE.test(l));
  return [...head, '…', ...rest].join('\n');
}
function parseStatement(src, scr, st) {
  const sText = scr.slice(st.start, st.end); if (!sText.trim()) return null;
  const lead = sText.length - sText.trimStart().length;
  const line = src.slice(0, st.start + lead).split('\n').length;
  const text = src.slice(st.start, st.end).trim();
  const snippet = snippetOf(text);
  if (st.unbalanced) return { op: '<unparsed>', line, args: {}, chain: [], snippet, parseNote: 'unbalanced brackets — the parser could not split this statement' };
  const m = OP_RE.exec(sText);
  if (!m) return { op: '<statement>', line, args: {}, chain: [], snippet };
  const open = st.start + m[0].length - 1;
  let depth = 0, close = st.end;
  for (let k = open; k < st.end; k++) { if (scr[k] === '(') depth++; else if (scr[k] === ')') { depth--; if (depth === 0) { close = k; break; } } }
  const args = {}; const aScr = scr.slice(open + 1, close); const aOrig = src.slice(open + 1, close);
  let d = 0, a = 0;
  for (let k = 0; k <= aScr.length; k++) {
    const c = aScr[k];
    if (k === aScr.length || (c === ',' && d === 0)) { const nm = /^\s*(\w+)\s*:/.exec(aScr.slice(a, k)); if (nm) args[nm[1]] = aOrig.slice(a, k).slice(nm[0].length).trim(); a = k + 1; }
    else if (c === '(' || c === '{' || c === '[') d++; else if (c === ')' || c === '}' || c === ']') d--;
  }
  // method chain after the call: every .Annotation/.OldAnnotation counts; a non-literal name is kept as null
  const chainScr = scr.slice(close + 1, st.end); const chainOrig = src.slice(close + 1, st.end);
  const chain = [...chainScr.matchAll(/\.\s*(Annotation|OldAnnotation)\s*\(/g)].map(c => { const lit = /^\s*"([^"]*)"/.exec(chainOrig.slice(c.index + c[0].length)); return { kind: c[1], name: lit ? lit[1] : null }; });
  return { op: m[1], line, args, chain, snippet };
}

// --- buckets ------------------------------------------------------------------------------------------------
export const SAFE = 'SAFE', REVIEW = 'REVIEW', DESTRUCTIVE = 'DESTRUCTIVE';
const RANK = { SAFE: 0, REVIEW: 1, DESTRUCTIVE: 2 };
const DESTRUCTIVE_OPS = new Set(['DropTable', 'DropColumn', 'DropSchema', 'DropSequence', 'DeleteData', 'UpdateData', 'Sql']);
const REVIEW_OPS = new Set(['AlterColumn', 'AlterSequence', 'AlterTable', 'RestartSequence', 'RenameTable', 'RenameColumn', 'RenameSequence', 'DropForeignKey', 'DropPrimaryKey', 'DropCheckConstraint', 'DropUniqueConstraint']);
const SAFE_OPS = new Set(['EnsureSchema', 'CreateTable', 'CreateSequence', 'InsertData', 'DropIndex', 'RenameIndex']);
const VALIDATED_ON_EXISTING_ROWS = new Set(['AddUniqueConstraint', 'AddPrimaryKey', 'AddForeignKey', 'AddCheckConstraint']);
const unq = v => String(v ?? '').replace(/^"(.*)"$/s, '$1');
const tkey = (schema, table) => `${unq(schema)}.${unq(table)}`;
const isUnique = o => /^true$/.test(o.args.unique || '');
const notNull = o => /^false$/.test(o.args.nullable || '');
const hasDefault = o => ('defaultValue' in o.args && !/^null$/.test(o.args.defaultValue)) || 'defaultValueSql' in o.args;
const ZERO_DEFAULT = /^(new\s+Guid\s*\(\s*"0{8}-0{4}-0{4}-0{4}-0{12}"\s*\)|Guid\.Empty|default(\s*\(\s*\w+\s*\))?|""|0|0L|0m|0d|0f|false)$/;
const cols = o => { const out = []; if (o.args.column) out.push(unq(o.args.column)); for (const c of String(o.args.columns || '').matchAll(/"([^"]+)"/g)) out.push(c[1]); return out; };
const alterDatabaseBucket = o => (o.chain.length > 0 && o.chain.every(c => c.kind === 'Annotation' && c.name && /^Npgsql:(PostgresExtension|CollationDefinition|Enum):/.test(c.name))) ? { bucket: SAFE } : { bucket: REVIEW, note: 'database-level change (extension/enum/collation removal or change, or a non-literal annotation)' };
const unparsed = o => o.op === '<unparsed>' ? { bucket: REVIEW, note: o.parseNote } : o.op === '<statement>' ? { bucket: REVIEW, note: 'statement is not a migrationBuilder call' } : null;

function bucketUp(o, created, fkCols) {
  const u = unparsed(o); if (u) return u;
  const t = tkey(o.args.schema, o.args.table); const isNew = created.has(t);
  if (DESTRUCTIVE_OPS.has(o.op)) return { bucket: DESTRUCTIVE, note: o.op === 'Sql' ? 'raw SQL is never analysed — always the owner\'s call' : undefined };
  if (SAFE_OPS.has(o.op)) return { bucket: SAFE };
  if (o.op === 'CreateIndex') return isUnique(o) && !isNew ? { bucket: REVIEW, note: 'unique index on an existing table fails on duplicates (a filtered index replacing a stricter one is safe — the script cannot see the old index)' } : { bucket: SAFE };
  if (VALIDATED_ON_EXISTING_ROWS.has(o.op)) return isNew ? { bucket: SAFE } : { bucket: REVIEW, note: `${o.op} on an existing table is validated against existing rows (orphans, duplicates, NULLs)` };
  if (o.op === 'AddColumn') {
    if (isNew) return { bucket: SAFE };
    if (notNull(o) && !hasDefault(o)) return { bucket: REVIEW, note: 'NOT NULL column without a default fails on existing rows' };
    if (notNull(o) && ZERO_DEFAULT.test(o.args.defaultValue || '') && fkCols.has(`${t}:${unq(o.args.name)}`)) return { bucket: REVIEW, note: 'NOT NULL column with a zero default that is then a foreign key — the FK fails on existing rows unless those rows are removed first (an explicit owner decision)' };
    return { bucket: SAFE };
  }
  if (o.op === 'AlterDatabase') return alterDatabaseBucket(o);
  if (REVIEW_OPS.has(o.op)) return { bucket: REVIEW };
  return { bucket: REVIEW, note: `unknown operation ${o.op} (fail-safe)` };
}
function classifyUp(ops) {
  const created = new Set(ops.filter(o => o.op === 'CreateTable').map(o => tkey(o.args.schema, o.args.name)));
  const fkCols = new Set(ops.filter(o => o.op === 'AddForeignKey').flatMap(o => cols(o).map(c => `${tkey(o.args.schema, o.args.table)}:${c}`)));
  return ops.map(o => ({ ...o, ...bucketUp(o, created, fkCols) }));
}
const ADDED_BY = { DropForeignKey: 'AddForeignKey', DropPrimaryKey: 'AddPrimaryKey', DropUniqueConstraint: 'AddUniqueConstraint', DropCheckConstraint: 'AddCheckConstraint', DropSequence: 'CreateSequence', DropSchema: 'EnsureSchema' };
function classifyDown(ops, up) {
  const upCreated = new Set(up.filter(o => o.op === 'CreateTable').map(o => tkey(o.args.schema, o.args.name)));
  const upAddedCols = new Set(up.filter(o => o.op === 'AddColumn').map(o => `${tkey(o.args.schema, o.args.table)}:${unq(o.args.name)}`));
  const upAdded = new Set(up.map(o => `${o.op}:${tkey(o.args.schema, o.args.table || o.args.name)}:${unq(o.args.name)}`));
  const reversesRename = o => up.some(r => r.op === o.op && unq(r.args.newName) === unq(o.args.name) && unq(r.args.name) === unq(o.args.newName));
  const downCreated = new Set(ops.filter(o => o.op === 'CreateTable').map(o => tkey(o.args.schema, o.args.name)));
  return ops.map(o => {
    const u = unparsed(o); if (u) return { ...o, ...u };
    const t = tkey(o.args.schema, o.args.table); let r;
    if (o.op === 'DropTable') r = upCreated.has(tkey(o.args.schema, o.args.name)) ? { bucket: SAFE } : { bucket: REVIEW, note: 'rollback drops a pre-existing table' };
    else if (o.op === 'DropColumn') r = upAddedCols.has(`${t}:${unq(o.args.name)}`) ? { bucket: SAFE } : { bucket: REVIEW, note: 'rollback drops a pre-existing column' };
    else if (o.op === 'DropIndex') r = { bucket: SAFE };
    else if (ADDED_BY[o.op]) r = upAdded.has(`${ADDED_BY[o.op]}:${tkey(o.args.schema, o.args.table || o.args.name)}:${unq(o.args.name)}`) ? { bucket: SAFE } : { bucket: REVIEW, note: 'rollback drops a pre-existing object' };
    else if (/^Rename/.test(o.op)) r = reversesRename(o) ? { bucket: SAFE } : { bucket: REVIEW, note: 'rollback renames something Up did not rename the other way' };
    else if (o.op === 'CreateIndex') r = isUnique(o) && !downCreated.has(t) ? { bucket: REVIEW, note: 'rollback recreates a unique index on existing data (an unfiltered index can fail on rows a filtered one ignored)' } : { bucket: SAFE };
    else if (VALIDATED_ON_EXISTING_ROWS.has(o.op)) r = downCreated.has(t) ? { bucket: SAFE } : { bucket: REVIEW, note: 'rollback constraint validated against existing rows' };
    else if (o.op === 'AddColumn') r = notNull(o) && !hasDefault(o) && !downCreated.has(t) ? { bucket: REVIEW, note: 'rollback adds a NOT NULL column without a default' } : { bucket: SAFE };
    else if (o.op === 'AlterDatabase') r = alterDatabaseBucket(o);
    else if (/^(AlterColumn|Sql|DeleteData|UpdateData|AlterTable|AlterSequence|RestartSequence)$/.test(o.op)) r = { bucket: REVIEW, note: 'rollback may fail or lose data' };
    else if (/^(CreateTable|CreateSequence|EnsureSchema|InsertData)$/.test(o.op)) r = { bucket: SAFE };
    else r = { bucket: REVIEW, note: `unknown operation ${o.op} (fail-safe)` };
    return { ...o, ...r };
  });
}
const maxBucket = arr => arr.reduce((m, o) => RANK[o.bucket] > RANK[m] ? o.bucket : m, SAFE);

/** classify(source) → { up, down, verdict: CLEAR|REVIEW|DESTRUCTIVE }; Down never raises the verdict above REVIEW.
 *  A missing/unrecognisable Up() or Down() body, or an unreadable file, yields a single `<unparsed>` REVIEW op. */
export function classify(source) {
  if (typeof source !== 'string' || source.includes('\u0000') || !/Migration/.test(source)) {
    const o = { op: '<unparsed>', line: 1, bucket: REVIEW, note: 'file is not readable as a C# migration (binary/UTF-16/empty?)', snippet: '' };
    return { up: [o], down: [], verdict: REVIEW };
  }
  const scr = scrub(source);
  const body = name => {
    const b = methodBody(scr, name);
    if (!b) return [{ op: '<unparsed>', line: 1, args: {}, chain: [], snippet: '', parseNote: `${name}() body not found — unusual method shape, review by hand` }];
    return statements(scr, b.start, b.end).map(st => parseStatement(source, scr, st)).filter(Boolean);
  };
  const up = classifyUp(body('Up'));
  const down = classifyDown(body('Down'), up);
  const u = maxBucket(up); const d0 = maxBucket(down); const d = d0 === DESTRUCTIVE ? REVIEW : d0;
  const top = RANK[u] >= RANK[d] ? u : d;
  return { up, down, verdict: top === SAFE ? 'CLEAR' : top };
}

// --- changed migrations since a commit (tracked diff + untracked) --------------------------------------------
export function collectMigrationChanges(repo, sha) {
  const git = a => spawnSync('git', a, { cwd: repo, encoding: 'utf8' });
  const ok = git(['rev-parse', '--verify', '--quiet', `${sha}^{commit}`]);
  if (ok.status !== 0) throw new Error(`commit "${sha}" is not in this repository's history`);
  const ns = git(['diff', '--name-status', sha, '--']);
  if (ns.status !== 0) throw new Error(`git diff --name-status ${sha}: ${ns.stderr.trim()}`);
  const all = [];
  for (const line of ns.stdout.split('\n').filter(Boolean)) {
    const parts = line.split('\t'); const st = parts[0][0]; const p = fwd(st === 'R' || st === 'C' ? parts[2] : parts[1]);
    all.push({ path: p, change: st === 'A' || st === 'C' ? 'added' : st === 'D' ? 'deleted' : st === 'R' ? 'renamed' : 'modified' });
    if (st === 'R' && isMigrationFile(fwd(parts[1])) && !isMigrationFile(p)) all.push({ path: fwd(parts[1]), change: 'deleted' });
  }
  const un = git(['ls-files', '--others', '--exclude-standard']);
  if (un.status !== 0) throw new Error(`git ls-files: ${un.stderr.trim()}`);
  for (const p of (un.stdout || '').split('\n').filter(Boolean)) all.push({ path: fwd(p), change: 'added' });
  return { migrations: all.filter(f => isMigrationFile(f.path)), snapshots: all.filter(f => isSnapshotFile(f.path)) };
}
/** migration_sha: identifies the CONTENT of the story's migrations (sorted path:sha256, deleted = null) — the key that
 *  binds a G2b answer to one migration; gate.js recomputes it from the working tree. */
export function migrationSha(entries) { return crypto.createHash('sha256').update(entries.map(f => `${f.path}:${f.sha256}`).sort().join('\n')).digest('hex'); }
export function currentMigrationSha(repo, migrations) { return migrationSha(migrations.map(m => ({ path: m.path, sha256: m.change === 'deleted' ? null : sha256File(path.join(repo, m.path)) }))); }

const strip = o => ({ op: o.op, bucket: o.bucket, line: o.line, ...(o.note ? { note: o.note } : {}), snippet: o.snippet });
const readJsonl = p => fs.existsSync(p) ? fs.readFileSync(p, 'utf8').split('\n').filter(Boolean).map(l => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean) : [];

// --- CLI -------------------------------------------------------------------------------------------------------
function main() {
  const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
  const story = args.story;
  const usage = () => { console.error('usage: migration-check --story NNN-slug [--commit <sha>] [--dry-run] [--print]'); process.exit(1); };
  if (!story || !/^\d{3}-[a-z0-9-]+$/.test(String(story)) || args.commit === true) usage();
  const runDir = path.join(REPO, 'runs', story);
  let commit = args.commit;
  if (!commit) {
    const bl = path.join(runDir, 'baseline.json');
    if (!fs.existsSync(bl)) { console.error(`migration-check: no ${path.relative(REPO, bl)} — run baseline.js first (no silent fallback to merge-base: 015 got 39 false skeleton violations that way)`); process.exit(1); }
    try { commit = JSON.parse(fs.readFileSync(bl, 'utf8')).commit; } catch { commit = null; }
    if (!commit) { console.error('migration-check: baseline.json has no "commit" — rerun baseline.js'); process.exit(1); }
  }
  let changes;
  try { changes = collectMigrationChanges(REPO, commit); } catch (e) { console.error(`migration-check: ${e.message}`); process.exit(1); }
  commit = spawnSync("git", ["rev-parse", commit], { cwd: REPO, encoding: "utf8" }).stdout.trim(); // the report records the resolved sha (gate compares it with baseline.commit)
  const { migrations, snapshots } = changes;
  const files = [];
  for (const m of migrations) {
    if (m.change === 'deleted') { files.push({ path: m.path, change: m.change, sha256: null, up: [], down: [], verdict: REVIEW, note: 'migration deleted since baseline' }); continue; }
    const abs = path.join(REPO, m.path);
    let source; try { source = fs.readFileSync(abs, 'utf8'); } catch (e) { source = null; }
    const { up, down, verdict } = classify(source);
    const f = { path: m.path, change: m.change, sha256: source === null ? null : sha256File(abs), up: up.map(strip), down: down.map(strip), verdict };
    if (m.change === 'modified') { f.note = 'edited an already-committed migration (constitution VI: one regenerated migration per story)'; if (f.verdict === 'CLEAR') f.verdict = REVIEW; }
    files.push(f);
  }
  const verdictRank = v => v === 'CLEAR' ? 0 : RANK[v] ?? 1;
  const verdict = files.length ? files.reduce((m, f) => verdictRank(f.verdict) > verdictRank(m) ? f.verdict : m, 'CLEAR') : (snapshots.length ? 'MODEL_CHANGED_NO_MIGRATION' : 'NO_MIGRATION');
  const sha = migrationSha(files);

  // be-writer's C3a self-assessment: a FORECAST made before the migration file existed — compared, never trusted
  const implDir = path.join(runDir, 'impl');
  const claims = fs.existsSync(implDir) ? fs.readdirSync(implDir).filter(d => /^be-writer\.C3a/.test(d)).map(d => { try { return { path: `impl/${d}/result.json`, schema_changes: JSON.parse(fs.readFileSync(path.join(implDir, d, 'result.json'), 'utf8')).schema_changes ?? null }; } catch { return null; } }).filter(Boolean) : [];
  const claimRank = { NONE: 0, ADDITIVE: 1, DESTRUCTIVE: 2 };
  const claim = claims.reduce((m, c) => (claimRank[c.schema_changes] ?? -1) > (claimRank[m] ?? -1) ? c.schema_changes : m, null);
  const claimMismatch = claim !== null && ((verdict === 'NO_MIGRATION' && claim !== 'NONE') || ((verdict === REVIEW || verdict === DESTRUCTIVE) && claim === 'NONE') || (verdict === DESTRUCTIVE && claim !== 'DESTRUCTIVE'));
  const g2bRequired = verdict === REVIEW || verdict === DESTRUCTIVE || claim === 'DESTRUCTIVE';

  const report = { story, commit, files, verdict, migration_sha: sha, g2b_required: g2bRequired, be_writer_claims: claims, be_writer_claim: claim, be_writer_claim_note: 'forecast made by be-writer in C3a before the migration file existed (it cannot see Down() risks)', claim_mismatch: claimMismatch, computed_at: new Date().toISOString(), hash_version: HASH_VERSION };
  if (!args['dry-run']) {
    const jp = path.join(runDir, 'journal.jsonl');
    const journal = readJsonl(jp); // read BEFORE writing the report, so a broken journal cannot leave a report without its GATE_OPEN
    fs.mkdirSync(runDir, { recursive: true });
    fs.writeFileSync(path.join(runDir, 'migration-check.json'), JSON.stringify(report, null, 2) + '\n');
    if (g2bRequired) {
      // open G2b in the journal, idempotently per migration content (status.js shows WAITING_OWNER; closer counts it)
      const lastOpen = [...journal].reverse().find(e => e.event === 'GATE_OPEN' && e.phase === 'G2b');
      if (!lastOpen || lastOpen.migration_sha !== sha) fs.appendFileSync(jp, JSON.stringify({ at: report.computed_at, event: 'GATE_OPEN', phase: 'G2b', artifact: 'migration-check.json', migration_sha: sha }) + '\n');
    }
  }
  const say = args.print ? console.error : console.log; // --print keeps stdout for the JSON; the human summary goes to stderr
  if (args.print) console.log(JSON.stringify(report, null, 2));
  say(`migration-check: ${verdict}${g2bRequired ? ' → G2b REQUIRED' : ''} (${files.length} migration file(s) since ${String(commit).slice(0, 8)}; migration_sha ${sha.slice(0, 8)})${claim ? `; be-writer forecast ${claim}${claimMismatch ? ' — MISMATCH' : ''}` : ''}`);
  for (const f of files) {
    say(`  ${f.path} [${f.change}] → ${f.verdict}${f.note ? ' — ' + f.note : ''}`);
    for (const side of ['up', 'down']) for (const o of f[side]) if (o.bucket !== SAFE) say(`    ${side.toUpperCase()} :${o.line} ${o.op} [${o.bucket}]${o.note ? ' — ' + o.note : ''}\n      ${o.snippet.replace(/\n/g, '\n      ')}`);
  }
  if (verdict === 'MODEL_CHANGED_NO_MIGRATION') say(`  model snapshot changed (${snapshots.map(s => path.basename(s.path)).join(', ')}) but no migration — run dotnet ef migrations add`);
  if (g2bRequired) say(`  next: AskUserQuestion with the statements above — the question and the options must name migration ${sha.slice(0, 8)}\n        (options: "Akceptuję migrację ${sha.slice(0, 8)}" / "Odrzucam migrację ${sha.slice(0, 8)}"; a chat answer is not journaled), then\n        node tools/fleet/decide.js --story ${story} --gate G2b --status PASSED|REJECTED --answer-ref latest`);
  process.exit(verdict === 'MODEL_CHANGED_NO_MIGRATION' ? 1 : g2bRequired ? 5 : 0);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
