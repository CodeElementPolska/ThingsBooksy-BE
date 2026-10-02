# Plan v2: `migration-check.js` (po krytyce z 2026-10-01; decyzje właściciela: A1 = REVIEW też otwiera G2b, B = sprostowanie D-4a)

Repo: D:\Projects\AI\ThingsBooksy-BE, gałąź 016-rename-resource-schema. Commit docelowy: `feat(fleet): migration-check (S9b) — deterministic G2b trigger`.

## Zasada
Skrypt jest fail-safe: klasyfikuje każdą instrukcję `Up()`/`Down()` nowych lub zmienionych migracji od `baseline` w koszyki SAFE/REVIEW/DESTRUCTIVE, nieznane = REVIEW. **REVIEW i DESTRUCTIVE otwierają G2b** (D-1: dane i migracje zawsze z decyzją właściciela — A1). Odpowiedź G2b jest związana z treścią migracji (`migration_sha`); regeneracja = nowy raport = nowe pytanie. Gate odmawia, gdy migracja istnieje bez aktualnego raportu lub bez pasującej odpowiedzi.

## Parser (`scrub` + ciała metod + instrukcje)
- `scrub(src)`: ta sama długość i te same znaki nowej linii; komentarze `//` (także na końcu linii) i `/* */` → spacje; zawartość stringów `"…"` (z `\"`), `@"…"` (`""`), `$"…"`, `$@"…"`/`@$"…"`, `"""…"""` (raw, ≥3 cudzysłowy) i `'c'` → `x`, delimitery zostają. Dzięki temu nawiasy/klamry/`;` wewnątrz stringów nie liczą się.
- Ciała: w scrubbed `protected override void (Up|Down)\s*\(` → pierwsza `{` → liczenie klamer do pary. Instrukcje: podział po `;` na głębokości nawiasów 0.
- Instrukcja: `^migrationBuilder\s*\.\s*(\w+)\s*(?:<[^>()]*>)?\s*\(` → `op`; nawiasy do pary → argumenty (scrubbed, podział po przecinkach na głębokości 0, `name: value`); reszta instrukcji → łańcuch `.Annotation("…")`/`.OldAnnotation("…")` (nazwy z ORYGINAŁU). Wartości potrzebne z oryginału: `table:`/`schema:`/`name:`/`column:`/`columns:`/`newName:`, `unique: true`, `nullable: false`, `defaultValue:` (zero: `new Guid("0000…")`, `""`, `0`, `false`), `defaultValueSql:`, `filter:`. Instrukcja nie będąca wywołaniem na `migrationBuilder` → op `<statement>`, REVIEW.
- `line` = numer linii początku instrukcji; `snippet` = oryginalne linie instrukcji (max 6).

## Koszyki w `Up()` (kontekst: `created` = tabele z `CreateTable` w tym samym Up; `addedCols` = `AddColumn` w tym Up)
| op | koszyk |
|---|---|
| `EnsureSchema`, `CreateTable`, `CreateSequence`, `InsertData`, `DropIndex`, `RenameIndex` | SAFE |
| `CreateIndex` | `unique: true` i tabela ∉ created → REVIEW; inaczej SAFE |
| `AddUniqueConstraint`, `AddPrimaryKey`, `AddForeignKey`, `AddCheckConstraint` | tabela ∈ created → SAFE; inaczej REVIEW (Postgres waliduje istniejące wiersze: duplikaty, sieroty, NULL) |
| `AddColumn` | tabela ∈ created → SAFE; `nullable: false` bez `defaultValue`/`defaultValueSql` → REVIEW; `nullable: false` z zerowym default **i** `AddForeignKey` w tym Up na tej kolumnie → REVIEW z notą „FK fails on existing rows unless data is wiped (AC-9)”; inaczej SAFE |
| `AlterDatabase` | łańcuch wyłącznie `.Annotation("Npgsql:PostgresExtension:…"|"Npgsql:CollationDefinition:…"|"Npgsql:Enum:…")` bez `.OldAnnotation` → SAFE; inaczej REVIEW |
| `AlterColumn`, `AlterSequence`, `AlterTable`, `RestartSequence`, `RenameTable`, `RenameColumn`, `RenameSequence`, `DropForeignKey`, `DropPrimaryKey`, `DropCheckConstraint`, `DropUniqueConstraint` | REVIEW |
| `DropTable`, `DropColumn`, `DropSchema`, `DropSequence`, `DeleteData`, `UpdateData`, `Sql` | DESTRUCTIVE |
| nieznany op, `<statement>` | REVIEW |

