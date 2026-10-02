// Tests for migration-check.js: scrub, classify (buckets with context, fail-safe parsing), golden verdicts on every
// migration in the repo, 016-style fixtures, collectMigrationChanges on a throwaway git repo, CLI paths on a
// synthetic story dir (cleaned up). Run: node tools/fleet/migration-check.test.js (no dependencies)
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { scrub, classify, isMigrationFile, isSnapshotFile, collectMigrationChanges, migrationSha } from './migration-check.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const SCRIPT = path.join(HERE, 'migration-check.js');
let bad = 0, total = 0;
const check = (name, cond, detail = '') => { total++; if (!cond) bad++; console.log(`${cond ? 'OK  ' : 'FAIL'} ${name}${!cond && detail ? ' :: ' + detail : ''}`); };
const mig = (up, down = '') => `using Microsoft.EntityFrameworkCore.Migrations;\nnamespace X { public partial class M : Migration {\n protected override void Up(MigrationBuilder migrationBuilder) {\n${up}\n }\n protected override void Down(MigrationBuilder migrationBuilder) {\n${down}\n }\n} }`;
const ops = r => r.up.map(o => `${o.op}:${o.bucket}`);
const downOps = r => r.down.map(o => `${o.op}:${o.bucket}`);

// 1. scrub
{
  const s1 = 'x; // migrationBuilder.DropTable(\nmigrationBuilder.CreateTable(name: "t");';
  check('scrub keeps length and newlines', scrub(s1).length === s1.length && scrub(s1).split('\n').length === 2);
  const r1 = classify(mig(s1));
  check('comment at end of line is not an operation', !r1.up.some(o => o.op === 'DropTable'), ops(r1).join(','));
  const r2 = classify(mig(`migrationBuilder.Sql(@"UPDATE t SET p = 'C:\\x' WHERE n = ""q"" AND (a) = (b)");\nmigrationBuilder.Sql("""\n SELECT ("Col") FROM {t} WHERE x = '(';\n """);`));
  check('verbatim ("" escape) and raw strings yield exactly two Sql operations', ops(r2).join(',') === 'Sql:DESTRUCTIVE,Sql:DESTRUCTIVE', ops(r2).join(','));
  const r3 = classify(mig('migrationBuilder.InsertData(table: "t", column: "c", value: "migrationBuilder.DropTable(");'));
  check('migrationBuilder inside a string is not an operation', ops(r3).join(',') === 'InsertData:SAFE', ops(r3).join(','));
  const r4 = classify(mig('migrationBuilder.CreateIndex(name: "ix", table: "t", columns: new[] { "A", "B" }, unique: true, filter: "\\"DeletedAt\\" IS NULL");'));
  check('escaped quotes and braces in args do not break parsing', ops(r4).join(',') === 'CreateIndex:REVIEW', ops(r4).join(','));
  const r5 = classify(mig('migrationBuilder.InsertData(table: "t", column: "c", value: $"{F("(")}");\nmigrationBuilder.DropTable(name: "t2");'));
  check('interpolation hole with a quoted "(" does not swallow the next statement', ops(r5).join(',') === 'InsertData:SAFE,DropTable:DESTRUCTIVE' && r5.verdict === 'DESTRUCTIVE', ops(r5).join(','));
  const r6 = classify(mig('#region x\nmigrationBuilder.DropTable(name: "t");\n#endregion'));
  check('preprocessor lines are ignored', ops(r6).join(',') === 'DropTable:DESTRUCTIVE', ops(r6).join(','));
  const r7 = classify(mig('migrationBuilder.Sql($"DELETE FROM {schema}.t WHERE a = \'(\' AND b = {x:N0}");'));
  check('interpolated Sql with braces and quotes is one DESTRUCTIVE op', ops(r7).join(',') === 'Sql:DESTRUCTIVE', ops(r7).join(','));
}
// 2. fail-safe parsing: anything the parser cannot read is REVIEW, never CLEAR
{
  const a = classify('using M; namespace X { public partial class M : Migration {\n protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "t");\n protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(name: "t", columns: table => new { });\n} }');
  check('expression-bodied Up/Down are parsed', ops(a).join(',') === 'DropTable:DESTRUCTIVE' && downOps(a).join(',') === 'CreateTable:SAFE', `${ops(a)} / ${downOps(a)}`);
  const b = classify('using M; namespace X { public partial class M : Migration {\n protected sealed override void Up(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable(name: "t"); }\n override protected void Down(MigrationBuilder migrationBuilder) { }\n} }');
  check('sealed / reordered modifiers are parsed', ops(b).join(',') === 'DropTable:DESTRUCTIVE', ops(b).join(','));
  const c = classify('using M; namespace X { public partial class M : Migration { protected override void Down(MigrationBuilder migrationBuilder) { } } }');
  check('missing Up() → <unparsed> REVIEW, never CLEAR', ops(c).join(',') === '<unparsed>:REVIEW' && c.verdict === 'REVIEW', `${ops(c)} ${c.verdict}`);
  check('binary/UTF-16/empty input → REVIEW', classify('M\u0000i\u0000g').verdict === 'REVIEW' && classify('').verdict === 'REVIEW' && classify(null).verdict === 'REVIEW');
  const d = classify(mig('migrationBuilder.CreateTable(name: "t", columns: table => new { Id = table.Column<Guid>(nullable: false) }'));
  check('unbalanced statement → <unparsed> REVIEW', ops(d).join(',') === '<unparsed>:REVIEW', ops(d).join(','));
  const e = classify(mig('migrationBuilder.AlterDatabase()\n .Annotation("Npgsql:Enum:x", "a,b")\n .OldAnnotation(NpgsqlAnnotationNames.Enum + "x", "a");'));
  check('non-literal OldAnnotation → REVIEW', ops(e).join(',') === 'AlterDatabase:REVIEW', ops(e).join(','));
  const f = classify(mig('migrationBuilder.CreateTable(name: "t", columns: table => new { });', 'migrationBuilder.AlterDatabase()\n .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");'));
  check('AlterDatabase with OldAnnotation in Down → REVIEW', downOps(f).join(',') === 'AlterDatabase:REVIEW' && f.verdict === 'REVIEW', `${downOps(f)} ${f.verdict}`);
}
// 3. generics and defaults
{
  const r = classify(mig('migrationBuilder.AddColumn<string>(name: "c", table: "t", type: "text", nullable: false);'));
  check('AddColumn<T> NOT NULL without default on existing table → REVIEW', ops(r)[0] === 'AddColumn:REVIEW', ops(r).join(','));
  const r2 = classify(mig('migrationBuilder.AlterColumn<string>(name: "c", table: "t", type: "varchar(10)", nullable: false, oldClrType: typeof(string));'));
  check('AlterColumn<T> → REVIEW', ops(r2)[0] === 'AlterColumn:REVIEW', ops(r2).join(','));
  const r3 = classify(mig('migrationBuilder.CreateSequence<int>(name: "s", schema: "x");'));
  check('CreateSequence<T> → SAFE', ops(r3)[0] === 'CreateSequence:SAFE' && r3.verdict === 'CLEAR', ops(r3).join(','));
  const r4 = classify(mig('migrationBuilder.AddColumn<int>(name: "c", table: "t", type: "integer", nullable: false, defaultValue: 0);'));
  check('AddColumn NOT NULL with default (no FK) → SAFE', ops(r4)[0] === 'AddColumn:SAFE', ops(r4).join(','));
  const r5 = classify(mig('migrationBuilder.AddColumn<string>(name: "c", table: "t", type: "text", nullable: false, defaultValue: null);'));
  check('defaultValue: null is not a default → REVIEW', ops(r5)[0] === 'AddColumn:REVIEW', ops(r5).join(','));
  const r6 = classify(mig('migrationBuilder.AddColumn<string>(name: "c", table: "t", type: "text", nullable: false, defaultValueSql: "now()");'));
  check('defaultValueSql counts as a default → SAFE', ops(r6)[0] === 'AddColumn:SAFE', ops(r6).join(','));
}
// 4. golden: every migration in the repo
{
  const expected = {
    '20260423113315_Init.cs': 'CLEAR', '20260511001231_InitResources.cs': 'CLEAR', '20260422122355_InitialCreate.cs': 'CLEAR',
    '20260514124150_AddTokenRevocations.cs': 'CLEAR', '20260511133835_AddDescriptionToResourceInstance.cs': 'CLEAR',
    '20260514155140_AddResourceTypeUniqueAndCursorIndexes.cs': 'REVIEW', '20260514155109_AddGroupOwnerNameUniqueIndex.cs': 'REVIEW', '20260928111659_SoftDeleteResourceTypeUniqueIndex.cs': 'REVIEW',
  };
  const ls = spawnSync('git', ['ls-files'], { cwd: REPO, encoding: 'utf8' }).stdout.split('\n').filter(isMigrationFile);
  check('golden set covers every migration in the repo', ls.length === Object.keys(expected).length && ls.every(p => path.basename(p) in expected), ls.map(p => path.basename(p)).join(','));
  for (const p of ls) {
    const r = classify(fs.readFileSync(path.join(REPO, p), 'utf8'));
    const flagged = [...r.up.filter(o => o.bucket !== 'SAFE').map(o => `UP ${o.op}`), ...r.down.filter(o => o.bucket !== 'SAFE').map(o => `DOWN ${o.op}`)];
    check(`golden ${path.basename(p)} → ${expected[path.basename(p)]}`, r.verdict === expected[path.basename(p)], `${r.verdict} [${flagged.join(', ')}]`);
  }
  const soft = classify(fs.readFileSync(path.join(REPO, 'backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Migrations/Migrations/20260928111659_SoftDeleteResourceTypeUniqueIndex.cs'), 'utf8'));
  check('015 migration: Up DropIndex is SAFE; Up filtered unique index is REVIEW (known false alarm — the script cannot see the stricter old index); Down unfiltered unique index is REVIEW (the real risk)',
    soft.up.find(o => o.op === 'DropIndex')?.bucket === 'SAFE' && soft.up.find(o => o.op === 'CreateIndex')?.bucket === 'REVIEW' && soft.down.some(o => o.op === 'CreateIndex' && o.bucket === 'REVIEW'));
  check('015 migration: the snippet of the Up index keeps the filter: line', /filter:/.test(soft.up.find(o => o.op === 'CreateIndex')?.snippet || ''));
}
// 5. 016 fixtures
{
  const i = classify(mig('migrationBuilder.DropTable(name: "resource_types", schema: "resources");\nmigrationBuilder.CreateTable(name: "resource_schemas", schema: "resources", columns: table => new { Id = table.Column<Guid>(type: "uuid", nullable: false) }, constraints: table => { table.PrimaryKey("PK_resource_schemas", x => x.Id); });', 'migrationBuilder.DropTable(name: "resource_schemas", schema: "resources");'));
  check('016 (i) DropTable + CreateTable → DESTRUCTIVE', i.verdict === 'DESTRUCTIVE', i.verdict);
  const ii = classify(mig('migrationBuilder.DropForeignKey(name: "FK_a", schema: "resources", table: "resource_property_definitions");\nmigrationBuilder.DropPrimaryKey(name: "PK_resource_types", schema: "resources", table: "resource_types");\nmigrationBuilder.RenameTable(name: "resource_types", schema: "resources", newName: "resource_schemas", newSchema: "resources");\nmigrationBuilder.RenameColumn(name: "ResourceTypeId", schema: "resources", table: "resource_property_definitions", newName: "ResourceSchemaId");\nmigrationBuilder.RenameIndex(name: "IX_a", schema: "resources", table: "resource_property_definitions", newName: "IX_b");\nmigrationBuilder.AddPrimaryKey(name: "PK_resource_schemas", schema: "resources", table: "resource_schemas", column: "Id");\nmigrationBuilder.AddForeignKey(name: "FK_b", schema: "resources", table: "resource_property_definitions", column: "ResourceSchemaId", principalSchema: "resources", principalTable: "resource_schemas", principalColumn: "Id", onDelete: ReferentialAction.Cascade);', 'migrationBuilder.RenameTable(name: "resource_schemas", schema: "resources", newName: "resource_types", newSchema: "resources");\nmigrationBuilder.RenameColumn(name: "ResourceSchemaId", schema: "resources", table: "resource_property_definitions", newName: "ResourceTypeId");'));
  check('016 (ii) rename variant → REVIEW, not DESTRUCTIVE', ii.verdict === 'REVIEW', ii.verdict);
  check('016 (ii) Down renames reversing Up renames are SAFE', ii.down.every(o => o.bucket === 'SAFE'), downOps(ii).join(','));
  const iii = classify(mig('migrationBuilder.DropColumn(name: "ResourceTypeId", schema: "resources", table: "resource_property_definitions");\nmigrationBuilder.AddColumn<Guid>(name: "ResourceSchemaId", schema: "resources", table: "resource_property_definitions", type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));\nmigrationBuilder.AddForeignKey(name: "FK_x", schema: "resources", table: "resource_property_definitions", column: "ResourceSchemaId", principalSchema: "resources", principalTable: "resource_schemas", principalColumn: "Id");'));
  const fkNote = iii.up.find(o => o.op === 'AddColumn');
  check('016 (iii) DropColumn + zero-default NOT NULL column + FK → DESTRUCTIVE with the FK note', iii.verdict === 'DESTRUCTIVE' && fkNote?.bucket === 'REVIEW' && /removed first/.test(fkNote?.note || ''), `${iii.verdict} ${fkNote?.bucket} ${fkNote?.note}`);
  const iv = classify(mig('migrationBuilder.AddColumn<Guid>(name: "C", table: "t", type: "uuid", nullable: false, defaultValue: Guid.Empty);\nmigrationBuilder.AddForeignKey(name: "FK_x", table: "t", columns: new[] { "C" }, principalTable: "u", principalColumn: "Id");'));
  check('Guid.Empty default + FK via columns: → FK note', /removed first/.test(iv.up.find(o => o.op === 'AddColumn')?.note || ''), ops(iv).join(','));
}
// 6. Down context
{
  const a = classify(mig('migrationBuilder.CreateTable(name: "t", columns: table => new { Id = table.Column<Guid>(nullable: false) });\nmigrationBuilder.CreateIndex(name: "ix", table: "t", column: "Id", unique: true);\nmigrationBuilder.AddForeignKey(name: "fk", table: "t", column: "Id", principalTable: "u", principalColumn: "Id");', 'migrationBuilder.DropForeignKey(name: "fk", table: "t");\nmigrationBuilder.DropTable(name: "t");'));
  check('unique index + FK on a table created in the same Up, Down reverses it → CLEAR', a.verdict === 'CLEAR', `${a.verdict} ${ops(a)} / ${downOps(a)}`);
  const b = classify(mig('migrationBuilder.AddColumn<string>(name: "c", table: "t", nullable: true);', 'migrationBuilder.DropTable(name: "other");'));
  check('Down drops a pre-existing table → REVIEW (never DESTRUCTIVE from Down)', b.verdict === 'REVIEW', b.verdict);
  const c = classify(mig('migrationBuilder.CreateIndex(name: "X", table: "t", column: "a");', 'migrationBuilder.DropForeignKey(name: "X", table: "t");'));
  check('Down DropForeignKey is not excused by an Up CreateIndex of the same name', downOps(c).join(',') === 'DropForeignKey:REVIEW', downOps(c).join(','));
  const d = classify(mig('migrationBuilder.RenameColumn(name: "a", table: "t", newName: "b");', 'migrationBuilder.RenameTable(name: "other", newName: "x");'));
  check('Down rename that does not reverse an Up rename → REVIEW', downOps(d).join(',') === 'RenameTable:REVIEW', downOps(d).join(','));
}
// 7. AlterDatabase chains, fail-safe ops
{
  const a = classify(mig('migrationBuilder.AlterDatabase()\n .Annotation("Npgsql:PostgresExtension:citext", ",,");'));
  check('AlterDatabase with extension annotation → SAFE', ops(a)[0] === 'AlterDatabase:SAFE' && a.verdict === 'CLEAR', ops(a).join(','));
  const b = classify(mig('migrationBuilder.AlterDatabase()\n .Annotation("Npgsql:Enum:x", "a,b")\n .OldAnnotation("Npgsql:Enum:x", "a");'));
  check('AlterDatabase with OldAnnotation → REVIEW', ops(b)[0] === 'AlterDatabase:REVIEW', ops(b).join(','));
  const c = classify(mig('migrationBuilder.FooBar(name: "x");'));
  check('unknown operation → REVIEW', ops(c)[0] === 'FooBar:REVIEW', ops(c).join(','));
  const d = classify(mig('var b = migrationBuilder;\nb.DropTable(name: "t");'));
  check('statements that are not migrationBuilder calls → REVIEW', d.up.length === 2 && d.up.every(o => o.op === '<statement>' && o.bucket === 'REVIEW'), d.up.map(o => o.op + ':' + o.bucket).join(','));
}
// 8. file filters
{
  check('migration file matched', isMigrationFile('backend/src/Modules/Users/ThingsBooksy.Modules.Users.Migrations/Migrations/20260422122355_InitialCreate.cs'));
  check('Designer excluded', !isMigrationFile('backend/src/Modules/Users/ThingsBooksy.Modules.Users.Migrations/Migrations/20260422122355_InitialCreate.Designer.cs'));
  check('snapshot excluded from migrations, detected as snapshot', !isMigrationFile('backend\\src\\Modules\\Users\\ThingsBooksy.Modules.Users.Migrations\\Migrations\\UsersDbContextModelSnapshot.cs') && isSnapshotFile('backend/src/Modules/Users/ThingsBooksy.Modules.Users.Migrations/Migrations/UsersDbContextModelSnapshot.cs'));
  check('file outside Migrations excluded', !isMigrationFile('backend/src/Modules/Users/ThingsBooksy.Modules.Users.Core/Migrations/x.cs'));
}
// 9. collectMigrationChanges on a throwaway git repo (A / M / D / R / untracked / snapshot / bad commit)
{
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'mc-'));
  const git = (...a) => spawnSync('git', a, { cwd: tmp, encoding: 'utf8' });
  const dir = 'backend/src/Modules/X/ThingsBooksy.Modules.X.Migrations/Migrations';
  const w = (rel, s) => { fs.mkdirSync(path.join(tmp, path.dirname(rel)), { recursive: true }); fs.writeFileSync(path.join(tmp, rel), s); };
  try {
    git('init', '-q'); git('config', 'user.email', 't@t'); git('config', 'user.name', 't'); git('config', 'core.autocrlf', 'false');
    w(`${dir}/1_A.cs`, mig('')); w(`${dir}/2_B.cs`, mig('')); w(`${dir}/XDbContextModelSnapshot.cs`, 'snap'); w(`${dir}/1_A.Designer.cs`, 'd');
    git('add', '-A'); git('commit', '-q', '-m', 'base'); const base = git('rev-parse', 'HEAD').stdout.trim();
    w(`${dir}/2_B.cs`, mig('// edited')); fs.renameSync(path.join(tmp, dir, '1_A.cs'), path.join(tmp, dir, '1_A_renamed.cs'));
    w(`${dir}/3_C.cs`, mig('')); git('add', '-A'); git('commit', '-q', '-m', 'change');
    w(`${dir}/4_D.cs`, mig('')); w(`${dir}/XDbContextModelSnapshot.cs`, 'snap2');
    const r = collectMigrationChanges(tmp, base);
    const byPath = Object.fromEntries(r.migrations.map(m => [path.basename(m.path), m.change]));
    check('collect: modified, renamed (as added + deleted old path), committed added, untracked added', byPath['2_B.cs'] === 'modified' && (byPath['1_A_renamed.cs'] === 'renamed' || byPath['1_A_renamed.cs'] === 'added') && byPath['3_C.cs'] === 'added' && byPath['4_D.cs'] === 'added', JSON.stringify(byPath));
    check('collect: Designer excluded, snapshot reported separately', !('1_A.Designer.cs' in byPath) && r.snapshots.length === 1 && /ModelSnapshot/.test(r.snapshots[0].path), JSON.stringify(r.snapshots));
    fs.unlinkSync(path.join(tmp, dir, '3_C.cs'));
    const r2 = collectMigrationChanges(tmp, git('rev-parse', 'HEAD').stdout.trim());
    check('collect: deleted file reported as deleted', r2.migrations.some(m => path.basename(m.path) === '3_C.cs' && m.change === 'deleted'), JSON.stringify(r2.migrations));
    let threw = false; try { collectMigrationChanges(tmp, 'deadbeef'); } catch { threw = true; }
    check('collect: unknown commit throws a readable error', threw);
    check('migrationSha is order-independent and distinguishes deleted', migrationSha([{ path: 'b', sha256: '2' }, { path: 'a', sha256: '1' }]) === migrationSha([{ path: 'a', sha256: '1' }, { path: 'b', sha256: '2' }]) && migrationSha([{ path: 'a', sha256: null }]) !== migrationSha([{ path: 'a', sha256: '1' }]));
  } finally { fs.rmSync(tmp, { recursive: true, force: true }); }
}
// 10. CLI on a synthetic story dir in this repo (cleaned up): REVIEW + GATE_OPEN idempotency, NO_MIGRATION + claim mismatch, no baseline
{
  const story = '998-mc-test'; const runDir = path.join(REPO, 'runs', story);
  const run = a => spawnSync(process.execPath, [SCRIPT, '--story', story, ...a], { cwd: REPO, encoding: 'utf8' });
  const mig015 = 'backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Migrations/Migrations/20260928111659_SoftDeleteResourceTypeUniqueIndex.cs';
  const added = spawnSync('git', ['log', '--format=%H', '--diff-filter=A', '--', mig015], { cwd: REPO, encoding: 'utf8' }).stdout.trim().split('\n').pop();
  try {
    fs.rmSync(runDir, { recursive: true, force: true });
    const r0 = run(['--dry-run']);
    check('CLI: no baseline.json → exit 1 with a hint', r0.status === 1 && /baseline\.js/.test(r0.stderr), `exit=${r0.status} ${r0.stderr.slice(0, 80)}`);
    fs.mkdirSync(runDir, { recursive: true });
    fs.writeFileSync(path.join(runDir, 'baseline.json'), JSON.stringify({ story, commit: `${added}^`, at: '2026-10-01T00:00:00.000Z' }));
    const r1 = run(['--print']);
    let rep = null; try { rep = JSON.parse(r1.stdout); } catch { /* reported */ }
    check('CLI: 015 migration since baseline → exit 5, REVIEW, g2b_required, report + GATE_OPEN written', r1.status === 5 && rep?.verdict === 'REVIEW' && rep?.g2b_required === true && fs.existsSync(path.join(runDir, 'migration-check.json')) && fs.readFileSync(path.join(runDir, 'journal.jsonl'), 'utf8').includes('"GATE_OPEN"'), `exit=${r1.status} ${rep?.verdict} ${r1.stderr.slice(0, 120)}`);
    check('CLI: --print keeps stdout as JSON and sends the human summary to stderr', /migration-check: REVIEW/.test(r1.stderr) && !/migration-check: REVIEW/.test(r1.stdout));
    check('CLI: report shape', rep && ['story', 'commit', 'files', 'verdict', 'migration_sha', 'g2b_required', 'be_writer_claims', 'be_writer_claim', 'be_writer_claim_note', 'claim_mismatch', 'computed_at', 'hash_version'].every(k => k in rep) && rep.files[0].sha256?.length === 64 && rep.hash_version === 2, Object.keys(rep || {}).join(','));
    run([]);
    check('CLI: GATE_OPEN is idempotent per migration_sha', (fs.readFileSync(path.join(runDir, 'journal.jsonl'), 'utf8').match(/"GATE_OPEN"/g) || []).length === 1);
    fs.mkdirSync(path.join(runDir, 'impl', 'be-writer.C3a'), { recursive: true });
    fs.writeFileSync(path.join(runDir, 'impl', 'be-writer.C3a', 'result.json'), JSON.stringify({ status: 'DONE', schema_changes: 'ADDITIVE' }));
    const r2 = run(['--commit', 'HEAD', '--dry-run', '--print']);
    let rep2 = null; try { rep2 = JSON.parse(r2.stdout); } catch { /* reported */ }
    check('CLI: no migration since HEAD but be-writer claimed ADDITIVE → NO_MIGRATION, exit 0, claim_mismatch', r2.status === 0 && rep2?.verdict === 'NO_MIGRATION' && rep2?.claim_mismatch === true && rep2?.be_writer_claim === 'ADDITIVE', `exit=${r2.status} ${rep2?.verdict} ${rep2?.claim_mismatch}`);
    fs.writeFileSync(path.join(runDir, 'impl', 'be-writer.C3a', 'result.json'), JSON.stringify({ status: 'DONE', schema_changes: 'DESTRUCTIVE' }));
    const r3 = run(['--commit', 'HEAD', '--dry-run', '--print']);
    let rep3 = null; try { rep3 = JSON.parse(r3.stdout); } catch { /* reported */ }
    check('CLI: be-writer forecast DESTRUCTIVE alone makes G2b required (exit 5)', r3.status === 5 && rep3?.g2b_required === true, `exit=${r3.status}`);
    const r4 = run(['--commit', 'not-a-commit', '--dry-run']);
    check('CLI: unknown --commit → exit 1 with a readable message', r4.status === 1 && /not in this repository/.test(r4.stderr), `exit=${r4.status} ${r4.stderr.slice(0, 100)}`);
    const r5 = run(['--commit', '--dry-run']);
    check('CLI: --commit without a value → usage, exit 1', r5.status === 1 && /usage/.test(r5.stderr));
  } finally { fs.rmSync(runDir, { recursive: true, force: true }); }
}
console.log(`\n${total - bad}/${total} passed`);
process.exit(bad ? 1 : 0);
