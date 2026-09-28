# tools/fleet — substrat floty agentów v4

Deterministyczne skrypty (bez LLM), na których stoją bramki z `docs/agent-fleet-v4/workflow.md` §5. Node ≥ 20, ESM, `npm install` w tym katalogu (D-12). Wszystkie wołane z katalogu repo: `node tools/fleet/<skrypt>.js …`.

| # | Skrypt | Status | Co robi | Wejście → wyjście | Exit |
|---|---|---|---|---|---|
| S1 | `acl-hook.js` | ✅ zweryfikowany (31 testów + smoke w harnessie) | hook `PreToolUse` zarejestrowany w `.claude/settings.json` projektu (D-13); egzekwuje `fleet-acl.json` per `agent_type`: Read/Grep/Glob wewnątrz `read_allow`, Edit/Write wewnątrz `write_allow`, Bash/PowerShell tylko z prefiksów `shell`, nigdy `deny_always`. Nieznany `agent_type` = brak ograniczeń. Fail-closed. | stdin JSON hooka → exit | 0 ok · 2 deny |
| S1 | `fleet-acl.json` | konfiguracja | allowlisty per agent; korzenie mogą zawierać `*`/`**` segmenty | — | — |
| S2 | `capability-map.js` | ✅ (28 endpointów, 9 ekranów, 3 moduły) | mapa możliwości aplikacji: endpointy ze swaggera zbudowanego backendu, ekrany z `*.routes.ts`, moduły z `/modules`. Jedyne wejście dla analityka biznesowego. `--refresh` uruchamia test `SwaggerExport` (Docker). | `generated/swagger.base.json`, `generated/modules.json` → `generated/capability-map.json` | 0 · 1 brak wejść |
| S2 | `SwaggerExportTests` (xUnit, `Category=Tooling`) | ✅ | eksport swaggera i listy modułów z `ThingsBooksyWebAppFactory` (D-15) | `dotnet test backend/src/Shared/ThingsBooksy.Shared.IntegrationTests --filter Category=Tooling` → `generated/*.json` | — |
| S3a | `contract-compose.js` | ✅ | składa OpenAPI Overlay 1.0 (delta story) z bazą → pełny `contract-next.json` (D-10); wymaga `x-evidence` dla akcji dotykających istniejących tras | `runs/<story>/contract-delta.overlay.json` + baza → `runs/<story>/contract-next.json` | 0 · 3 walidacja |
| S3b | `contract-diff.js` | ✅ | dryf kontrakt ↔ implementacja: brakujące/niezamówione operacje, różnice kodów odpowiedzi, `openapi-diff` (breaking) | `contract-next.json` vs `generated/swagger.base.json` (po re-eksporcie) → `runs/<story>/contract-diff.json` | 0 CLEAR · 3 DRIFT |
| S5 | `ac-matrix.js` | ✅ | macierz AC → testy z `[Trait("AC","AC-n")]` (BE) i `it("[AC-n] …")` (FE), opcjonalnie wyniki z TRX | `story.md`/`spec.md` (id AC) + źródła testów → `runs/<story>/ac-matrix.json` | 0 · 3 niepokryte AC |
| S4 | `red-first-prover.js` | ✅ (probe: failing→RED, passing→`passed_prematurely`) | po C3a: buduje projekty testów, uruchamia testy story (`--filter AC=…`), każdy musi upaść; zapisuje hash źródeł testów akceptacyjnych | AC z `story.md`/`--ac` → `runs/<story>/tests/red-first.json` | 0 RED · 3 NOT_RED/NO_TESTS/BUILD_FAILED |
| S4a | `CoreSurfaceExportTests` (xUnit, `Category=Tooling`) | ✅ (3 moduły, 40 typów) | dump powierzchni typów Core przez refleksję: encje, read modele, DbContext+DbSet, sygnatury `Create`, settery; wejście testera ślepego (D-4a) | → `generated/core-surface.json` | — |
| S4b | `coverage-gaps.js` | 📝 napisany, nieprzetestowany | zmienione linie produkcyjne (git diff vs merge-base) ∩ nietrafione linie / częściowe gałęzie z Cobertura (coverlet) → wejście przebiegu 2 testera (D-4b) | → `runs/<story>/coverage-gaps.json` | 0 · 3 luki |
| S4c | `skeleton-check.js` | ✅ | zmiany vs merge-base tylko w Domain/DAL/ReadModels/Exceptions/Migrations/Shared.Abstractions + rekordy Command/Query/Result; handlery, walidatory, Api, frontend, testy = naruszenie | → `runs/<story>/skeleton-check.json` | 0 · 3 naruszenia |
| S6 | `gate.js` | ✅ (GREEN na bieżącym drzewie: build, format, 128 testów, re-eksport swaggera; 44 s) | build → format `--verify-no-changes` → arch-tests (SKIPPED do epika) → testy (story lub `--full`) → re-eksport swaggera → contract-diff → ac-matrix → hash testów vs red-first (REBASELINE w journal) | → `runs/<story>/gate.json` | 0 GREEN · 3 RED |
| S7a | `status.js` | ✅ (guard gałęzi + syntetyczna story) | wylicza `runs/<story>/state.json` z artefaktów i `journal.jsonl` (schemat `state`): faza, bramki, hashe, otwarte decyzje / założenia z listy D-1 / blockery; odmawia, gdy gałąź ≠ story (`--skip-branch-check` tylko do testów) | → `runs/<story>/state.json` | 0 · 1 |
| S7b | `dod.js` | ✅ (syntetyczna story: 6/10 → NOT_DONE) | Definition of Done jako skrypt: gate GREEN, AC 100%, brak otwartych decyzji, brak założeń z listy D-1 bez decyzji, 0 BLOCKER, brak sporów, UNSPECIFIED z decyzją, contract CLEAR, tasks.md odhaczone, hash testów | → `runs/<story>/dod.json` | 0 DONE · 3 NOT_DONE |
| S7c | `closer.js` | 📝 napisany, nieprzetestowany (wymaga story z DoD=DONE) | `metrics.json` (§6 workflow) + `close-report.md` z checklistą na issue (wkleja dyrygent przez MCP — `gh` nie jest zainstalowane) + `PHASE_END C6` w journal | → `runs/<story>/{metrics.json,close-report.md}` | 0 · 3 |
| S8 | `prompt-builder.js` + `templates/<agent>.md` | ✅ (szablon `code-researcher`; wyłapał brak `runs` w ACL) | prompt = szablon (front matter: `inputs`, `output_schema`, `conventions`, `max_turns`) + lista plików wejściowych z hashami + schemat wyjścia + blok provenance; odmawia, gdy wejście jest poza `read_allow` agenta lub brakuje wymaganego pliku. Żadnych streszczeń w prompcie. | → `runs/<story>/prompts/<agent>[.<instance>].{md,json}` | 0 · 3 |
| — | `owner-answer-hook.js` | ✅ (symulacja stdin + `decide`) | hook `PostToolUse(AskUserQuestion)` z frontmatteru person (dev-analyst, scrum): zapisuje odpowiedź właściciela do `runs/<story>/journal.jsonl` jako `OWNER_ANSWER` (jedyne źródło `decided_by: owner`) | stdin hooka → journal | 0 |
| — | `decide.js` | ✅ | jedyny zapis `decided_by: owner`: bierze wybraną opcję z `OWNER_ANSWER` w journalu (`--answer-ref latest` lub `tool_use_id`), odrzuca opcję spoza listy; `--veto ASM-n --replacement DEC-m` | decisions/assumptions.jsonl | 0 · 3 |
| — | `validate.js` | ✅ | walidacja JSON/JSONL względem `docs/agent-fleet-v4/schemas/*.schema.json` (ajv 2020-12) | plik lub stdin | 0 · 3 |
| S10 | `fleet-smoke` | częściowo (agent `fleet-smoke-blind`) | zabawkowa story przez cały substrat | | |