## Koszyki w `Down()` (max REVIEW; kontekst: created/added z Up oraz z samego Down)
- `Drop*` celu utworzonego/dodanego w Up (tabela, kolumna, indeks, klucz/constraint po nazwie) → SAFE; `Drop*` innego celu → REVIEW („rollback drops a pre-existing object”); `DropIndex` → SAFE.
- `Rename*` → SAFE, gdy Up ma `Rename*` (odwrócenie); inaczej REVIEW.
- Operacje mogące paść na danych (`CreateIndex unique`, `AddUniqueConstraint`, `AddPrimaryKey`, `AddForeignKey`, `AddCheckConstraint`, `AlterColumn`, `AddColumn nullable:false` bez default, `Sql`, `DeleteData`, `UpdateData`) → REVIEW, chyba że tabela ∈ created w Down.
- Reszta (`CreateTable`, `AddColumn` nullable, `CreateIndex` nie-unique, `EnsureSchema`, …) → SAFE. Nieznane → REVIEW.
- Werdykt = max(Up, min(Down, REVIEW)).

## Zbiór plików i werdykty specjalne
- Wymagany `runs/<story>/baseline.json` (`commit`) albo `--commit <sha>`; brak → exit 1 „run baseline.js first” (świadomie bez fallbacku do merge-base).
- `git diff --name-status <sha>` (bez pathspec; filtr regexem w JS: `backend/src/Modules/*/ThingsBooksy.Modules.*.Migrations/Migrations/*.cs`, bez `*.Designer.cs`, bez `*ModelSnapshot.cs`) + `git ls-files --others --exclude-standard`. Status: A/untracked = `added`, M = `modified` (REVIEW „edited an already-committed migration” + klasyfikacja), D = `deleted` (REVIEW, bez klasyfikacji), R = `renamed` (jak added).
- 0 migracji, ale zmieniony `*ModelSnapshot.cs` → werdykt `MODEL_CHANGED_NO_MIGRATION`, exit 1 (brakuje `dotnet ef migrations add`).
- 0 zmian → `NO_MIGRATION`, exit 0.

## Raport `runs/<story>/migration-check.json`
`{ story, commit, files: [{ path, change, sha256, up: [{op, bucket, line, note?, snippet}], down: [...] , verdict }], verdict: NO_MIGRATION|CLEAR|REVIEW|DESTRUCTIVE|MODEL_CHANGED_NO_MIGRATION, migration_sha, g2b_required, be_writer_claims: [{path, schema_changes}], claim_mismatch, computed_at, hash_version: 2 }`
- `sha256` przez `sha256File` z `hash.js`; `migration_sha` = sha256 posortowanych `path:sha256` (identyfikuje treść migracji, nie czas).
- `be_writer_claims` z `runs/<story>/impl/be-writer.C3a*/result.json` (`schema_changes`); claim = max; `claim_mismatch` gdy skrypt ≥ REVIEW a claim NONE, skrypt DESTRUCTIVE a claim ≠ DESTRUCTIVE, albo NO_MIGRATION a claim ≠ NONE. Raport zaznacza, że claim jest prognozą sprzed wygenerowania pliku.
- `g2b_required = verdict ∈ {REVIEW, DESTRUCTIVE} || claim === DESTRUCTIVE`.
- Gdy `g2b_required` i nie `--dry-run`: dopisanie do `journal.jsonl` `{at, event:'GATE_OPEN', phase:'G2b', artifact:'migration-check.json', migration_sha}` idempotentnie (pomijane, gdy ostatni GATE_OPEN G2b ma ten sam `migration_sha`). `journal.js` bez zmian (skrypty piszą GATE_* bezpośrednio, jak `decide.js`).
- Stdout: werdykt, lista `file:line op [bucket] note` z wycinkami dla REVIEW/DESTRUCTIVE, mismatch, instrukcja: `AskUserQuestion (never a chat answer — the hook records only AskUserQuestion) → node tools/fleet/decide.js --story <s> --gate G2b --status PASSED|REJECTED --answer-ref latest`.
- Exit: 0 NO_MIGRATION/CLEAR · 5 G2B_REQUIRED · 1 usage/baseline/MODEL_CHANGED_NO_MIGRATION. `--dry-run` = bez zapisu raportu i journala (testy, podgląd). `--print` = JSON na stdout.
- Eksporty modułu: `scrub`, `classify(source) → {up, down, verdict}`, `collectMigrationChanges(repo, sha) → [{path, change}]`, `isMigrationFile`, `MIGRATION_RE`; CLI pod `import.meta.url === pathToFileURL(process.argv[1]).href`.

## `decide.js --gate G2b`
- Wymaga `runs/<story>/migration-check.json`; odmawia (exit 3), gdy go nie ma, albo gdy `OWNER_ANSWER.at <= computed_at` raportu (odpowiedź starsza niż raport = nie dotyczy tej migracji). GATE_ANSWER dla G2b dostaje `migration_sha`.

## `gate.js` — krok `migration` jako PIERWSZY
- Brak `baseline.json` → FAILED „run baseline.js”. `collectMigrationChanges` = 0 → SKIPPED „no migration since baseline”. Pliki są, raportu brak → FAILED „run migration-check”. `sha256` któregoś pliku ≠ raport → FAILED „stale report — rerun migration-check”. `g2b_required` bez ostatniego `GATE_ANSWER` `phase=G2b status=PASSED` z `migration_sha` === raportu → FAILED „G2b not answered for this migration”. Inaczej PASSED z werdyktem. Journal parsowany jako JSONL.

