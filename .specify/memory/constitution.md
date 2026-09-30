<!--
Sync Impact Report — 2026-09-24
Version change: 1.2.0 → 1.3.0 (MINOR: Article V materially expanded; Governance section extended)
Modified principles: I. Modular Monolith Architecture (project list), II. Simplified DDD (project list),
  V. Test-First Approach (test style, acceptance-before-behaviour, AC tags, red-first, two tester passes),
  VI. Persistence and Migrations (database update by developer only; one regenerated migration per story)
Modified sections: Development Workflow (agent fleet v4 pointer, SpecKit usage), Docker and Local Environment
  (docker from PowerShell), Governance (agent fleet: rule_ref, BLOCKER, enforced-by, generated/ and specs/ rules)
Added sections: none. Removed sections: none.
Templates: ⚠ .specify/templates/tasks-template.md still uses generic tests/integration/test_[name].py paths
  (pre-existing; not changed here) · ✅ plan-template.md Constitution Check gate remains valid ·
  ✅ CLAUDE.md already points to docs/agent-fleet-v4/ · ✅ docs/agent-fleet-v4/decisions.md D-4…D-15 are the source
Follow-up TODOs: create {ModuleName}.Tests.Unit projects (scaffold-module script); tag articles with enforced-by
  when the deferred static-analysis epic lands (docs/backlog/deferred-static-analysis-sonarqube.md)
-->
# ThingsBooksy Constitution

## Core Principles

### I. Modular Monolith Architecture (NON-NEGOTIABLE)
The application is built as a **Modular Monolith**: a single deployable artifact composed of independent, self-contained modules.
- Each module lives in `backend/src/Modules/{ModuleName}/` and contains two **source** projects — `{Name}.Api` and `{Name}.Core` — plus `{Name}.Migrations`, `{Name}.IntegrationTests` and `{Name}.Tests.Unit`
- Modules **must never** directly reference each other — communication happens exclusively through `IMessageBroker` (events) or `IModuleClient` (queries)
- Shared infrastructure belongs exclusively to `backend/src/Shared/` — no cross-module dependencies
- Each module exposes its public contract via `IModule` and registers endpoints in `Expose(IEndpointRouteBuilder)`

### II. Simplified DDD (NON-NEGOTIABLE)
The project applies **Simplified DDD** — no separate Application or Infrastructure layers.
- Production code lives in exactly two projects: `{Name}.Api` (HTTP layer, Minimal API) and `{Name}.Core` (domain, persistence, commands, queries, events); migrations and tests live in their own projects (see I, V, VI)
- `Core` contains: domain entities, EF `DbContext`, command/query handlers, domain events, value objects
- `Api` contains: `IModule` registration, endpoint definitions, DTOs (request/response records), module JSON config file
- No MediatR — commands/queries are plain C# classes dispatched via `IDispatcher`

### III. Minimal API Endpoints (NON-NEGOTIABLE)
All HTTP endpoints are defined using **ASP.NET Core Minimal APIs** — no MVC controllers.
- Endpoints registered in `{ModuleName}Module.Expose()` for each module
- Route prefix pattern: `/{module-name}/...`
- Always call `services.AddEndpointsApiExplorer()` so Swagger discovers Minimal API endpoints
- Swagger/OpenAPI must be available at `/swagger` in all environments

### IV. Inter-Module Communication via Events
Modules communicate through domain events published via `IMessageBroker`.
- Publishing: `await _messageBroker.PublishAsync(new SomethingHappenedEvent(...))`
- Subscribing: implement `IEventHandler<TEvent>` and register it in the module's `Register(IServiceCollection)`
- If a module needs data from another module, it subscribes to events and stores a **read model** (local copy) — it never queries another module's database
- Event contracts live in `backend/src/Shared/ThingsBooksy.Shared.Abstractions/` — no module-specific types in event bodies

