# Implementation Plan: Resources lifecycle events and schema soft delete

**Branch**: `015-resource-time-buffer` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/015-resource-time-buffer/spec.md`; discovery record in `runs/015-resource-time-buffer/` (facts F-1..F-8, decisions DEC-3/4/5, assumptions ASM-3..19).

## Summary

The Resources module starts publishing four integration events (`ResourceSchemaCreatedEvent`, `ResourceInstanceCreatedEvent`, `ResourceSchemaDeletedEvent`, `ResourceInstanceDeletedEvent`) from its four existing command handlers, after `SaveChangesAsync`, exactly as ManagementGroups does today (F-6). `DELETE /resources/types/{id}` changes from a hard delete to a soft delete using the entity's existing `Delete(now)` (DEC-5), which requires the `(GroupId, Name)` unique index to become partial in the story's single migration (F-8). No HTTP contract, no frontend, no new column.

## Technical Context

**Language/Version**: C# 13 / .NET 10 / ASP.NET Core 10  
**Primary Dependencies**: EF Core 10 (Npgsql), in-house `IMessageBroker` (`InMemoryMessageBroker`, async in-process dispatcher, outbox disabled — F-7), `IDispatcher`, xUnit  
**Storage**: PostgreSQL 17, schema `resources`; one migration (index change only)  
**Testing**: xUnit integration tests in `ThingsBooksy.Modules.Resources.IntegrationTests` against `ThingsBooksyWebAppFactory`; `[Trait("AC","AC-n")]` per acceptance test; red-first proof via `tools/fleet/red-first-prover.js`  
**Target Platform**: Linux container (Docker), single deployable  
**Project Type**: modular-monolith web service (backend only for this story)  
**Performance Goals**: N/A — one extra `PublishAsync` per command; schema delete stays one bulk UPDATE + one row update  
**Constraints**: publish strictly after `SaveChangesAsync` (FR-005); no per-instance events on schema delete (FR-003); `GroupDeleted` bulk path untouched (FR-011); constructor of handlers ≤ 4 params  
**Scale/Scope**: 4 event records, 4 handlers touched, 1 entity method reused, 1 EF configuration line, 1 migration, ~7 acceptance tests + 2 rewritten tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Article | Check | Status |
|---|---|---|
| I Modular Monolith | Only `Resources.Core`, `Resources.Migrations`, `Resources.IntegrationTests`, `Shared.Abstractions`, `Shared.IntegrationTests` change; no module references another | PASS |
| II Simplified DDD | Events published from `Core` command handlers; no new layer | PASS |
| III Minimal API | No endpoint change | PASS (n/a) |
| IV Events | Contracts in `Shared.Abstractions/Events/Resources/`, records with primitive/Guid fields only; published via `IMessageBroker` | PASS |
| V Test-First | Acceptance tests written after skeleton (event records + EF config + migration) and before handler changes; AC tags; red-first | PASS (plan enforces order) |
| VI Persistence | Single migration `SoftDeleteResourceTypeUniqueIndex`; schema `resources` already set; `database update` by developer only | PASS |
| VII YAGNI | No new package; test observer is one decorator class in the test project | PASS |
| IX Entities | `ResourceType.Delete(now)` already exists; no new setters | PASS |
| X GUID v7 | No new identifiers generated | PASS |
| XI DataProvider | Handlers keep `IXxxDataProvider`; `IMessageBroker` is a broker, not a DbContext; `DeleteResourceTypeCommandDataProvider.RemoveResourceType` becomes unused → removed | PASS |
| XII Commands in endpoints | No request change | PASS (n/a) |
| XIII Naming | Event names per ASM-5 (`…Event` suffix — deviation from `GroupCreated` style, owner-visible assumption, not a constitution rule) | PASS |
| XIV InternalsVisibleTo | Unchanged; test project already listed | PASS |

No violations → Complexity Tracking not needed.

## Project Structure

### Documentation (this feature)

```text
specs/015-resource-time-buffer/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions taken from discovery (no open unknowns)
├── data-model.md        # Phase 1 — entity/event shapes and state transitions
├── quickstart.md        # Phase 1 — how to build, migrate, run the AC tests
├── contracts/
│   └── events.md        # Phase 1 — the four event contracts (C#) + HTTP contract statement (no change)
└── tasks.md             # Phase 2 — /speckit-tasks
```

### Source Code (repository root)

```text
backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/Resources/      # NEW folder
├── ResourceSchemaCreatedEvent.cs
├── ResourceSchemaDeletedEvent.cs
├── ResourceInstanceCreatedEvent.cs
└── ResourceInstanceDeletedEvent.cs

backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/
├── DAL/Configurations/ResourceTypeConfiguration.cs                         # index → .HasFilter("\"DeletedAt\" IS NULL")
├── Features/CreateResourceType/CreateResourceTypeCommandHandler.cs         # + IMessageBroker, publish after SaveChangesAsync
├── Features/CreateResourceInstance/CreateResourceInstanceCommandHandler.cs # + IMessageBroker, publish
├── Features/DeleteResourceType/DeleteResourceTypeCommandHandler.cs         # Delete(now) instead of Remove; + publish
├── Features/DeleteResourceType/DataProviders/IDeleteResourceTypeCommandDataProvider.cs  # drop RemoveResourceType
├── Features/DeleteResourceType/DataProviders/DeleteResourceTypeCommandDataProvider.cs   # drop RemoveResourceType
└── Features/DeleteResourceInstance/DeleteResourceInstanceCommandHandler.cs # + IMessageBroker, publish

backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Migrations/Migrations/
└── <timestamp>_SoftDeleteResourceTypeUniqueIndex.cs (+ snapshot)          # generated by dotnet ef

backend/src/Shared/ThingsBooksy.Shared.IntegrationTests/
├── ThingsBooksyWebAppFactory.cs                                            # ConfigureTestServices: decorate IMessageBroker
└── Messaging/RecordingMessageBroker.cs                                     # NEW: records IMessage, forwards to inner broker

backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests/
├── Events/ResourceLifecycleEventTests.cs                                   # NEW: AC-3, AC-4, AC-5, AC-6, AC-7
├── ResourceTypes/SoftDeleteResourceTypeTests.cs                            # NEW: AC-8, AC-9
├── ResourceTypes/UpdateDeleteResourceTypeTests.cs                          # lines 185, 283 → soft-delete assertions
└── Clients/ResourcesTestClient.cs                                          # + helper to read a type with IgnoreQueryFilters (if missing)
```

**Structure Decision**: web-service layout already in place (`backend/src/Modules/*`, `backend/src/Shared/*`); the story adds one folder in Shared.Abstractions and one test helper in Shared.IntegrationTests; everything else edits existing files.

## Implementation order (drives tasks.md)

1. **Skeleton** (no behaviour): the four event records; `ResourceTypeConfiguration` index filter; migration `SoftDeleteResourceTypeUniqueIndex` (developer runs `dotnet ef migrations add`, checks the generated `Up()` drops and recreates `IX_resource_types_GroupId_Name` with `filter: "\"DeletedAt\" IS NULL"`); `RecordingMessageBroker` + factory registration.
2. **Acceptance tests (RED)**: `ResourceLifecycleEventTests` (AC-3..AC-7), `SoftDeleteResourceTypeTests` (AC-8, AC-9); rewrite the two hard-delete assertions. Build passes, tests fail (`red-first-prover`).
3. **Behaviour (GREEN)**: inject `IMessageBroker` and publish in the four handlers; switch `DeleteResourceTypeCommandHandler` to `resourceType.Delete(now)`; remove `RemoveResourceType`.
4. **Second pass**: branch coverage (`coverage-gaps.js`) — e.g. delete of an already-deleted schema publishes nothing (ASM-19, tag `AC-5`/`UNSPECIFIED` as appropriate).
5. **Gate**: `dotnet format`, `gate.js` (build, tests, swagger re-export → contract-diff CLEAR, ac-matrix 100 %).

## Complexity Tracking

Not applicable — no constitution violations.
