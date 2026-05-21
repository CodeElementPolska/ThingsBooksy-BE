# ThingsBooksy

> **Business description coming soon.**

---

## Architecture

ThingsBooksy is a **Modular Monolith** — a single deployable artifact composed of isolated, independently-developed modules. Each module owns its domain, its database schema, and its HTTP surface. Modules never reference each other directly; all cross-module communication goes through events (fire-and-forget) or queries (request/response) via shared infrastructure abstractions.

```
┌───────────────────────────────────────────────────────────────┐
│                   ThingsBooksy.Bootstrapper                    │
│      Discovers and composes all modules at startup via         │
│      reflection (IModule interface)                            │
└───────────────────────────────┬───────────────────────────────┘
                                │
          ┌─────────────────────┼─────────────────────┐
          ▼                     ▼                     ▼
┌───────────────────┐ ┌───────────────────┐ ┌───────────────────┐
│       Users       │ │  ManagementGroups │ │     Resources     │
│       .Api        │ │       .Api        │ │       .Api        │
│       .Core       │ │       .Core       │ │       .Core       │
│    .Migrations    │ │    .Migrations    │ │    .Migrations    │
│ .IntegrationTests │ │ .IntegrationTests │ │ .IntegrationTests │
└─────────┬─────────┘ └─────────┬─────────┘ └─────────┬─────────┘
          │                     │                     │
          └─────────────────────┼─────────────────────┘
                                │
                   ┌────────────▼────────────┐
                   │   Shared.Abstractions   │
                   │   Events / Contracts    │
                   │   IDispatcher           │
                   │   IMessageBroker        │
                   └─────────────────────────┘
```

Each module follows a consistent project structure:

```
src/Modules/{ModuleName}/
├── {ModuleName}.Api               # Minimal API endpoints, DTOs, module config
├── {ModuleName}.Core              # Domain entities, EF DbContext, handlers
└── {ModuleName}.Migrations        # EF Core migrations (optional)

tests/
└── {ModuleName}.IntegrationTests  # Integration tests
```

---

## Modules

| Module | Description |
|---|---|
| **Users** | Authentication and user account management |
| **ManagementGroups** | Creation and management of groups and their memberships |
| **Resources** | EAV-based resource schema — resource types with property definitions, and resource instances with typed attribute values |

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 |
| Language | C# 13 |
| Database | PostgreSQL 17 (Docker) |
| ORM | Entity Framework Core 10 |
| Auth | JWT Bearer + AES-256 |
| API docs | Swashbuckle / Swagger UI (`/swagger`) |
| Logging | Serilog |
| Containerization | Docker / docker-compose |

---

## Rules & Conventions

All conventions live in `.claude/conventions/`. Every agent that writes or reviews code must follow them exactly.

| Convention | Summary |
|---|---|
| **domain-entity-design** | All entity properties use `private set`; entities are created via a static `Create()` factory (max 4 params) and mutated through named domain methods only. Read-models use `Upsert()` instead of `Create()`. |
| **naming-commands-queries-handlers-results** | Commands, queries, and handlers use full module-scoped PascalCase with their respective suffixes (`Command`, `CommandHandler`, `Query`, `QueryHandler`). Result types are derived mechanically from the handler name by stripping `Handler`. |
| **data-provider-pattern** | Handlers never inject `DbContext` directly — each handler depends on a dedicated `IXxxDataProvider` interface co-located in its feature folder. Providers are registered automatically via `AddDataProviders` called once per module. |
| **data-provider-query-syntax** | DataProvider methods use parenthesized LINQ query syntax with materialization chained outside — `(from ... select ...).ToListAsync(ct)` — for joins and group-by; method syntax is allowed only for simple single-table queries. |
| **command-construction-in-endpoints** | Commands are never bound directly from the HTTP request body; a request DTO (`{Module}.Api/Requests/`) holds only client-permitted fields, and the endpoint constructs the command explicitly. |
| **minimal-api-endpoints** | All HTTP endpoints use Minimal API (no MVC controllers), registered in `Expose()` with a `/{module-name}/...` route prefix. `AddEndpointsApiExplorer()` must be called in `Register()`. |
| **dispatcher-usage** | All commands go through `IDispatcher.SendAsync`, all queries through `IDispatcher.QueryAsync`. Direct handler calls and MediatR are forbidden. |
| **ef-schema-isolation** | Every module `DbContext` must call `modelBuilder.HasDefaultSchema(...)` with the module's lowercase snake_case name. Using `"public"` or omitting the call is forbidden. |
| **internals-visible-to** | Every `.Core` project must declare four `InternalsVisibleTo` attributes: for `.Api`, `.Migrations`, `.IntegrationTests`, and `DynamicProxyGenAssembly2`. |
| **integration-test-naming** | Test methods follow `{Action}{Entity}_{Condition}_{Result}` — the result segment is an HTTP status code for simple status assertions or a behavioral description when the test also asserts side effects or DB state. |
| **integration-test-infrastructure** | Each module defines a `TestClient` (HTTP + DB methods), entity `Factory` classes (direct DB insertion), and a module-scoped `IntegrationTestCollection`. Each test creates its own user to avoid shared state. |

