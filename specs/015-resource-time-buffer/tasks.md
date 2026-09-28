---

description: "Task list for 015-resource-time-buffer — Resources lifecycle events and schema soft delete"
---

# Tasks: Resources lifecycle events and schema soft delete

**Input**: Design documents from `/specs/015-resource-time-buffer/` (plan.md, spec.md, research.md, data-model.md, contracts/events.md, quickstart.md); discovery record in `runs/015-resource-time-buffer/`.
**Prerequisites**: plan.md, spec.md (AC-3..AC-9), decisions DEC-3/4/5 in `runs/015-resource-time-buffer/discovery/decisions.jsonl`.

**Tests**: REQUIRED (constitution V, test-first). Acceptance tests are written after the skeleton (Phase 2) and MUST fail before Phase 4/5 behaviour lands (`tools/fleet/red-first-prover.js`). Every acceptance test carries `[Trait("AC", "AC-n")]`.

**Organization**: Phase 1 setup → Phase 2 skeleton (foundational, no behaviour) → Phase 3 acceptance tests (RED) for both stories → Phase 4 US1 behaviour → Phase 5 US2 behaviour → Phase 6 polish/gates. Tests for both stories sit in one RED phase because the fleet's red-first proof runs once over the whole story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: [US1] events, [US2] schema soft delete
- File paths are repository-relative.

## Path Conventions

- Backend module: `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.{Core,Migrations,IntegrationTests}/`
- Shared: `backend/src/Shared/ThingsBooksy.Shared.{Abstractions,IntegrationTests}/`
- No frontend, no HTTP contract change (contracts/events.md).

---

## Phase 1: Setup

**Purpose**: confirm the working tree and tooling before any file changes.

