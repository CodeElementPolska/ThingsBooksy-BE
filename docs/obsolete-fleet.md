# OBSOLETE — stara flota agentów (v3), wycięta z CLAUDE.md 2026-09-24

> Ten plik jest archiwum. Nie czytać przy projektowaniu nowej floty (`docs/agent-fleet-v4/`).
> Agenci i skille, o których mowa, mają prefiks `obsolete-` w `.claude/agents/` i `.claude/skills/`.

---

## Workflow

- **New features / substantial changes:** SpecKit — `/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`. Helpers: `/speckit-clarify`, `/speckit-analyze`, `/speckit-checklist`, `/speckit-constitution`, `/speckit-taskstoissues`.
- **Small bugfixes:** edit directly, no SpecKit, no full agent pipeline.

---

## Agent fleet

Main session is the orchestrator — it reads agent descriptions and orchestration rules below.

### Known agents

| Agent | When to delegate |
|---|---|
| `agent-architect` | Designing a new agent or growing the fleet |
| `convention-writer` | Interactive session to add or change a coding convention |
| `product-strategist` | Scope-routed interview (Phase 0 classifies fe-only/be-only/both/direct-edit, then Phase 1 business, Phase 2 BE, Phase 3 FE with Claude Design prompt). Produces handoff brief for `/speckit-specify`, except `direct-edit` which short-circuits with a file pointer and no handoff |
| `doc-writer` | After `product-strategist` brief — writes ADR; also for Stage 3 pivots |
| `plan-validator` | After `/speckit-tasks` (and after `/api-contract-emit` runs via after_plan hook), before `/speckit-implement` — GO/NO-GO + EXECUTION MAP (BE Waves + FE Wave with per-component listing) |
| `contract-definer` | After plan-validator GO — handles two contract negotiations: (1) BE↔BE `IEvent`/`IModuleClient` (cross-module deps in EXECUTION MAP), (2) FE↔BE `api-contract.md` finalization (DRAFT→FINALIZED, per-endpoint TBD resolution). Signals orchestrator to invoke `/swagger-emit` next |
| `module-scaffolder` | Before `module-writer` when `backend/src/Modules/{Name}/` does not exist — creates the full empty scaffold (4 projects, `.slnx` registration, `ThingsBooksyWebAppFactory` patches) and reports `Build: PASSED` |
| `module-writer` | After contracts ready (and after `module-scaffolder` for new modules) — implements one module's assigned task IDs |
| `migration-agent` | After module-writer when `Schema changes != NONE` — generates EF migration |
| `quality-reviewer` | After migration-agent (or module-writer if no schema changes) — interactive code review |
| `integration-test-writer` | After quality-reviewer ends — writes integration tests for the module |
| `architecture-guard` | After all Wave modules tested — solution-wide cross-module check |
| `fe-api-client-writer` | Regenerates TypeScript HTTP client → `frontend/src/app/api/` from the static `specs/{feature}/swagger.json` only. Aborts fail-fast if the file is missing — no runtime swagger fallback |
| `html-extractor` | Analyzes a Claude Design `.html` artifact against `specs/{feature}/spec.md` + `api-contract.md` (FINALIZED) + `tasks.md`; runs contract sanity check (UI↔endpoint↔FE Surface) and produces a component plan with autopopulated endpoint mapping |
| `fe-plan-validator` | After `html-extractor` Phase 6 approval, before any `fe-component-writer` — interactive read-only gate; produces `VERDICT GO/NO-GO` |
| `fe-component-writer` | After `fe-plan-validator` GO — implements one Angular component per call |
| `fe-quality-reviewer` | After every `fe-component-writer` `Build: PASSED` and before `fe-route-writer` — per-component interactive read-only review (12 checks) against `.claude/conventions/angular-*.md`; produces `FE-QUALITY-REVIEWER COMPLETE` with BLOCKERS/WARNINGS/NOTES/Challenged items |
| `fe-route-writer` | After all `fe-component-writer` and `fe-quality-reviewer` instances for a feature complete — creates `{feature}.routes.ts` and registers in `app.routes.ts` |
| `fe-test-writer` | After `fe-route-writer` reports `Build: PASSED` — writes feature-level Vitest integration tests against FINALIZED `api-contract.md` (HTTP mocks, signal assertions, routing, form flows); runs `npm test -- --run` |
| `contract-drift-check` | Final pipeline gate. After BOTH BE Wave (`architecture-guard`) and FE Wave (`fe-test-writer`) complete. Fetches runtime swagger from `localhost:8080`, diffs vs static `specs/{feature}/swagger.json`, categorizes drift (6 categories, BLOCKER/WARNING), classifies repair owner (BE drift → `module-writer` / contract drift → `contract-definer --force`). Read-only, interactive. Produces `CONTRACT-DRIFT-CHECK COMPLETE` with `Status: ALL CLEAR | BLOCKED` |