## `status.js`
- Po `gates`: jeśli raport istnieje i `g2b_required` i brak GATE_ANSWER G2b PASSED z pasującym `migration_sha` → `open_items.blockers` dostaje stałe id `G2B-MIGRATION`. (DoD nie czyta state; zatrzymuje go gate RED — udokumentowane w README.)

## Testy `migration-check.test.js` (`npm run migration:test`, bez zależności)
1. `scrub`: komentarz na końcu linii z `migrationBuilder.DropTable(` → brak operacji; `Sql(@"…\…""…")` i `Sql("""…"Col"…(…)…""")` → jedna operacja `Sql`, nawiasy w stringu nie psują; `migrationBuilder.` wewnątrz stringa → brak operacji; długość i liczba linii zachowane.
2. Generyki: `AddColumn<string>(nullable: false)` na istniejącej tabeli → REVIEW; `AlterColumn<string>(` → REVIEW; `CreateSequence<int>(` → SAFE.
3. Golden na 8 istniejących migracjach: MG Init, Res InitResources, Users InitialCreate, AddTokenRevocations, AddDescriptionToResourceInstance → CLEAR; AddResourceTypeUniqueAndCursorIndexes → REVIEW (Up); AddGroupOwnerNameUniqueIndex, SoftDeleteResourceTypeUniqueIndex (015) → REVIEW (Down zawiera `CreateIndex unique` na istniejącej tabeli).
4. Fikstury 016: (i) `DropTable(resource_types)` + `CreateTable(resource_schemas)` → DESTRUCTIVE; (ii) rename: `DropForeignKey`/`DropPrimaryKey`/`RenameTable(newName)`/`RenameColumn`/`RenameIndex`/`AddPrimaryKey`/`AddForeignKey` → REVIEW; (iii) `DropColumn` + `AddColumn<Guid>(nullable:false, defaultValue: new Guid("0000…"))` + `AddForeignKey` → DESTRUCTIVE z notą FK.
5. `Down` z `DropTable` tabeli utworzonej w Up → SAFE (CLEAR); `Down` z `DropTable` innej tabeli → REVIEW.
6. `AlterDatabase().Annotation("Npgsql:PostgresExtension:citext", ",,")` → SAFE; z `.OldAnnotation` → REVIEW.
7. Nieznany op `FooBar(` → REVIEW; instrukcja `var b = migrationBuilder;` → REVIEW.
8. `isMigrationFile`: Designer/Snapshot wykluczone, ścieżka spoza Migrations wykluczona.
9. `--dry-run` na story 015 (baseline istnieje, migracja po baseline): werdykt REVIEW, `g2b_required: true`, `claim_mismatch: false` przy claim ADDITIVE (udokumentowane: mismatch nie łapie pomyłki be-writera w Down); brak zapisu.

## Dokumenty
- `runbook-delivery.md`: wiersz 11 (bramki właściciela: „pytanie do właściciela w czacie” → „AskUserQuestion — tylko ona trafia do journala jako OWNER_ANSWER”); wiersz „migracja”: `→ node tools/fleet/migration-check.js --story <s>`; wynik: `CLEAR | REVIEW/DESTRUCTIVE → AskUserQuestion z wycinkami → decide.js --gate G2b --status PASSED|REJECTED --answer-ref latest; REJECTED → regeneracja → ponownie migration-check`.
- `workflow.md`: :75 bez „, migracja”; :78 `→ migracja generowana [właściciel] → migration-check [skrypt] (REVIEW/DESTRUCTIVE → G2b)`; :112 `**G2b:** gdy migration-check (po dotnet ef migrations add, przed C2) da REVIEW lub DESTRUCTIVE, albo be-writer zgłosi schema_changes: DESTRUCTIVE`; tabela §5 (skrypty) — wiersz S9b.
- `decisions.md`: D-4a — dopisek „**Sprostowanie 2026-10-01 (właściciel):** migrację generuje właściciel między C3a a C2 (`dotnet ef migrations add`), be-writer jej nie edytuje; destrukcyjność ocenia `migration-check` (S9b) i G2b”.
- `tools/fleet/README.md`: wiersz S9b w tabeli skryptów (format kolumn jak reszta); Konwencje: testy `npm run migration:test`; wyjątek journala (GATE_OPEN z migration-check, GATE_ANSWER z decide); dziennik: wiersz 2026-10-01 (dlaczego skrypt, nie agent; 8/8 → 5 CLEAR / 3 REVIEW po poprawkach krytyków).
- `tools/fleet/package.json`: `"migration:test"`.
- `tools/fleet/owner-answer-hook.js:2-3`: komentarz „registered project-wide in .claude/settings.json (PostToolUse AskUserQuestion), fires for personas and the main session”.

## Poza zakresem (świadomie)
- Analiza treści `Sql(...)`; rozpoznawanie zawężenia w `AlterColumn` (każdy = REVIEW); próbne `Up`/`Down` na Testcontainers (możliwe później, Docker jest w sesji C).
- Szablon be-writera i `result.schema.json` bez zmian.