### V. Test-First Approach
New features and bug fixes require tests before implementation.
- Test projects: `{ModuleName}.IntegrationTests` (acceptance/integration) and `{ModuleName}.Tests.Unit` (unit; created by the module scaffold when missing)
- **Integration test style**: Arrange = seed data through EF Core and domain factories (per-entity Factory in the test project), Act = HTTP call through the test client, Assert = re-read from the database through EF Core and compare with the response. Seeding through the public API is forbidden — it couples every test to every endpoint
- **Acceptance tests come before behaviour**: they are written after the entity skeleton (entities, EF configuration, migration) exists and before any handler, endpoint or domain method is implemented; they MUST compile and MUST fail before the behaviour is implemented (red-first proof). A test that passes before the behaviour exists proves nothing and is rejected
- **Traceability**: every acceptance test carries `[Trait("AC", "AC-n")]` (frontend: `it("[AC-n] …")`) linking it to an acceptance criterion of the story; every acceptance criterion MUST have at least one test
- **Unit tests** for domain logic (entities, value objects, handlers) are written by the implementer alongside the behaviour
- After the behaviour is green, a second test pass covers branches of the new code not exercised by the acceptance tests; each such test is tagged with an `AC-n` or, when the behaviour has no criterion, `UNSPECIFIED` — an `UNSPECIFIED` test is a decision for the owner, never a silently accepted behaviour
- No tests = no merge for business logic changes

### VI. Persistence and Migrations
Each module has its own **EF Core DbContext** with schema isolation.
- Schema naming: lowercase snake_case of the module name — `"bookings"`, `"management_groups"`, `"users"`, etc.
- Every `DbContext` must call `modelBuilder.HasDefaultSchema(...)` — using `"public"` or omitting the call is forbidden (causes cross-module table collisions and silent Respawn data loss in tests)
- Migrations live in a dedicated `{ModuleName}.Migrations` project
- Migration command: `dotnet ef migrations add {Name} --project backend/src/Modules/{M}/{M}.Migrations --startup-project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`
- `dotnet ef database update` is run only by the developer against the local database — never by an agent
- Within one story a module has a single migration: when the model changes later in the story, the migration is removed and regenerated (`migrations remove` + `migrations add`), never stacked

### VII. Simplicity and YAGNI
Do not add abstractions, patterns, or packages unless they solve a current problem.
- Prefer built-in .NET features over external libraries
- No MediatR, AutoMapper, or heavy frameworks — plain C# dispatch and manual mapping
- Configuration: `module.{name}.json` per module, merged by `ConfigureModules()` at startup

### VIII. Code Formatting
Code is formatted with the **`dotnet format`** tool built into the .NET SDK.
- Run **before every commit**: `dotnet format`
- Formatting covers: indentation, whitespace, `using` organization, code style aligned with `.editorconfig`
- If `.editorconfig` does not exist at the root — create it
- CI/CD should verify formatting: `dotnet format --verify-no-changes`

### IX. Domain Entities — Encapsulation (NON-NEGOTIABLE)
Domain entities **must** protect their state through full encapsulation.
- **Private setters**: all entity properties have `private set` — state changes only through entity methods
- **Private constructor**: the entity constructor is `private` — the only way to create an instance is via the static factory method `Create(...)`
- **Command objects as parameters**: `Create` and `Update` receive the command object as the first parameter, not individual primitives. Resolved external data (foreign keys, timestamps) is added as extra parameters after the command. Maximum **4 parameters total**.
- **Domain methods**: all state mutations are performed through public entity methods (`Update`, `Delete`, `Restore`, etc.)
- **Read-models** use `internal static Upsert(TEvent)` as factory — not `Create` — to signal that the method covers both creation and update semantics.
- Full rules: `.claude/conventions/domain-entity-design.md`
- Pattern example:
  ```csharp
  internal class Booking
  {
      public Guid Id { get; private set; }
      public string Name { get; private set; } = null!;
      public Guid OwnerId { get; private set; }

      private Booking() { }

      public static Booking Create(CreateBookingCommand command)
          => new() { Id = Guid.CreateVersion7(), Name = command.Name, OwnerId = command.OwnerId };

      public void Update(UpdateBookingCommand command) => Name = command.Name;
  }
  ```

