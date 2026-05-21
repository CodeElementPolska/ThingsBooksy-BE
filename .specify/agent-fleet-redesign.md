# Agent Fleet Redesign — Plan

**Cel:** rozszerzyć obecną flotę 17 agentów tak, aby workflow obsługiwał feature wytwarzany równolegle BE+FE z wspólnym, negocjowanym kontraktem FE↔BE. Punkt wejścia: brainstorming z agentem przed designem HTML.

Plik jest self-contained — można go wykonać w cold session poleceniem „wykonaj zawartość pliku `.specify/agent-fleet-redesign.md`".

---

## 1. Diagnoza stanu wyjściowego

**BE pipeline (dojrzały):**
`product-strategist → doc-writer → /speckit-specify → /speckit-plan → /speckit-tasks → plan-validator → contract-definer → module-scaffolder → module-writer → migration-agent → quality-reviewer → integration-test-writer → architecture-guard`

**FE pipeline (skrócony, oddzielny):**
`html-extractor → fe-plan-validator → fe-api-client-writer → fe-component-writer → fe-route-writer`

**Główne luki:**
1. Brak agenta negocjującego kontrakt FE↔BE. `contract-definer` obsługuje wyłącznie BE↔BE.
2. `.specify/` (spec/plan/tasks) jest backend-only — żaden agent FE go nie czyta.
3. Brak FE-owego odpowiednika `product-strategist` — FE startuje dopiero gdy HTML istnieje.
4. Brak FE-owego odpowiednika `plan-validator` na poziomie specu.
5. Bug ścieżki `swagger.json` w `html-extractor` (`frontend/src/app/api/swagger.json` — `fe-api-client-writer` nie zapisuje tam tego pliku).
6. FE zawsze trailuje BE — `fe-api-client-writer` wymaga runtime swagger z uruchomionego BE.
7. Phase 3 `html-extractor` (interview o endpointach) i częściowo Phase 5 (routing) powtarzają informację, która powinna istnieć wcześniej w pipeline.

---

## 2. Decyzje architektoniczne

Wszystkie potwierdzone interaktywnie przed napisaniem tego pliku.

| # | Decyzja | Wartość |
|---|---|---|
| 1 | Scope router | Phase 0 w `product-strategist` (nie osobny agent, nie reguła w CLAUDE.md) |
| 2 | Scope'y | 4: `fe-only` / `be-only` / `both` / `direct-edit` |
| 3 | `fe-only` używa SpecKit | Pełny SpecKit z FE-aware spec.md (nie skip) |
| 4 | `plan-validator` | Rozszerzony o FE checks (jeden agent, dwie ścieżki) |
| 5 | Kontrakt FE↔BE | Osobny `specs/{feature}/api-contract.md` (korekta z `.specify/{feature}/...` zgodnie z layoutem spec-kit, 2026-05-21) |
| 6 | Cykl kontraktu | `/speckit-plan` produkuje v1 → `contract-definer` finalizuje |
| 7 | Design HTML | Pojawia się po Phase 3 strategist; **iteracyjny loop** developer ↔ strategist ↔ Claude Design |
| 8 | BE Wave / FE Wave | Równolegle po `contract-definer` GO |
| 9 | swagger.json dla FE | Static, emitowany do `specs/{feature}/swagger.json` (korekta z `.specify/{feature}/...`, 2026-05-21) |
| 10 | Drift handling | Drift = BLOCKER; repair loop z `module-writer` (cap 2 iteracje) |
| 11 | `html-extractor` zmiana | Opcja A: rozszerz o Phase 0 (czyta `.specify/`), usuń Phase 3 (endpoint interview), dodaj Phase 0.5 (contract sanity check) |
| 12 | OpenAPI emission | `contract-definer` finalizuje api-contract.md; nowy skill `/swagger-emit` (Element 4b) emituje swagger.json deterministycznie. Oba pliki produkowane w jednym kroku orkiestracji (korekta z "literalnie naraz" — split na agent+skill dla lepszej walidacji JSON-a, 2026-05-21) |
| 13 | FE tasks w tasks.md | `/speckit-tasks` rozszerzony, FE tasks per-component |
| 14 | `contract-drift-check` | Nowy standalone agent |
| 15 | FE QA gates | `fe-quality-reviewer` per-component (NOWY); bez `fe-architecture-guard` |
| 16 | FE testy | `fe-test-writer` per-feature (NOWY), po `fe-route-writer` |
| 17 | Kolejność wdrożenia | Najpierw `product-strategist` (Phase 0 router + Phase 3 FE surface) |

