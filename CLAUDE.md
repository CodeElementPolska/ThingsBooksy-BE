# CLAUDE.md — ThingsBooksy

Modular Monolith (.NET 10 backend + Angular 21 frontend, single deployable). This file is an **index**, not a rulebook. Authoritative rules live in the linked files.

---

## Quick navigation

- **Architecture rules** → `.specify/memory/constitution.md`
- **Backend conventions** → `.claude/conventions/*.md` (table below)
- **Frontend conventions** → `.claude/conventions/angular-*.md` (table below)
- **Agent fleet (design in progress)** → `docs/agent-fleet-v4/` — do not read files or agents prefixed `obsolete-`
- **Bootstrapper** → `backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`
- **Shared contracts** → `backend/src/Shared/ThingsBooksy.Shared.Abstractions`

---

## Hard non-negotiable rules

1. **GUID v7 only** — `Guid.CreateVersion7()`. `Guid.NewGuid()` is forbidden.
2. **No direct cross-module references** — events via `IMessageBroker`, queries via `IModuleClient`. Never query another module's DB.
3. **Tests required** — new business logic without tests does not merge.

All other rules (entity encapsulation, naming, DI pattern, schema isolation, etc.) live in `.claude/conventions/` and are authoritative there.

---

## Commands

### Backend
- Build: `dotnet build backend/ThingsBooksy.slnx`
- Run: `dotnet run --project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`
- Test all: `dotnet test backend/ThingsBooksy.slnx` (target a specific `.csproj` for faster feedback)
- Format: `dotnet format backend/ThingsBooksy.slnx` — also runs automatically on staged `.cs` files via Husky.NET pre-commit hook (`.husky/task-runner.json`). Run manually only when iterating mid-session before a commit.
- EF migration add: `dotnet ef migrations add {Name} --project backend/src/Modules/{Module}/{Module}.Migrations --startup-project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`
- EF migration apply (dev only): `dotnet ef database update --project ... --startup-project ...` — integration tests apply migrations automatically via `ThingsBooksyWebAppFactory`; manual `database update` is needed only for local Docker DB.
- PowerShell scripts: call `powershell.exe -File ...` (`pwsh` is not on PATH in Git Bash).

### Frontend
- Install: `cd frontend && npm install`
- Dev server: `cd frontend && npm start` (localhost:4200)
- Build: `cd frontend && npm run build`
- Tests: `cd frontend && npm test`
- Lint: `cd frontend && npm run lint`

### Docker
- `wsl docker compose up --build` (WSL prefix required on Windows). App: `localhost:8080`, Swagger: `localhost:8080/swagger`.

---

## Tech stack

.NET 10 / ASP.NET Core 10 / C# 13 · PostgreSQL 17 · EF Core 10 · JWT Bearer + AES-256 · Serilog · Swashbuckle · Solution: `.slnx` · Angular 21 (standalone, signals, Reactive Forms, Vitest) · Docker / WSL.

---

## Backend conventions

| File | Rule (one line) |
|---|---|
| `domain-entity-design.md` | Entity encapsulation: `private set`, private ctor, `Create`/`Upsert` factory, max 4 params |
| `naming-commands-queries-handlers-results.md` | PascalCase, full names, fixed suffixes, one class per file |
| `data-provider-pattern.md` | Handlers depend on `IXxxDataProvider`, never `DbContext`; `AddDataProviders` per module |
| `data-provider-query-syntax.md` | Parenthesized LINQ query syntax for joins/group by |
| `command-construction-in-endpoints.md` | Request DTO in `.Api/Requests/`; command constructed in endpoint, never bound from body |
| `minimal-api-endpoints.md` | Minimal API only; endpoints in `Expose()`; `/{module-name}/...` route prefix |
| `dispatcher-usage.md` | `IDispatcher.SendAsync` / `QueryAsync`; no MediatR |
| `ef-schema-isolation.md` | `HasDefaultSchema(lowercase_snake_case)` mandatory; never `"public"` |
| `internals-visible-to.md` | 4 `InternalsVisibleTo` per `.Core`: `.Api`, `.Migrations`, `.IntegrationTests`, `DynamicProxyGenAssembly2` |
| `integration-test-naming.md` | `{Action}{Entity}_{Condition}_{Result}` |
| `integration-test-infrastructure.md` | TestClient + per-entity Factory + IntegrationTestCollection per module |

---

## Frontend conventions

| File | Rule (one line) |
|---|---|
| `angular-folder-structure.md` | `core/`, `features/`, `shared/`, `src/app/api/`; `provideCore()` registers interceptors; lazy-load features |
| `angular-component-design.md` | Standalone, `inject()`, `signal()`, built-in control flow, `tb-` selector prefix |
| `angular-http-pattern.md` | Feature services return `Observable<T>`; `withFetch()` mandatory; interceptors for 401/500 |
| `angular-forms-pattern.md` | Reactive Forms, typed `FormControl<T>`, `nonNullable: true`, async validators via `timer(300) + first()` |
| `angular-styling.md` | SCSS + CSS custom properties in `_tokens.scss`; no `::ng-deep`, no hard-coded values, mobile-first |
| `angular-routing.md` | One `{feature}.routes.ts` per feature; export `{feature}Routes` (camelCase); lazy-load via `loadComponent`/`loadChildren`; max depth 2 |

---

## Workflow

- **Small bugfixes / single-file changes:** edit directly. No SpecKit, no agent pipeline.
- **Features:** the agent workflow is being redesigned — see `docs/agent-fleet-v4/workflow.md` and `decisions.md`. Until it ships, use SpecKit skills (`/speckit-plan`, `/speckit-tasks`, `/speckit-clarify`, `/speckit-analyze`) manually on a `NNN-slug` branch; `/speckit-implement` is not used. The previous fleet is archived in `docs/obsolete-fleet.md` and must not be used as a reference.

---

## Testing policy

- Integration tests: EF Core, migrations, DB. Projects: `{Module}.IntegrationTests` under each module (Arrange = seed via EF + domain factories, Act = HTTP, Assert = re-read via EF).
- Unit tests: domain entities, value objects, handlers — target projects `{Module}.Tests.Unit`; **these projects do not exist yet** (empty directories only) and must be created before unit tests are written.
- Test-first for new features and business logic changes.