## Agenci sesji B (iteracja promptów, 2026-09-24)

| Agent | Tryb | Plik | Stan |
|---|---|---|---|
| `dev-analyst` | persona `claude --agent dev-analyst` na gałęzi `NNN-slug` | `.claude/agents/dev-analyst.md` | ✅ headless: ładuje się, egzekwuje regułę gałęzi, mówi po polsku |
| `code-researcher` | subagent (sonnet, RO, `omitClaudeMd`) | `.claude/agents/code-researcher.md` + `templates/code-researcher.md` | ✅ headless na prawdziwym pytaniu: `fact` poprawny wg schematu, provenance skopiowane |
| `impact-analyst` | subagent (sonnet, RO, `omitClaudeMd`) | `.claude/agents/impact-analyst.md` + `templates/impact-analyst.md` | 📝 napisany, nieprzetestowany |

## Agenci sesji C — dostawa (2026-09-28)

| Agent | Faza | Plik agenta | Szablon(y) | Stan |
|---|---|---|---|---|
| `be-writer` | C3a szkielet, C3b zachowanie | `.claude/agents/be-writer.md` | `templates/be-writer-skeleton.md`, `templates/be-writer-behaviour.md` (`--template`) | 📝 napisany; ACL: pisze Core/Api/Shared.Abstractions, czyta testy, bez Migrations, bez `ef migrations` |
| `test-designer` | C2 przebieg ślepy | `.claude/agents/test-designer.md` | `templates/test-designer.md` | 📝 napisany; ACL: bez `src` Core/Api; pisze projekty testów + Shared.IntegrationTests; `dotnet build/test` projektów testowych |
| `test-designer-sighted` | C4b przebieg 2 | `.claude/agents/test-designer-sighted.md` | `templates/test-designer-sighted.md` | 📝 napisany; czyta wszystko, pisze tylko testy |