---

## 3. Docelowy workflow

**Scope `both` (najpełniejszy):**

```
1. product-strategist
   Phase 0: SCOPE ROUTER → fe-only / be-only / both / direct-edit
   Phase 1: business why
   Phase 2: BE breakdown (warunkowo wg scope)
   Phase 3: FE surface + iteracyjny loop z Claude Design (warunkowo wg scope)
            → finalnie developer podaje ścieżkę do lokalnego HTML
            ↓
2. doc-writer (ADR)
            ↓
3. /speckit-specify → spec.md (FE+BE aware)
            ↓
4. /speckit-plan → plan.md + api-contract.md (v1)
            ↓
5. /speckit-tasks → tasks.md (BE tasks per-module + FE tasks per-component)
            ↓
6. plan-validator (rozszerzony) → EXECUTION MAP (BE Wave + FE Wave)
            ↓
7. contract-definer → finalizuje api-contract.md + emituje specs/{feature}/swagger.json
            ↓
   ┌─────────────────────────────┬─────────────────────────────┐
   ↓ BE Wave                     ↓ FE Wave (równolegle)
   module-scaffolder              html-extractor (rozszerzony)
   module-writer                  fe-plan-validator
   migration-agent                fe-api-client-writer (czyta static swagger)
   quality-reviewer               fe-component-writer
   integration-test-writer        fe-quality-reviewer (NOWY, per-component)
                                  fe-route-writer
                                  fe-test-writer (NOWY, per-feature)
   └─────────────────────────────┴─────────────────────────────┘
            ↓
8. architecture-guard (BE solution-wide)
            ↓
9. contract-drift-check (NOWY) → runtime swagger ↔ specs/{feature}/swagger.json
   drift > 0 = BLOCKER → repair loop z module-writer
```