---

## AI

ThingsBooksy uses a fleet of purpose-built Claude Code agents that automate the full feature-delivery pipeline — from product discovery through architecture validation, across both backend and frontend, with a final cross-cutting drift gate.

### Development Workflow

The main session acts as orchestrator. After planning, the pipeline forks into two parallel Waves (BE + FE) that share a single negotiated FE↔BE contract, then rejoins for a final drift check.

```
                              product-strategist
                              (Scope: be-only | fe-only | both | direct-edit)
                                      │  handoff brief
                                      ▼
                                  doc-writer                          [skipped for direct-edit]
                                  (writes ADR)
                                      │
                                      ▼
                     /speckit-specify → /speckit-plan
                                      │
                                      ▼
                          /api-contract-emit (auto hook)
                          → specs/{feature}/api-contract.md (DRAFT)
                                      │
                                      ▼
                              /speckit-tasks
                                      │
                                      ▼
                          /fe-tasks-augment (auto hook)
                                      │
                                      ▼
                              plan-validator
                          (GO / NO-GO + EXECUTION MAP:
                           BE Waves + FE Wave per component)
                                      │ GO
                                      ▼
                              contract-definer
                       (1) BE↔BE IEvent / IModuleClient
                       (2) FE↔BE api-contract.md → FINALIZED
                                      │
                                      ▼
                                /swagger-emit
                        → specs/{feature}/swagger.json
                                      │
              ┌───────────────────────┴───────────────────────┐
              ▼                                               ▼
         === BE WAVE ===                                === FE WAVE ===
                                                       fe-api-client-writer
                                                       (regenerates src/app/api/)
                                                              │
       module-scaffolder                                      ▼
       (new modules only)                              html-extractor
              │                                        (Claude Design HTML
              ▼                                         → component plan)
         module-writer                                        │
       (per module, parallel)                                 ▼
              │                                        fe-plan-validator
              ▼                                        (GO / NO-GO)
       migration-agent                                        │ GO
       (only if Schema changes)                               ▼
              │                                      fe-component-writer
              ▼                                       (one per component,
       quality-reviewer                                parallel)
       (interactive review)                                   │
              │                                               ▼
              ▼                                      fe-quality-reviewer
       integration-test-writer                       (per component)
              │                                               │
              ▼                                               ▼
       architecture-guard                              fe-route-writer
       (solution-wide, after all                       (one per feature)
        modules of the Wave)                                  │
              │                                               ▼
              │                                        fe-test-writer
              │                                       (Vitest integration)
              ▼                                               ▼
              └───────────────────────┬───────────────────────┘
                                      ▼
                              contract-drift-check
                       (runtime swagger vs static swagger.json,
                        BE drift → module-writer,
                        contract drift → contract-definer --force)
                                      │
                                      ▼
                                    DONE
```

**Repair loops.** Three agents emit BLOCKERs that trigger an automatic repair loop (cap 2 iterations, escalate to user on the 3rd):

- `quality-reviewer` / `fe-quality-reviewer` → re-invoke the matching writer for the same target, then walk the tail of the pipeline.
- `architecture-guard` → re-invoke `module-writer` with the violation, then tail (`migration-agent` → `quality-reviewer` → `integration-test-writer` → `architecture-guard`).
- `contract-drift-check` → BE drift re-invokes `module-writer` (+ tail); contract drift re-invokes `contract-definer` → `/swagger-emit` → `fe-api-client-writer`.

Items the developer explicitly marks as `Challenged (no resolution)` skip the loop.

---

### Agent Reference

#### Cross-cutting

| Agent | Responsibility |
|---|---|
| **agent-architect** | Designs new agents for the fleet, challenges weak proposals, and writes the agent file to `.claude/agents/`. Used only when growing the fleet itself. |
| **convention-writer** | Interactive author of new coding conventions — challenges the proposal in dialogue and writes the approved rule to `.claude/conventions/`. |

#### Discovery & planning (Stage 0–2)