Dyrygent C1–C6: na razie sesja główna (prompt-builder → Agent → skrypty między fazami); skill `/deliver` po pierwszym pełnym przebiegu.

## Dziennik defektów floty (z prawdziwych przebiegów)

| Data | Story | Defekt | Naprawa |
|---|---|---|---|
| 2026-09-24 | 015 | szablon `code-researcher`: „nothing else was given to you" odczytane jako zakaz czytania kodu | prompt-builder: wejścia ≠ zakres odczytu; zakres = ACL |
| 2026-09-24 | 015 | wszystkie fakty `id: F-1` | `prompt-builder` nadaje `F-<nr pytania>` przez `{fact_id}` |
| 2026-09-24 | 015 | `decide.js` logował `GATE_ANSWER G2` przy każdej decyzji → `status` pokazywał G2 PASSED za wcześnie | zdarzenie `DECISION`; bramkę zamyka tylko `decide --gate G2 --status PASSED|REJECTED` |
| 2026-09-24 | 015 | `.specify/feature.json` wskazywał 010 → `/speckit-plan` nadpisał `specs/010/plan.md` | `status.js` odmawia przy niezgodności; checklista startu `dev-analyst` |
| 2026-09-24 | 015 | persona uruchomiła `gate.js` w discovery | zakaz w prompcie persony |
| 2026-09-24 | 015 | pliki `.trx` w `runs/` | `.gitignore: runs/**/tests/` |
| 2026-09-28 | 015 | `tasks.md`: infrastruktura testowa (recording broker, TestClient) w fazie szkieletu; taski zawierają gotowy kod | reguła w prompcie `dev-analyst`; szablony writer/tester filtrują zadania po zakresie, nie po nagłówku fazy |
| 2026-09-24 | 015 | założenia o `BufferMinutes` zostały ACTIVE po rescopingu | pole `status: WITHDRAWN` w schemacie `assumption`; `status`/`dod` je pomijają |

## Konwencje

- Identyfikator story: `NNN-slug` = gałąź = `specs/NNN-slug/` = `runs/NNN-slug/`.
- `generated/` i `runs/` są commitowane (provenance); `tools/fleet/node_modules` nie.
- Skrypty nie piszą do `decisions.jsonl` pola `decided_by` inaczej niż z `journal.jsonl` (`OWNER_ANSWER`).
- Testy skryptów: `npm run acl:test`; pozostałe skrypty testowane ręcznie na syntetycznych wejściach (zob. historia w `docs/agent-fleet-v4/`).