**Warianty wg scope:**
- `be-only`: pomija Phase 3 strategist, FE Wave, html-extractor, fe-*, contract-drift-check (BE-only feature nie ma FE contract'u).
- `fe-only`: pomija Phase 2 strategist (lub minimalizuje), BE Wave; api-contract.md tylko jeśli FE potrzebuje nowych endpointów (wtedy de facto = `both`).
- `direct-edit`: strategist po Phase 0 kończy z komunikatem „to nie wymaga pipeline'u, popraw ręcznie X w pliku Y".

---

## 4. Lista zmian floty

| Komponent | Status | Zmiana |
|---|---|---|
| `product-strategist` | MODIFY | + Phase 0 (scope router), + Phase 3 (FE surface z iteracyjnym loopem Claude Design) |
| `plan-validator` | MODIFY | + FE checks, FE Wave w EXECUTION MAP |
| `/speckit-plan` skill | MODIFY | + generuje `api-contract.md` v1 |
| `/speckit-tasks` skill | MODIFY | + FE tasks per-component |
| `contract-definer` | MODIFY | + finalizuje `api-contract.md` + emituje `swagger.json` (+ `Write` w tools) |
| `html-extractor` | MODIFY | + Phase 0 czyta `.specify/`, − Phase 3 endpoint interview, + Phase 0.5 contract sanity check |
| `fe-quality-reviewer` | **NEW** | Per-component review po `fe-component-writer` |
| `fe-test-writer` | **NEW** | Per-feature integracyjne testy po `fe-route-writer` |
| `contract-drift-check` | **NEW** | Finalna bramka po obu Wave'ach |
| `CLAUDE.md` | MODIFY | Update tabeli agentów, orchestration rules dla nowych ogniw |

---

## 5. Kolejność wdrożenia

**Etap 1 (start):** rozszerzenie `product-strategist` ✅ **DONE (2026-05-21)**
- Phase 0: scope router ✅
- Phase 3: FE surface + iteracyjny loop z Claude Design ✅
- Wykorzystać `agent-architect` (model Opus 4.7) do projektowania ✅
- **Cel walidacji:** po wdrożeniu przetestować na 2 realnych feature'ach (jeden `fe-only`, jeden `be-only`) zanim ruszymy dalej — **PENDING (do zrobienia przed Etap 2)**

Implementation notes:
- Plik agenta: `.claude/agents/product-strategist.md` (~381 linii treści)
- CLAUDE.md: zaktualizowano tabelę "Known agents" i orchestration rule dla `doc-writer` (direct-edit short-circuit)
- Kluczowe rozszerzenia: Phase 0 (scope router z 4 wartościami + budget 1 inventory action + max 1 doprecyzowujące pytanie), direct-edit short-circuit (cap 3 wywołań, brak handoff), scope revision rule (agent-wide, hurtowy summary konfliktów + 3 opcje), Phase 3 (iteracyjny loop z markerem `[STATE: WAITING_FOR_PATH]`, ambiguity flows A/B, scope drift detection w 3a i 3d), handoff z fixed literal `Scope:` + warunkowy block `FE Surface` + opcja `Design artifact: none — modifies existing screens`

**Etap 2:** kontrakt FE↔BE
- Element 1: emisja `api-contract.md` v1 po `/speckit-plan` ✅ **DONE (2026-05-21)**
- Element 2: rozszerzenie `plan-validator` o FE checks + FE Wave w EXECUTION MAP ✅ **DONE (2026-05-21)**
  - Dodane checki: B1 (endpoint→task match), B2a (FE Surface↔FE tasks count), B5 (Status DRAFT/FINALIZED), B7 (module ownership, WARNING), B8 (cross-module deps), B9 (Scope consistency)
  - Odrzucone: B3, B4, B6 (TBD jest legalne, pagination semantic match łamie determinizm)
  - EXECUTION MAP: BE Waves + FE Wave (per-component listing, klasyfikacja po ścieżkach backend/src vs frontend/src)
  - Defensive: refuse if spec.md missing Scope field (direct-edit safeguard)
- Element 3: rozszerzenie `/speckit-tasks` o FE tasks per-component ✅ **DONE (2026-05-21)**
  - Implementacja: nowy skill `.claude/skills/fe-tasks-augment/SKILL.md` + mandatory `after_tasks` hook
  - Vendored `speckit-tasks/SKILL.md` nietknięty
  - Format: 1 task per component, `Consumes: METHOD /route` aktywuje CHECK 8 plan-validatora, marker `[FE]` odrzucony (path-based classification)
  - Sources: FE Surface w spec.md (primary), endpoints NEW w api-contract.md jako WARNING block (developer dopisuje brakujące modale)
  - api-client regen przeniesiony na początek FE Wave (foundational), wszystkie komponenty zależą od niego
  - Store tasks NIE generowane automatycznie — WARNING block sugeruje developerowi
  - Early-exit dla Scope=be-only
  - Idempotency: --refresh (default, hook auto) / --force (manual override)
  - **Known limitation**: plan-validator FE Wave płaska (bez sub-waves wg FE→FE deps) — defer do Etap 3
- Element 4: rozszerzenie `contract-definer` o finalizację `api-contract.md` (DRAFT→FINALIZED) ✅ **DONE (2026-05-21)**
  - Per-endpoint negotiation TBD-ów, cross-cutting round, drift-ack z plan.md
  - Surgical updates via Edit (zachowanie user-edits w sekcji Notes)
  - Sekcja `## 4. Open questions` → `## 4. Resolved questions`, nowa sekcja `## 6. Negotiation log`
  - Tools: dodany Edit, NIE dodany Bash (swagger emission delegowane do skilla)
  - Sygnalizuje orchestratorowi: "Next step: invoke /swagger-emit"
- Element 4b: nowy skill `/swagger-emit` emisja `specs/{feature}/swagger.json` ✅ **DONE (2026-05-21)**
  - Deterministyczna konwersja FINALIZED api-contract.md → OpenAPI 3.0 JSON
  - Walidacja: pwsh Test-Json + LLM self-check (5-10 deterministycznych checków vs api-contract.md)
  - Retry cap 2; po 3-cim failu ABORT z sugestią `npx swagger-cli validate`
  - NIE jest auto-invoked (brak hooka); orchestrator woła ręcznie po `CONTRACT-DEFINER COMPLETE`
  - Tools: Glob, Read, Write, Bash

Implementation notes (Etap 2 element 1):
- Skill: `.claude/skills/api-contract-emit/SKILL.md` — `user-invocable: true`, accepts `--force` flag for manual override of FINALIZED contracts; automatic hook never passes `--force`.
- Hook: `.specify/extensions.yml` — dodano entry pod `hooks.after_plan` po `git.commit`, `enabled: true`, `optional: false`, `condition: null`. Brak fizycznego katalogu `.specify/extensions/api-contract/` (decyzja: minimalizm).
- Idempotency: `Status: DRAFT|FINALIZED` w frontmatter pliku; brak `Last-modified-by` field. Skill nigdy nie pisze `FINALIZED` — to zarezerwowane dla `contract-definer` w Etap 4.
- Scope fallback: brak sygnału `Scope:` w spec.md → domyślnie `both` + adnotacja w sekcji Notes.
- Korekta decyzji 5 i 9: oba pliki (`api-contract.md`, `swagger.json`) lądują w `specs/{feature}/...`, nie w `.specify/{feature}/...` — zgodne z layoutem spec-kit, w którym katalogi per-feature żyją pod `specs/`.
- CLAUDE.md: dodano jedną linię w Orchestration rules (po regule `fe-route-writer`) informującą orchestrator o automatycznym emicie kontraktu po `/speckit-plan`.

**Etap 3:** parallelizm FE Wave
- Element 1: rozszerz `html-extractor` (Phase 0 + 0.5, usuń Phase 3) ✅ **DONE (2026-05-21)**
  - Phase 0 czyta trzy źródła z `specs/{feature}/`: `spec.md` (FE Surface + Scope), `api-contract.md` (FINALIZED endpoints + Consumers), `tasks.md` (FE tasks names jako authoritative naming)
  - Feature slug resolution: branch inference (jak `api-contract-emit`) → fallback: most-recently-modified `specs/*/spec.md` → fallback: ASK ONCE
  - Refuse-conditions w Phase 0.3: `Scope: be-only|direct-edit` → ABORT; `api-contract.md Status: DRAFT` → ABORT; brak `FE Surface` w spec.md → ABORT
  - Phase 0.5 contract sanity check: B1 (UI action→endpoint, BLOCKER), B2 (endpoint NEW/MODIFIED→UI, WARNING), B3 (FE Surface screen→HTML section, BLOCKER); challenge mechanism per BLOCKER, brak scope revision flow (upstream concern)
  - Phase 3 (endpoint interview) **usunięte**; zastąpione Phase 1.85 (autopopulated endpoint mapping z api-contract.md — `Consumer` field driver naming, cross-check z tasks.md)
  - Phase 2 dekompozycja: cross-check z tasks.md FE tasks; annotacje `NEW COMPONENT (not in tasks.md)` / `MERGES tasks T0XX + T0YY` per komponent
  - Renumeracja faz: Phase 0/0.5/1 (z 1.85)/2/3 (a11y, ex-4)/4 (routing, ex-5)/5 (approval, ex-6, bez API endpoint checkboxa)/6 (output, ex-7)
  - Final output block: dodana sekcja `Contract sanity overrides` (loguje challenged BLOCKERs i accepted WARNINGs), `Task mapping` per komponent, `Contract source: specs/{feature}/api-contract.md (Status: FINALIZED)` header
  - Tools bez zmian (`Read, Glob` wystarcza — agent nie pisze, swagger.json opcjonalny fallback)
- Element 2: Update `fe-api-client-writer` (czyta static swagger.json zamiast runtime) ✅ **DONE (2026-05-21)**
  - Phase 0 (feature resolution + preflight) zastępuje starą Phase 1 (Step 1.1 z runtime check)
  - Źródło: `specs/{featureSlug}/swagger.json` (jedyne akceptowane)
  - Resolution: orchestrator-provided slug → `setup-plan.ps1 -Json` BRANCH field → ASK ONCE
  - Preflight: file missing → ABORT z komunikatem o brakującym `/swagger-emit` (brak fallbacku do runtime ani do dev-provided path)
  - Optional Phase 0.3: jeśli `api-contract.md` Status=DRAFT → WARNING ale nie blokuje (swagger.json istnieje, więc go-ahead)
  - Generation: absolute path via `Resolve-Path` (working dir shift do `frontend/`)
  - Output report: dodane pola `Feature slug`, `Contract status` (FINALIZED/DRAFT/UNKNOWN), sekcja `Warnings`
  - Tools bez zmian (Read, Write, Edit, Bash); Bash potrzebny do `npx swagger-typescript-api` + `pwsh setup-plan.ps1`
- Element 3: `fe-quality-reviewer` (NEW) ✅ **DONE (2026-05-21)**
  - Per-component (1 invocation per komponent), interactive read-only, analogiczny do BE `quality-reviewer`
  - Inputs: PascalCase name + folder absolute path + opcjonalnie `HTML-EXTRACTOR COMPLETE` block (cross-check)
  - 12 checków: standalone/inject/selector, signals, input()/output(), control flow, feature service boundary (api/ → BLOCKER), forms (nonNullable, getRawValue, firstValueFrom), async validators (timer+first+catchError), styling (tokens, no ::ng-deep, breakpoints), smart/dumb placement, type safety (no `any`), test coverage (.spec.ts mandatory), html-extractor plan alignment (opcjonalnie)
  - Phase 3 zero-issues → "Do you want deeper review?" (analogicznie BE)
  - Output: `FE-QUALITY-REVIEWER COMPLETE` z BLOCKERS/WARNINGS/NOTES/Challenged items/Agent fleet suggestions
  - Tools: Glob, Grep, Read (read-only); model: sonnet
  - Repair loop w orchestratorze: BLOCKERS>0 i Challenged=0 → re-invoke `fe-component-writer` z violation description, cap 2 iteracje, eskalacja na 3-cim failu
- Element 4: `fe-test-writer` (NEW) ✅ **DONE (2026-05-21)**
  - Per-feature (1 invocation after wszystkie `fe-component-writer` Build:PASSED + `fe-route-writer` Build:PASSED)
  - Inputs: feature kebab name + `FE-ROUTE-WRITER COMPLETE` block + optional list of components in scope
  - Refuse-conditions: `Scope: be-only|direct-edit` → BLOCKED; `api-contract.md Status: DRAFT` → BLOCKED; brak files → BLOCKED; brak Vitest+Angular config → BLOCKED
  - Phase 2 TEST PLAN przed pisaniem (developer confirms scope: files to create/extend, scenarios per component, routing assertions)
  - Test scope: integration-style (HttpTestingController, real TestBed, real signals, real forms, router spies via vi.spyOn) — NIE unit testy każdego signal'a
  - HTTP mock alignment: status codes, payload shapes, error formats — dokładnie wg `api-contract.md`
  - Scenario matrix per endpoint: happy/empty/error-500/error-422/error-404/router-navigate
  - Routing spec per feature: lazy import resolution + guard attachment assertions
  - Vitest single-run: `npm test -- --run` (nie watch); 3 attempts repair loop dla test logic errors; production bugs/contract drift → BLOCKED (nie naprawia produkcji)
  - Output: `FE-TEST-WRITER COMPLETE` z files created/extended, tests written, run result, scenarios covered, Blocked
  - Tools: Glob, Grep, Read, Write, Edit, Bash; model: sonnet

**Etap 4:** finalna bramka ✅ **DONE (2026-05-21)**
- `contract-drift-check` (NEW) ✅
  - Standalone read-only interactive gate; uruchamiany po BE Wave (ARCHITECTURE-GUARD COMPLETE) AND FE Wave (FE-TEST-WRITER COMPLETE)
  - Phase 0 (locate feature + verify FINALIZED api-contract + parse Module: column) / Phase 1 (fetch runtime swagger via Invoke-WebRequest, retry once on developer "ready" signal — agent NIE startuje BE) / Phase 2 (6 kategorii diff: missing endpoints BLOCKER, extra endpoints WARNING, type mismatches BLOCKER, parameter mismatches BLOCKER, response shape mixed, error codes WARNING) / Phase 3 (klasyfikacja repair ownership: BE drift → module-writer | contract drift → contract-definer --force | ambiguous → developer decision)
  - Out-of-scope guard: porównujemy tylko endpointy w zakresie kontraktu; runtime endpointy z innych feature'ów ignorowane
  - Output: `CONTRACT-DRIFT-CHECK COMPLETE` block z `Drift summary:` (per category counts), `Repair recommendations:` per-owner (module-writer per Module / contract-definer --force), `Status: ALL CLEAR | BLOCKED`
  - Tools: Glob, Read, Bash (Bash do Invoke-WebRequest + pwsh setup-plan.ps1 dla feature slug); brak Write/Edit (read-only, plik tempa w `$env:TEMP`, nie w repo)
  - Refuse-conditions: brak `ARCHITECTURE-GUARD COMPLETE` lub `FE-TEST-WRITER COMPLETE` → BLOCKED; brak `api-contract.md` (be-only/direct-edit) → BLOCKED (skip); `Status: DRAFT` → BLOCKED
  - Repair loop spec (CLAUDE.md): per-recommendation re-invoke `module-writer({Module})` lub `contract-definer --force` → `/swagger-emit` → `fe-api-client-writer`, na końcu re-invoke `contract-drift-check`. Cap 2 iteracje, eskalacja na 3-cim failu (idem `architecture-guard` / `quality-reviewer`)
- Update `CLAUDE.md` (orchestration rules + tabela) ✅
  - Nowy wpis w tabeli "Known agents"
  - Nowa orchestration rule po `/swagger-emit` skill block (final-gate position)

---

## STATUS: FLEET REDESIGN ZAMKNIĘTY (2026-05-21)

Wszystkie 4 etapy DONE. Flota gotowa do walidacji na pierwszym realnym feature `Scope: both`:

| Etap | Zakres | Status |
|---|---|---|
| 1 | `product-strategist` (Phase 0 scope router + Phase 3 FE surface z Claude Design loop) | ✅ DONE |
| 2 | Kontrakt FE↔BE (`/api-contract-emit`, `plan-validator` FE checks, `/fe-tasks-augment`, `contract-definer` rozszerzony, `/swagger-emit`) | ✅ DONE |
| 3 | Parallelizm FE Wave (`html-extractor` rozszerzony, `fe-api-client-writer` static-only, `fe-quality-reviewer` NEW, `fe-test-writer` NEW) | ✅ DONE |
| 4 | Finalna bramka (`contract-drift-check` NEW + CLAUDE.md orchestration) | ✅ DONE |

**Następny krok:** walidacja end-to-end na realnym feature'rze `Scope: both`. Sugerowana ścieżka:
1. `product-strategist` → Phase 0 classifies `both` → Phase 1+2+3 → handoff brief
2. `/speckit-specify` → `/speckit-plan` → automatyczny `/api-contract-emit` (after_plan hook) → `/speckit-tasks` → automatyczny `/fe-tasks-augment` (after_tasks hook)
3. `plan-validator` → GO + EXECUTION MAP (BE Waves + FE Wave)
4. `contract-definer` → BE↔BE + FE↔BE finalization → `/swagger-emit`
5. **Równolegle:** BE Wave (`module-scaffolder` → `module-writer` → `migration-agent` → `quality-reviewer` → `integration-test-writer` → `architecture-guard`) i FE Wave (`html-extractor` → `fe-plan-validator` → `fe-api-client-writer` → `fe-component-writer` → `fe-quality-reviewer` → `fe-route-writer` → `fe-test-writer`)
6. **Final gate:** `contract-drift-check` (runtime swagger ↔ static swagger)

Walidacja powinna ujawnić: koszty (token consumption), interaction overhead (czy iteracyjne loops są znośne dla dewelopera), realne edge case'y (np. drift między api-contract.md a tasks.md przy rozszerzaniu spec.md w trakcie), kosztochłonność per-component FE Wave.

Wszelkie znalezione luki/poprawki: nowy plik `agent-fleet-iteration-N.md` w `.specify/`.

---

## 6. Ryzyka

| Ryzyko | Mitygacja |
|---|---|
| Prompt `product-strategist` rośnie (3 fazy vs 2 obecnie), jakość pojedynczych faz może spaść | Testować na 1-2 realnych przypadkach po wdrożeniu Etap 1 |
| Phase 3 iteracyjny loop z Claude Design wymaga state-handling (czekam na ścieżkę / dostałem wytyczne / dostałem ścieżkę) | Explicit state markers w prompcie agenta |
| Mis-klasyfikacja w Phase 0 routerze | Decyzja musi być rewizjowalna; developer może override'ować |
| Drift między api-contract.md a runtime swagger akumuluje się | BLOCKER + repair loop, cap 2 iteracje, eskalacja do usera na 3-ciej |
| `contract-definer` emituje JSON ręcznie (LLM) → ryzyko sporadycznego błędu składni | Po zapisie agent waliduje swagger.json prostym validatorem; retry jeśli zły |
| `html-extractor` prompt puchnie po dodaniu Phase 0/0.5 | Trzymać Phase 1 (analiza HTML) bez zmian; testować jakość po wdrożeniu |
| Single source of truth (api-contract.md) staje się single point of failure | `plan-validator` walidacja kontraktu przed contract-definer; manual review na finalizacji |
| Workflow staje się skomplikowany — 9 etapów, 6+ zmienionych agentów, 3 nowych | Etap 1 sprawdzony zanim rusza Etap 2; rollback przez git zawsze możliwy |

---

## 7. Kontekst do cold session

Jeśli wracasz do tego pliku po przerwie:

1. Zacznij od **Etap 1** — rozszerzenie `product-strategist`.
2. Użyj agenta `agent-architect` (model **Opus 4.7**) do iteracyjnego projektowania.
3. Kontekst dla agent-architect: "rozszerz product-strategist o Phase 0 (scope router z 4 wartościami) i Phase 3 (FE surface z iteracyjnym loopem Claude Design)". Decyzje 1, 2, 7 z sekcji 2 są wiążące.
4. Po wdrożeniu Etap 1, walidacja na 1-2 feature'ach, dopiero potem Etap 2.

**Existing agent files** żywą w `.claude/agents/`. **Konwencje** w `.claude/conventions/`. **Constitution** w `.specify/memory/constitution.md`. **CLAUDE.md** zawiera orchestration rules.