| Agent | Responsibility |
|---|---|
| **product-strategist** | Scope-routed feature interview (Phase 0 classifies `be-only`/`fe-only`/`both`/`direct-edit`, then runs business + BE + FE phases as needed). Produces the handoff brief that feeds `/speckit-specify`; `direct-edit` short-circuits with a file pointer. |
| **doc-writer** | Writes Architecture Decision Records (ADRs) to `.specify/decisions/` immediately after `product-strategist` returns a brief. Skipped for `direct-edit` scope and reused for Stage-3 pivots. |
| **plan-validator** | Runs deterministic consistency checks across `spec.md`, `plan.md`, `tasks.md` and the auto-emitted `api-contract.md`. Returns `GO`/`NO-GO` + an EXECUTION MAP that fans out into BE Waves and a FE Wave with per-component listings. |
| **contract-definer** | Negotiates and finalizes both contract layers: (1) BE↔BE `IEvent` / `IModuleClient` types in `Shared.Abstractions`, (2) FE↔BE `api-contract.md` (DRAFT → FINALIZED). Signals the orchestrator to run `/swagger-emit` next. |

#### Backend Wave

| Agent | Responsibility |
|---|---|
| **module-scaffolder** | Creates the empty 4-project scaffold for a new module (`{Name}.Api/.Core/.Migrations/.IntegrationTests`), registers it in the `.slnx`, and patches `ThingsBooksyWebAppFactory`. Runs only when `backend/src/Modules/{Name}/` does not yet exist. |
| **module-writer** | Implements a single module's assigned task IDs: entities, `DbContext`, command/query handlers, event subscriptions, DTOs, Minimal API endpoints, `IModule` registration. One instance per module; independent modules run in parallel. |
| **migration-agent** | Runs `dotnet ef migrations add` for a module after `module-writer` reports `Schema changes != NONE` and reports a human-readable schema summary. Skipped entirely when `Schema changes: NONE`. |
| **quality-reviewer** | Interactive read-only review of module source against ThingsBooksy architecture rules, `spec.md`, `plan.md`, and `tasks.md`. One issue at a time; emits BLOCKERs that may trigger the repair loop. |
| **integration-test-writer** | Writes module integration tests (`TestClient`, per-entity Factories, feature-grouped test classes) and runs `dotnet format` + `dotnet test`. Migrations are applied automatically by `ThingsBooksyWebAppFactory`. |
| **architecture-guard** | Post-Wave solution-wide scan for violations no per-module agent can detect: direct module references, orphan events / `IModuleClient` routes, missing bootstrapper registrations, duplicate EF schemas, incomplete `InternalsVisibleTo`. Interactive, one violation at a time. |

#### Frontend Wave

| Agent | Responsibility |
|---|---|
| **fe-api-client-writer** | Regenerates the typed Angular HTTP client into `frontend/src/app/api/` from the static `specs/{feature}/swagger.json` only — no runtime fallback. Fails fast if the swagger file is missing, blocking the FE Wave. |
| **html-extractor** | Analyzes a Claude Design `.html` artifact against `spec.md`, FINALIZED `api-contract.md`, and `tasks.md`. Produces a component plan with autopopulated endpoint mapping and a UI↔endpoint↔FE-Surface sanity check; never writes code. |
| **fe-plan-validator** | Read-only interactive gate after `html-extractor` Phase 6 approval. Runs 7 deterministic checks against the frontend sources and produces `VERDICT: GO/NO-GO` before any FE component is written. |
| **fe-component-writer** | Implements exactly one Angular component per invocation (`.ts`, `.html`, `.scss`, `.spec.ts`) and verifies `ng build` passes. Independent components run in parallel within the Wave. |
| **fe-quality-reviewer** | Per-component interactive read-only review against `.claude/conventions/angular-*.md` (12 checks). Runs after every `fe-component-writer` `Build: PASSED` and before `fe-route-writer`. |
| **fe-route-writer** | Creates or updates `{feature}.routes.ts`, registers it in `app.routes.ts`, and verifies `ng build` passes. One invocation per feature; runs only after every component has built and been reviewed. |
| **fe-test-writer** | Writes feature-level Vitest integration tests against the FINALIZED `api-contract.md` (HTTP mocks, signal assertions, routing, form flows) and runs `npm test -- --run`. Stops with `Blocked` rather than editing production code on failure. |

#### Final gate

| Agent | Responsibility |
|---|---|
| **contract-drift-check** | Final pipeline gate after both `ARCHITECTURE-GUARD COMPLETE` and `FE-TEST-WRITER COMPLETE`. Diffs the live `http://localhost:8080/swagger/v1/swagger.json` against the static `specs/{feature}/swagger.json`, classifies each drift (BE drift → `module-writer`, contract drift → `contract-definer --force`), and produces `Status: ALL CLEAR | BLOCKED`. |