- [ ] T001 Verify branch is `015-resource-time-buffer` and `dotnet build backend/ThingsBooksy.slnx` is green on the untouched tree; note the current last Resources migration is `20260514155140_AddResourceTypeUniqueAndCursorIndexes` in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Migrations/Migrations/`
- [ ] T002 Read the conventions that apply before writing code: `.claude/conventions/data-provider-pattern.md`, `.claude/conventions/integration-test-infrastructure.md`, `.claude/conventions/integration-test-naming.md`, `.claude/conventions/domain-entity-design.md`; read the reference publisher `backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.Core/Features/CreateManagementGroup/CreateManagementGroupCommandHandler.cs` (SaveChangesAsync → PublishAsync order) and the reference event `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/ManagementGroups/GroupCreated.cs` (namespace, `: IEvent`)

---

## Phase 2: Foundational (skeleton — no behaviour, MUST compile, MUST NOT make any AC pass)

**Purpose**: contracts, EF configuration, migration and the test observer. `tools/fleet/skeleton-check.js` allows only Domain/DAL/Migrations/Shared.Abstractions + test infrastructure here — no handler changes.

- [ ] T003 [P] Create `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/Resources/ResourceSchemaCreatedEvent.cs`: `public record ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId) : IEvent;` — namespace mirrors `Events/ManagementGroups/GroupCreated.cs` with `Resources` in place of `ManagementGroups`
- [ ] T004 [P] Create `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/Resources/ResourceSchemaDeletedEvent.cs`: `public record ResourceSchemaDeletedEvent(Guid SchemaId, Guid GroupId) : IEvent;`
- [ ] T005 [P] Create `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/Resources/ResourceInstanceCreatedEvent.cs`: `public record ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId) : IEvent;`
- [ ] T006 [P] Create `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/Resources/ResourceInstanceDeletedEvent.cs`: `public record ResourceInstanceDeletedEvent(Guid ResourceId, Guid SchemaId) : IEvent;`
- [ ] T007 In `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/DAL/Configurations/ResourceTypeConfiguration.cs` change `builder.HasIndex(t => new { t.GroupId, t.Name }).IsUnique();` to `.IsUnique().HasFilter("\"DeletedAt\" IS NULL");` (pattern: `backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.Core/DAL/Configurations/ManagementGroupConfiguration.cs:14`)
- [ ] T008 DEVELOPER-ONLY (constitution VI): run `dotnet ef migrations add SoftDeleteResourceTypeUniqueIndex --project backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Migrations --startup-project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`; verify the generated `Up()` in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Migrations/Migrations/<timestamp>_SoftDeleteResourceTypeUniqueIndex.cs` only drops and recreates `IX_resource_types_GroupId_Name` with `unique: true, filter: "\"DeletedAt\" IS NULL"`, and that `ResourcesDbContextModelSnapshot.cs` gained the `.HasFilter(...)`; this is the story's single migration — if the model changes later, `migrations remove` then `migrations add` again
- [ ] T009 [P] Create `backend/src/Shared/ThingsBooksy.Shared.IntegrationTests/Messaging/RecordingMessageBroker.cs`: `public sealed class RecordingMessageBroker : IMessageBroker` wrapping an inner `IMessageBroker`, thread-safe `IReadOnlyList<IMessage> Published`, `Clear()`, both `PublishAsync` overloads record then forward to the inner broker
- [ ] T010 In `backend/src/Shared/ThingsBooksy.Shared.IntegrationTests/ThingsBooksyWebAppFactory.cs` add `builder.ConfigureTestServices(services => { … })` that replaces the `IMessageBroker` registration with a singleton `RecordingMessageBroker` decorating the original `InMemoryMessageBroker` (resolve the original via the existing descriptor's implementation type/factory), and expose it as `public RecordingMessageBroker Broker` (or a `GetBroker()` helper) on the factory; keep the existing configuration overrides untouched
- [ ] T011 In `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests/Clients/ResourcesTestClient.cs` FIRST read the existing `GetResourceTypeFromDbAsync` — the comment at `UpdateDeleteResourceTypeTests.cs:185` suggests it already uses `IgnoreQueryFilters()`; then make sure the client exposes BOTH a filtered read (null for soft-deleted) and an unfiltered read (`…IgnoringFiltersAsync`, row with `DeletedAt`) plus `GetInstancesFromDbIgnoringFiltersAsync(Guid typeId)`; name them so that T015/T016 can tell them apart
- [ ] T012 `dotnet build backend/ThingsBooksy.slnx` green; run `node tools/fleet/skeleton-check.js --story 015-resource-time-buffer` → 0 violations

**Checkpoint**: contracts exist, DB index is partial, tests can observe the broker — no event is published yet and the schema is still hard-deleted.

---

## Phase 3: Acceptance tests — RED (both stories)

**Purpose**: encode AC-3..AC-9 before any behaviour. All tests MUST compile and MUST fail (`red-first-prover`). Arrange = EF seeding via factories (never via the API), Act = HTTP, Assert = DB re-read + recorded events. Test names: `{Action}{Entity}_{Condition}_{Result}`.

- [ ] T013 [P] [US1] Create `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests/Events/ResourceLifecycleEventTests.cs` in `[Collection(nameof(IntegrationTestCollection))]` with `Broker.Clear()` in the constructor and these tests: `[Trait("AC","AC-3")] CreateResourceType_WithValidData_PublishesResourceSchemaCreatedEvent` (POST /resources/types as owner → 201; exactly one `ResourceSchemaCreatedEvent` with `SchemaId` = created id and `GroupId` = group; row exists in DB before/at assertion); `[Trait("AC","AC-4")] CreateResourceInstance_WithValidData_PublishesResourceInstanceCreatedEvent`; `[Trait("AC","AC-5")] DeleteResourceType_WithInstances_PublishesExactlyOneResourceSchemaDeletedEvent` (seed type + 2 instances; DELETE → 204; exactly one `ResourceSchemaDeletedEvent(SchemaId, GroupId)`; zero `ResourceInstanceDeletedEvent`); `[Trait("AC","AC-5")] DeleteResourceType_WithoutInstances_PublishesOneResourceSchemaDeletedEvent`; `[Trait("AC","AC-6")] DeleteResourceInstance_Existing_PublishesResourceInstanceDeletedEvent`
- [ ] T014 [P] [US1] In the same file add `[Trait("AC","AC-7")] PublishedResourceEvents_UseSharedAbstractionsContracts_WithGuidOnlyPayloads`: run the AC-3 and AC-5 flows, then assert every recorded event is an instance of a type whose namespace ends with `Events.Resources` (assembly `ThingsBooksy.Shared.Abstractions`), implements `IEvent`, and whose constructor parameters are all `System.Guid` — this test is RED before Phase 4 (nothing is recorded yet), which satisfies the red-first rule while proving AC-7
- [ ] T015 [P] [US2] Create `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests/ResourceTypes/SoftDeleteResourceTypeTests.cs`: `[Trait("AC","AC-8")] DeleteResourceType_AsOwner_SoftDeletesTypeAndHidesIt` (seed type with 2 instances; DELETE → 204; `GetResourceTypeFromDbIgnoringFiltersAsync` returns row with `DeletedAt != null`; `GetResourceTypeFromDbAsync` (filtered) returns null; GET /resources/types/{id} → 404; GET /resources/types?groupId → does not contain id; both instances have `DeletedAt`); `[Trait("AC","AC-9")] CreateResourceType_WithNameOfSoftDeletedType_ReturnsCreated` (seed type "X", DELETE it, POST "X" same group → 201, DB has one active and one deleted "X")
- [ ] T016 [US2] Rewrite the two hard-delete assertions in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests/ResourceTypes/UpdateDeleteResourceTypeTests.cs` (around lines 185 and 283): replace "NOT found even with IgnoreQueryFilters" with: filtered read returns null AND `GetResourceTypeFromDbIgnoringFiltersAsync` returns the row with `DeletedAt` set; add `[Trait("AC","AC-8")]` to both tests
- [ ] T017 `dotnet build backend/ThingsBooksy.slnx` green; `node tools/fleet/red-first-prover.js --story 015-resource-time-buffer` → RED for AC-3..AC-9 (all seven must be RED; AC-7 is red because no event is recorded yet)

**Checkpoint**: all acceptance tests exist, compile and fail for the right reason (no events recorded / row physically gone).

---

## Phase 4: User Story 1 — lifecycle events (Priority: P1) 🎯 MVP

**Goal**: the four command handlers publish their event after `SaveChangesAsync`.

**Independent Test**: T013/T014 green; no other test regresses.

- [ ] T018 [P] [US1] `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Features/CreateResourceType/CreateResourceTypeCommandHandler.cs`: inject `IMessageBroker messageBroker` (constructor stays ≤ 4 params: data provider, clock, broker); after `await _dataProvider.SaveChangesAsync(cancellationToken);` add `await _messageBroker.PublishAsync(new ResourceSchemaCreatedEvent(resourceType.Id, resourceType.GroupId), cancellationToken);`
- [ ] T019 [P] [US1] `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Features/CreateResourceInstance/CreateResourceInstanceCommandHandler.cs`: inject `IMessageBroker`; after `SaveChangesAsync` publish `new ResourceInstanceCreatedEvent(instance.Id, instance.ResourceTypeId)`
- [ ] T020 [P] [US1] `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Features/DeleteResourceInstance/DeleteResourceInstanceCommandHandler.cs`: inject `IMessageBroker`; after `SaveChangesAsync` publish `new ResourceInstanceDeletedEvent(instance.Id, instance.ResourceTypeId)`
- [ ] T021 [US1] `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Features/DeleteResourceType/DeleteResourceTypeCommandHandler.cs`: inject `IMessageBroker`; after the single `SaveChangesAsync` publish exactly one `new ResourceSchemaDeletedEvent(resourceType.Id, resourceType.GroupId)`; do NOT touch `SoftDeleteInstancesAsync` (bulk path stays, DEC-4)
- [ ] T022 [US1] Confirm `Resources` `Extensions.cs`/module registration needs no change (`IMessageBroker` is registered by `AddMessaging` in Shared.Infrastructure — F-7); `dotnet test backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests --filter "AC=AC-3|AC=AC-4|AC=AC-5|AC=AC-6|AC=AC-7"` green

**Checkpoint**: US1 delivered; AC-8/AC-9 still red (schema still hard-deleted, so AC-5 is green while AC-8 is red — expected).

---

## Phase 5: User Story 2 — schema soft delete (Priority: P2)

**Goal**: `DELETE /resources/types/{id}` marks the row instead of removing it; name reusable.

**Independent Test**: T015/T016 green; T013 AC-5 still green.

- [ ] T023 [US2] In `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Features/DeleteResourceType/DeleteResourceTypeCommandHandler.cs` replace `_dataProvider.RemoveResourceType(resourceType);` with `resourceType.Delete(_clock.CurrentDate());` (same `now` as used for `SoftDeleteInstancesAsync`), keep the single `SaveChangesAsync` and the publish from T021
- [ ] T024 [US2] Remove `RemoveResourceType` from `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Features/DeleteResourceType/DataProviders/IDeleteResourceTypeCommandDataProvider.cs` and `.../DataProviders/DeleteResourceTypeCommandDataProvider.cs` (now unused); ensure the type is loaded as a tracked entity so `Delete(now)` persists
- [ ] T025 [US2] `dotnet test backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests` (whole project) green — includes AC-8, AC-9, the rewritten tests and all pre-existing Resources tests

**Checkpoint**: both stories green.

---

## Phase 6: Polish & gates

- [ ] T026 Run `node tools/fleet/coverage-gaps.js --story 015-resource-time-buffer`; for each uncovered branch of changed production lines add a test in the matching test file tagged `AC-n` or `[Trait("AC","UNSPECIFIED")]`; expected minimum: `DeleteResourceType_AlreadyDeleted_ReturnsNotFoundAndPublishesNothing` and `DeleteResourceInstance_AlreadyDeleted_ReturnsNotFoundAndPublishesNothing` in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests/Events/ResourceLifecycleEventTests.cs` (ASM-19; tag `UNSPECIFIED` — it is an owner decision at DoD)
- [ ] T027 [P] Add unit tests for `ResourceType.Delete(now)` state change if `ThingsBooksy.Modules.Resources.Tests.Unit` exists; if the project does not exist (empty directory today), record `UNSPECIFIED: unit project missing` in `runs/015-resource-time-buffer/journal.jsonl` instead of creating the project in this story
- [ ] T028 `dotnet format backend/ThingsBooksy.slnx` then `dotnet format backend/ThingsBooksy.slnx --verify-no-changes`
- [ ] T029 Verify FR-011 by checking that `git diff --name-only <merge-base>` does NOT list `Events/Handlers/GroupDeletedHandler.cs`, then `node tools/fleet/gate.js --story 015-resource-time-buffer` → GREEN (build, format, tests, swagger re-export, contract-diff CLEAR against `runs/015-resource-time-buffer/contract-next.json`, ac-matrix 100 % for AC-3..AC-9, red-first hash unchanged)
- [ ] T030 Update `runs/015-resource-time-buffer/story.md` only if an AC wording had to change during implementation (then re-run `node tools/fleet/ac-matrix.js`); tick all boxes in this file; do NOT commit — show the diff to the owner (commits only with explicit approval)

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 (T003–T006, T009 parallel; T007 before T008; T010 after T009; T011 independent) → T012.
- Phase 3 after T012; T013/T014/T015 parallel, T016 after T011; T017 last.
- Phase 4 after T017: T018/T019/T020 parallel, T021 sequential with nothing (same file as T023 — do T021 before T023).
- Phase 5 after T021: T023 → T024 → T025.
- Phase 6 after T025: T026, T027 parallel; T028 → T029 → T030.

### User story dependency

- US1 (events) is independent of US2 except for the shared file `DeleteResourceTypeCommandHandler.cs` (T021 then T023).
- US2 depends on Phase 2 T007/T008 (partial index) for AC-9.

## Parallel Example

```text
# Phase 2 skeleton in one wave:
T003 ResourceSchemaCreatedEvent.cs | T004 ResourceSchemaDeletedEvent.cs | T005 ResourceInstanceCreatedEvent.cs | T006 ResourceInstanceDeletedEvent.cs | T009 RecordingMessageBroker.cs
# Phase 3 tests in one wave:
T013 ResourceLifecycleEventTests.cs | T015 SoftDeleteResourceTypeTests.cs
# Phase 4 handlers in one wave:
T018 CreateResourceType | T019 CreateResourceInstance | T020 DeleteResourceInstance
```

## Implementation Strategy

- **MVP** = Phases 1–4 (US1): events flowing, schema still hard-deleted — already useful to a future Availability module.
- **Increment 2** = Phase 5 (US2): soft delete + name reuse.
- Both must pass `gate.js` before the owner is asked to approve the commit.
