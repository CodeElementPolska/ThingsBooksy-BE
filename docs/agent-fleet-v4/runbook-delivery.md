# Runbook dyrygenta — sesja C (dostawa jednej story)

Stan: 2026-09-28, pierwszy przebieg na story `015-resource-time-buffer`. Dyrygentem jest zwykła sesja Claude (bez `--agent`) na gałęzi story. Docelowo ten runbook staje się skillem `/deliver`.

## Zasady dyrygenta
- Prompty agentów buduje **tylko** `tools/fleet/prompt-builder.js`; dyrygent podaje agentowi ścieżkę do `runs/<story>/prompts/<template>.md` („przeczytaj w całości i wykonaj"), nie streszcza i nie dopisuje kontekstu.
- Wynik agenta (JSON `result`) zapisuje dyrygent do `runs/<story>/impl/<agent>[.<phase>]/result.json` i waliduje: `node tools/fleet/validate.js --schema result --file …`. `files_changed` porównuje z `git status`.
- Między fazami działają wyłącznie skrypty. Dyrygent nie naprawia kodu sam — porażka wraca do agenta z outputem skryptu (nowa instancja, ten sam szablon, `--var fix_round=n`).
- Bramki właściciela (G2b migracja destrukcyjna, G3) = pytanie do właściciela w czacie; odpowiedź zapisuje `decide.js --gate`.
- Commity tylko na wyraźne „tak" właściciela.

## Przebieg

| Faza | Polecenia | Warunek przejścia |
|---|---|---|
| 0 start | `node tools/fleet/status.js --story <s>` (gałąź = story, `feature.json` zgodny) | exit 0, faza C1 |
| C1 brama planu | (plan-guard nie istnieje — pomijane); `node tools/fleet/contract-compose.js --story <s>` gdy jest overlay | compose OK |
| baseline | `node tools/fleet/baseline.js --story <s>` — zapisuje commit startu kodu story (`runs/<s>/baseline.json`); `skeleton-check` i `coverage-gaps` liczą diff od niego, nie od merge-base z `main` | plik istnieje, drzewo `backend/` czyste |
| C3a szkielet | `node tools/fleet/prompt-builder.js --story <s> --agent be-writer --template be-writer-skeleton --var module=<M>` → Agent `be-writer` → zapis result → `node tools/fleet/skeleton-check.js --story <s>` | `status: DONE`, skeleton-check OK, build zielony |
| migracja | **właściciel**: `dotnet ef migrations add <Name> --project backend/src/Modules/<M>/ThingsBooksy.Modules.<M>.Migrations --startup-project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`; potem `dotnet test backend/src/Shared/ThingsBooksy.Shared.IntegrationTests --filter Category=Tooling` (odświeża `generated/core-surface.json`) | migracja i snapshot w drzewie |
| C2 testy ślepe | `prompt-builder … --agent test-designer --template test-designer --var module=<M>` → Agent `test-designer` → result → `node tools/fleet/red-first-prover.js --story <s>` → `node tools/fleet/ac-matrix.js --story <s>` | RED, każde AC ma test |
| C3b zachowanie | `prompt-builder … --agent be-writer --template be-writer-behaviour --var module=<M>` → Agent → result → `node tools/fleet/gate.js --story <s>` | gate GREEN (limit 3 rund napraw) |
| C4b testy z kodem | `node tools/fleet/coverage-gaps.js --story <s>` → `prompt-builder … --agent test-designer-sighted --template test-designer-sighted --var module=<M>` → Agent → result → `gate.js` | GREEN; testy `UNSPECIFIED` trafiają do listy na G3 |
| C5 review | reviewerzy jeszcze nie istnieją (2026-09-28) — zatrzymać się i zaprojektować na materiale z tej story | — |
| C6 zamknięcie | `dod.js` → `closer.js` → G3 (właściciel) → commit za zgodą | DoD DONE |

## Statusy agentów i reakcja dyrygenta
- `DONE` → następny skrypt.
- `BLOCKED_ON_DECISION` → `decisions_needed[]` pokazać właścicielowi w formacie D-2 (AskUserQuestion, jeśli opcje dyskretne) → `decide.js --decision … --answer-ref latest` → ponowny agent.
- `DISPUTE_TEST` → spór test↔spec: właściciel rozstrzyga (arbiter nie istnieje); zmiana testu = tester + `REBASELINE` w journalu.
- `FAILED` → treść `error` do właściciela; nie ponawiać w ciemno.

## Znane ograniczenia pierwszego przebiegu
- Brak reviewerów, guarda, arbitra i skryptu workflow — dyrygent ręczny.
- `runs/<story>/prompts/*` są generowane; commitować razem z `impl/` na końcu story.
- Story 015: T009–T011 (recording broker, TestClient) wykonuje **tester** w C2, mimo że `tasks.md` umieszcza je w fazie 2.