### X. Identifiers — GUID v7 (NON-NEGOTIABLE)
All identifiers **generated by the application** must use `Guid.CreateVersion7()`.
- `Guid.NewGuid()` is **forbidden** — replace every occurrence with `Guid.CreateVersion7()`
- GUID v7 is time-ordered (better index performance in PostgreSQL) and monotonic
- New entity IDs are generated inside the entity's `Create(...)` method
- Identifiers referencing **an existing entity** (e.g. `OwnerId`, `GroupId`) may be accepted from outside — they are references, not new identifiers

### XI. DataProvider Pattern (NON-NEGOTIABLE)
Handlers (command and query) must **never** inject `DbContext` directly.
- Each handler depends on a dedicated `IXxxDataProvider` interface — named by stripping `Handler` and appending `DataProvider`
- Both interface and implementation live in `{Module}.Core/Features/{Feature}/DataProviders/`
- Each module calls `AddDataProviders([typeof(Extensions).Assembly])` once from its own `Extensions.cs` — no manual `.AddScoped` per provider
- Data provider methods that are a single awaitable expression return `Task` directly — no `async`/`await`
- Full rules: `.claude/conventions/data-provider-pattern.md` and `.claude/conventions/data-provider-query-syntax.md`

### XII. Command Construction in Endpoints (NON-NEGOTIABLE)
Commands must **never** be bound directly from the HTTP request body.
- Each endpoint with a body declares a request DTO `record` in `{Module}.Api/Requests/` containing only client-permitted fields
- The command is constructed explicitly in the endpoint lambda; server-sourced values (route params, JWT-derived IDs, `Guid.CreateVersion7()`) are injected at the call site
- This prevents clients from overriding server-sourced fields via JSON body injection
- Full rules: `.claude/conventions/command-construction-in-endpoints.md`

### XIII. Naming — Commands, Queries, Handlers, Results
All commands, queries, handlers, and result types follow mechanical naming rules.
- PascalCase everywhere; full unambiguous names including module/aggregate context
- Suffixes: `Command`, `CommandHandler`, `Query`, `QueryHandler`
- Result class name derived from handler name by stripping `Handler` — nothing else
- One class per file; file name equals class name exactly
- Full rules: `.claude/conventions/naming-commands-queries-handlers-results.md`

### XIV. Module Internal Visibility
Every `.Core` project must declare four `InternalsVisibleTo` attributes in `Extensions.cs`.
- `ThingsBooksy.Modules.{ModuleName}.Api`
- `ThingsBooksy.Modules.{ModuleName}.Migrations`
- `ThingsBooksy.Modules.{ModuleName}.IntegrationTests`
- `DynamicProxyGenAssembly2`
- Missing any one causes compilation errors or runtime mock failures
- Full rules: `.claude/conventions/internals-visible-to.md`

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 |
| Language | C# 13 |
| Database | PostgreSQL 17 (Docker) |
| ORM | Entity Framework Core 10 |
| Authentication | JWT Bearer + AES-256 symmetric encryption |
| Containerization | Docker / docker-compose (WSL locally) |
| API Docs | Swashbuckle / Swagger UI |
| Logging | Serilog |
| Solution Format | `.slnx` (VS 2022+) |

## Development Workflow