### Orchestration rules (compact)

- **doc-writer** runs immediately after `product-strategist` returns a brief. Do not start `/speckit-specify` until ADR is written. Skip `doc-writer` entirely when `product-strategist` returned a `Scope: direct-edit` short-circuit — there is no brief to document.
- **plan-validator** NO-GO blocks `/speckit-implement`. On GO, pass EXECUTION MAP downstream — orchestrator spawns BE Waves and FE Wave (parallel after `contract-definer` GO). Refuses to run if spec.md lacks `Scope:` field (direct-edit safeguard).
- **contract-definer** runs only when EXECUTION MAP has cross-module deps. Block `module-writer` until contracts are ready.
- **module-scaffolder** runs before `module-writer` for any module whose `backend/src/Modules/{Name}/` directory does not exist. Pass only the module name (PascalCase). Block `module-writer` until `MODULE-SCAFFOLDER COMPLETE` with `Build: PASSED` is received.
- **module-writer** spawns one instance per module per Wave; parallelize independent modules.
- **migration-agent** runs only when `Schema changes != NONE`.
- **quality-reviewer** is interactive and read-only. Block `integration-test-writer` until session ends. If the final report has `BLOCKERS > 0` and zero `Challenged items`, repair loop: re-invoke `module-writer` for the affected module passing the violation description → tail pipeline (`migration-agent` if `Schema changes != NONE`) → re-invoke `quality-reviewer` with the same task IDs. Cap 2 iterations; on the 3rd failure escalate to the user. Skip the loop entirely when the user marked the BLOCKER as `Challenged (no resolution)`.
- **integration-test-writer** runs after `quality-reviewer` session ends. The EF schema is added to `SchemasToInclude` by `module-scaffolder` for new modules; verify it is present before invoking integration-test-writer (Respawn will not clean otherwise).
- **architecture-guard** runs after all Wave modules report `INTEGRATION-TEST-WRITER COMPLETE`. If `Challenged (no resolution) > 0`, repair loop: re-invoke `module-writer` with violation description → tail pipeline (`migration-agent` if schema → `quality-reviewer` → `integration-test-writer`) → re-invoke `architecture-guard`. Cap 2 iterations; on 3rd failure escalate to user.
- **fe-plan-validator** runs after `html-extractor` Phase 6 approval and before any `fe-component-writer` invocation. Pass the full `HTML-EXTRACTOR COMPLETE` block as inline text. If `VERDICT: NO-GO`, do not invoke `fe-component-writer` — surface unresolved BLOCKERs to the developer, correct the plan, re-run `html-extractor` Phase 6, then re-invoke `fe-plan-validator`. If `GO` (including Challenged items), proceed.
- **html-extractor** refuses to run when `specs/{feature}/api-contract.md` is `Status: DRAFT` or missing, when `spec.md` is `Scope: be-only|direct-edit`, or when `spec.md` lacks an `FE Surface:` block. Run `contract-definer` and `/swagger-emit` before invoking `html-extractor` for any `Scope: both` or `Scope: fe-only` feature. Phase 0.5 contract sanity check (UI↔endpoint↔FE Surface) may surface BLOCKERs that the developer either fixes upstream or challenges with a documented reason — challenged overrides land in the `HTML-EXTRACTOR COMPLETE` block under `Contract sanity overrides` and are visible to `fe-plan-validator`.
- **fe-api-client-writer** runs at the start of every FE Wave to populate or refresh `frontend/src/app/api/`. It reads exclusively from `specs/{feature}/swagger.json` (static, emitted by `/swagger-emit`) — there is no runtime fallback. If `/swagger-emit` aborted or `swagger.json` is missing, `fe-api-client-writer` will fail-stop and the FE Wave does not start; fix the upstream contract pipeline (`contract-definer` → `/swagger-emit`) and re-invoke.
- **fe-component-writer** requires populated `frontend/src/app/api/` — run `fe-api-client-writer` first if empty. Independent components may run in parallel; Wave is done only when every instance reports `Build: PASSED`.
- **fe-quality-reviewer** runs after every `fe-component-writer` instance reports `Build: PASSED`, before `fe-route-writer`. One invocation per component. Interactive and read-only. Pass component PascalCase name + folder absolute path + optionally the `HTML-EXTRACTOR COMPLETE` block. If the final report has `BLOCKERS > 0` and zero `Challenged items`, repair loop: re-invoke `fe-component-writer` for the affected component passing the violation description → re-invoke `fe-quality-reviewer`. Cap 2 iterations; on the 3rd failure escalate to the user. Skip the loop entirely when the user marked the BLOCKER as `Challenged (no resolution)`.
- **fe-route-writer** runs after every `fe-component-writer` for a feature reports `Build: PASSED` AND every `fe-quality-reviewer` session has ended. Pass feature name + routing plan (paths, component class names, guards). Missing components cause the agent to fail-stop. One invocation per feature; independent features may run in parallel.
- **fe-test-writer** runs after `fe-route-writer` reports `Build: PASSED` for a feature. One invocation per feature. Pass feature kebab name + the full `FE-ROUTE-WRITER COMPLETE` block. Refuses to run when `spec.md` Scope is `be-only|direct-edit`, when `api-contract.md` Status is `DRAFT`, or when Vitest+Angular is not configured. Presents a TEST PLAN (Phase 2) and waits for developer confirmation before writing. Runs `npm test -- --run` (single-run, no watch). If tests fail due to a production bug or contract drift, the agent stops and reports `Blocked` — it does not edit production code. Repair for test-logic errors is internal (cap 3 attempts).
- **api-contract-emit** runs automatically via `.specify/extensions.yml` `after_plan` hook (non-optional, no `--force`). Emits or refreshes `specs/{feature}/api-contract.md` as `Status: DRAFT`. Manual invocation `/api-contract-emit --force` is reserved for the developer to overwrite a `FINALIZED` contract; finalization itself belongs to `contract-definer` (Etap 4).
- **fe-tasks-augment** runs automatically after `/speckit-tasks` via the mandatory `after_tasks` hook (`.specify/extensions.yml`). Reads `FE Surface:` from `spec.md` handoff and endpoints from `api-contract.md`, generates 1 FE task per component (page, panels, modals) plus a single global api-client regen task (foundational) and one routes task per feature. Skips entirely when `Scope: be-only`. Idempotent via `--refresh` (default for hook) vs `--force` (manual override). Every generated task contains `frontend/src/` in its description so `plan-validator` CHECK 10 classifies it correctly. Generated WARNING block flags endpoints (NEW) without matching screen — developer should manually add modal tasks.
- **`/swagger-emit` skill** is invoked by the orchestrator (main session) AFTER `contract-definer` reports `CONTRACT-DEFINER COMPLETE` for FE↔BE finalization. Reads FINALIZED `specs/{feature}/api-contract.md`, emits `specs/{feature}/swagger.json` (OpenAPI 3.0) with pwsh JSON validation + LLM self-check vs api-contract.md. Retry cap 2; manual fallback suggestion (`npx swagger-cli validate`) on 3rd failure. The static swagger.json is the input for `fe-api-client-writer` and `contract-drift-check` in subsequent Waves.
- **contract-drift-check** is the final pipeline gate. Run only AFTER both `ARCHITECTURE-GUARD COMPLETE` (BE Wave) and `FE-TEST-WRITER COMPLETE` (FE Wave) for the same feature. Refuses to run if either marker is missing, if `specs/{feature}/api-contract.md` is missing (`be-only` / `direct-edit` features skip this gate entirely), or if `Status:` is not `FINALIZED`. The developer owns the backend process lifecycle — the agent fetches `http://localhost:8080/swagger/v1/swagger.json` but does not start the backend; on connection refused it instructs the developer and waits for `ready`, then retries once. On `Status: BLOCKED`, repair loop: for each `module-writer ({Module})` entry re-invoke `module-writer` for that module with violation description → tail pipeline (`migration-agent` if `Schema changes != NONE` → `quality-reviewer` → `integration-test-writer`); for each `contract-definer --force` entry re-invoke `contract-definer` → `/swagger-emit` → `fe-api-client-writer`. After all repairs, re-invoke `contract-drift-check` from scratch. Cap 2 iterations; on the 3rd failure escalate to the user. Skip the loop entirely for items the developer marked as `Challenged (no resolution)`.

### Adding a new agent

1. Use `agent-architect` to design and produce the file in `.claude/agents/`.
2. After approval, update the table above with one-line "When to delegate".
3. Naming: module-specific `{module}-{purpose}.md`, cross-module `{purpose}.md`. Root only — no nested folders.