1. **New module**: create `{Name}.Api` + `{Name}.Core` + `{Name}.Migrations` + `{Name}.IntegrationTests` + `{Name}.Tests.Unit`, register in `Bootstrapper`, add `module.{name}.json`
2. **New feature**: the agent workflow in `docs/agent-fleet-v4/workflow.md` (business session → discovery session → delivery). SpecKit skills used inside it: `/speckit-plan`, `/speckit-tasks`, and `/speckit-clarify` / `/speckit-analyze` as lint; `/speckit-specify` and `/speckit-implement` are not used
3. **Story identity**: one identifier `NNN-slug` names the branch, `specs/NNN-slug/` and `runs/NNN-slug/`; SpecKit scripts resolve the feature from the branch name, so a mismatching branch silently targets another spec
4. **Database change**: add EF migration (see VI), update the local database, verify in Docker
5. **Before commit**: ensure the project builds, Swagger shows all endpoints, Docker Compose starts correctly

## Docker and Local Environment

- Local Docker is Docker Desktop with the WSL backend: from PowerShell or cmd call `docker` directly (`docker compose up --build`); `wsl docker …` works only inside a WSL distribution, and Git Bash can use neither form
- Environment: `ASPNETCORE_ENVIRONMENT=Docker` activates `appsettings.Docker.json`
- PostgreSQL connection string in `appsettings.Docker.json` must include username and password
- App available at `localhost:8080`, Swagger at `localhost:8080/swagger`

## Coding Conventions

Detailed, example-rich rules for recurring code patterns live in `.claude/conventions/`. Every agent that writes or reviews code must read the relevant files before acting. The constitution states *what* is non-negotiable; the convention files state *how* to implement it correctly.

| File | Covers |
|---|---|
| `domain-entity-design.md` | Entity structure, member order, factory/mutation signatures, read-model `Upsert` |
| `naming-commands-queries-handlers-results.md` | Naming rules for all CQRS types and result records |
| `data-provider-pattern.md` | IDataProvider per handler, registration via `AddDataProviders`, async eliding |
| `data-provider-query-syntax.md` | Parenthesized LINQ query syntax for joins and group-by |
| `command-construction-in-endpoints.md` | Request DTOs in `Requests/`, explicit command construction, no direct body binding |
| `minimal-api-endpoints.md` | No controllers, `Expose()` method, route prefix, `AddEndpointsApiExplorer()` |
| `dispatcher-usage.md` | `IDispatcher.SendAsync` / `QueryAsync`, no MediatR, no direct handler calls |
| `ef-schema-isolation.md` | `HasDefaultSchema` mandatory, lowercase snake_case, never `"public"` |
| `internals-visible-to.md` | Four `InternalsVisibleTo` declarations per `.Core` project |
| `integration-test-naming.md` | `{Action}{Entity}_{Condition}_{Result}` test method naming |
| `integration-test-infrastructure.md` | TestClient, Factory, IntegrationTestCollection patterns |

## Governance

- This constitution takes precedence over all other practices and conventions
- Changes require updating this file with a justification and a version increment
- All PRs must verify compliance with Modular Monolith and Simplified DDD principles
- Any deviation from the `AddEndpointsApiExplorer()` requirement is forbidden — Swagger compliance is mandatory
- **Agent fleet**: guards and reviewers of the agent workflow (`docs/agent-fleet-v4/`) cite this constitution by article number (`rule_ref: constitution#<article>`); a violation of an article is a BLOCKER that stops the delivery gate. Articles that can be checked by a machine (analyzers, architecture tests) will be tagged `enforced-by` once the deferred static-analysis epic lands (`docs/backlog/deferred-static-analysis-sonarqube.md`); until then they are reviewed by agents. Process rules of the workflow (owner decisions, triage, phases) are not part of this constitution
- **Sources of truth**: the API contract is the OpenAPI document generated from the built backend (`generated/swagger.base.json`) plus a per-story OpenAPI Overlay — never a hand-written description; `generated/` (swagger, capability map, core surface) is the source of truth about what the application can do today; `specs/NNN-slug/` is an immutable record once the story is closed

**Version**: 1.3.0 | **Ratified**: 2026-04-23 | **Last Amended**: 2026-09-24
