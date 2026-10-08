---

description: "Task list for 016-rename-resource-schema - rename resource type to resource schema"
---

# Tasks: Rename resource type to resource schema

**Input**: Design documents from `/specs/016-rename-resource-schema/` (plan.md, spec.md, research.md, data-model.md, contracts/README.md, quickstart.md); discovery record in `runs/016-rename-resource-schema/` (facts F-0..F-7, decisions DEC-1..DEC-5, assumptions ASM-1..ASM-19, `contract-next.json`).
**Prerequisites**: plan.md, spec.md (AC-1, 2, 6-14), all decisions DECIDED, G2 PASSED.

**Tests**: REQUIRED (constitution V). Acceptance tests are written blind in Phase 4, after the skeleton and the migration, and the contract criteria MUST fail before Phase 5 (`red-first-prover --ac AC-1,AC-2,AC-6,AC-7,AC-10,AC-11,AC-12`; AC-9, AC-13, AC-14 are the justified exception of plan.md Complexity Tracking / ASM-18). Backend tests carry `[Trait("AC", "AC-n")]`, frontend tests `it("[AC-n] …")`.

**Organization** (DEC-5): Phase 1 setup -> Phase 2 **Step 0** internal rename, before the delivery baseline -> Phase 3 skeleton + developer migration -> Phase 4 blind acceptance tests (all test infrastructure and all tests) -> Phase 5 US1 API behaviour -> Phase 6 US2 database verification -> Phase 7 US3 frontend -> Phase 8 polish and gates.

**Tasks are goals with acceptance.** The name map is `data-model.md`; the expected responses are the tables in `spec.md`. Nothing here is a code listing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: [US1] API says "schema", [US2] database says "schema", [US3] screens
- **Actor tags**: `CONDUCTOR`, `DEVELOPER-ONLY`, `OWNER` mark tasks that no delivery agent performs; untagged Phase 3 / 5 tasks are be-writer's, Phase 4 tasks are test-designer's
- File paths are repository-relative.

## Path Conventions

- `RES` = `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources`  (so `RES.Core/…`, `RES.Api/…`, `RES.Migrations/…`, `RES.IntegrationTests/…`)
- `MG.IT` = `backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.IntegrationTests`
- `FE` = `frontend/src/app`

---

## Phase 1: Setup

- [x] T001 CONDUCTOR Verify the start state and record it as a `NOTE` in `runs/016-rename-resource-schema/journal.jsonl`: branch is `016-rename-resource-schema`; `node tools/fleet/status.js --story 016-rename-resource-schema` exits 0 with G2 PASSED; `dotnet build backend/ThingsBooksy.slnx` is green; the number of passing tests of `dotnet test backend/ThingsBooksy.slnx` and the sha256 of `generated/swagger.base.json` are written down (they are the "before" values of T007).
- [x] T002 CONDUCTOR Fleet precondition, not story code: before Phase 4 the AC-trait collision must be resolved in `tools/fleet/` (existing tests in `RES.IntegrationTests` carry `[Trait("AC","AC-6")]`..`AC-10` of story 015; `red-first-prover` and `ac-matrix` select by AC id only - research.md R7). Acceptance: a dry run of `node tools/fleet/ac-matrix.js --story 016-rename-resource-schema` on the untouched tree lists no test of story 015 as coverage of this story.

---

## Phase 2: Step 0 - internal rename (before the delivery baseline; behaviour-neutral)

**Purpose**: C# names say "schema"; nothing a client or the database can observe changes (FR-014, FR-009, FR-010, DEC-5). One actor edits production and test code together (developer or conductor) - a compile-coupled rename, so no task here is parallel.

**Do not touch in this phase**: route strings, `WithName(…)` texts, request record names and properties in `RES.Api/Requests/`, the endpoint parameter `resourceTypeId`, the property `ResourceTypeId` of `ResourceInstanceRowDto` and `GetResourceInstanceQueryResult`, every message text, the literal `RESOURCE_TYPE_NAME_TAKEN`, test-client URLs and JSON field names, the raw SQL table name, `RES.Migrations/`, `frontend/`.

- [x] T003 CONDUCTOR/DEVELOPER Rename the production C# names in `RES.Core/` (Domain, DAL and DAL/Configurations, Features, Exceptions, Events/Handlers, Extensions.cs) exactly as the table "Backend code - Step 0" of `specs/016-rename-resource-schema/data-model.md`, including folders, file names and namespaces. Pin the database names: the schema entity keeps `ToTable("resource_types")` and the two renamed foreign-key properties are mapped to the column name `ResourceTypeId`. Acceptance: `RES.Core` builds; no identifier of the family `ResourceType` remains in `RES.Core` except the two kept JSON-visible properties, the pins and the 409 literal.
- [x] T004 CONDUCTOR/DEVELOPER Update `RES.Api/ResourcesModule.cs` so that it compiles against the renamed commands and queries, changing nothing from the "do not touch" list. Acceptance: `dotnet build backend/ThingsBooksy.slnx` green for the two production projects.
- [x] T005 CONDUCTOR/DEVELOPER Rename the internal names of the test code in `RES.IntegrationTests/` (folder `ResourceTypes/` -> `ResourceSchemas/`, test classes and methods, `Clients/ResourcesResourceTypeFactory.cs` -> `ResourcesResourceSchemaFactory.cs`, method names of `Clients/ResourcesTestClient.cs`, usages in `Events/` and `ResourceInstances/`, comments) per FR-010; arrange and assert logic, URLs and JSON names stay.
- [x] T006 CONDUCTOR/DEVELOPER Rename the internal names in `MG.IT/Clients/ManagementGroupsTestClient.cs` and `MG.IT/ManagementGroups/DeleteManagementGroupTests.cs` (method `ResourcesAllResourceTypesDeletedAsync`, variables, assertion message); the raw SQL text and the request field stay.
- [x] T007 CONDUCTOR Prove Step 0 is invisible, following "After Step 0" of `specs/016-rename-resource-schema/quickstart.md`: full test run green with the test count of T001; re-exported `generated/swagger.base.json` has the sha256 of T001; no change under `RES.Migrations/` and `frontend/`; `dotnet format backend/ThingsBooksy.slnx --verify-no-changes` clean. If the test run fails on a pending model change, STOP and ask the owner (research.md R1). Then show the diff summary to the owner, commit only with the owner's explicit yes, and run `node tools/fleet/baseline.js --story 016-rename-resource-schema`.

**Checkpoint**: baseline recorded; from here the standard delivery phases run.

---

## Phase 3: Skeleton + migration (data shapes only; MUST compile; MUST NOT make a contract criterion pass)

- [x] T008 Remove the Step 0 pins in `RES.Core/DAL/Configurations/` (schema, instance and property-definition configurations): table name `resource_schemas`, both foreign-key columns `ResourceSchemaId` by default naming, the foreign key from `resource_property_definitions` to `resource_schemas` with the explicit name `FK_resource_property_definitions_resource_schemas` via `HasConstraintName` (AC-13, DEC-1, DEC-6: the default name would exceed the 63-character limit of PostgreSQL). Acceptance: build green; `node tools/fleet/skeleton-check.js --story 016-rename-resource-schema` reports 0 violations and only files under `RES.Core/DAL/`.
- [x] T009 DEVELOPER-ONLY (constitution VI) Generate the story's single migration `RenameResourceTypeToResourceSchema` in `RES.Migrations/` and make it rename-only as described in `specs/016-rename-resource-schema/research.md` R2 (DEC-1): no table or column is dropped or created; the filtered unique index keeps its filter; `Down` mirrors `Up`. Refresh `generated/core-surface.json` with the tooling tests, run `node tools/fleet/migration-check.js --story 016-rename-resource-schema` and close G2b through `decide.js` after the owner's answer that names the migration sha (AC-9, AC-13, AC-14).

**Checkpoint**: database names are new, API is still old; existing tests pass except the ManagementGroups raw-SQL check (fixed in T010).

---

## Phase 4: Acceptance tests - blind pass (test infrastructure and all tests)

**Rule**: written from `spec.md`, `contract-next.json`, `generated/core-surface.json` and the test conventions, without reading production source. Every new test is tagged with its AC.

- [x] T010 Switch the test infrastructure to the new contract: `RES.IntegrationTests/Clients/ResourcesTestClient.cs` (addresses `/resources/schemas`, JSON field and filter `resourceSchemaId`, response DTOs), the expected literal `RESOURCE_SCHEMA_NAME_TAKEN` in the two existing 409 tests under `RES.IntegrationTests/ResourceSchemas/`, the table name in the raw SQL of `MG.IT/Clients/ManagementGroupsTestClient.cs` (keep the comment that explains raw SQL on another module's schema) and the request field in `MG.IT/ManagementGroups/DeleteManagementGroupTests.cs`. Existing tests keep their arrange and assert logic - they are the regression net of AC-1 (FR-010, SC-001).
- [x] T011 [P] [US1] AC-1: new file `RES.IntegrationTests/ResourceSchemas/ResourceSchemaContractTests.cs` - as group owner, one test per schema operation (create with 201 + `Location: /resources/schemas/{id}` + `{ id }`, list by group, read, update, delete) and per instance operation that names a schema (create with `resourceSchemaId`, list rows carry `resourceSchemaId`, list filtered by `resourceSchemaId`, read one instance carries `resourceSchemaId`), each compared with the database through EF; plus the taken-name case: 409 with code `RESOURCE_SCHEMA_NAME_TAKEN` and the unchanged message (DEC-2).
- [x] T012 [P] [US1] AC-2: new file `RES.IntegrationTests/ResourceSchemas/RemovedResourceTypeAddressesTests.cs` - `POST` and `GET /resources/types`, `GET`, `PUT`, `DELETE /resources/types/{id}` answer 404 for a signed-in owner and for a caller without a token (FR-002).
- [x] T013 [P] [US1] AC-6: new file `RES.IntegrationTests/Contract/ResourcesApiDescriptionTests.cs` - load the generated API description from the test host and assert that no path, component schema name, property name, parameter name or operation id contains `ResourceType`, `resourceTypeId`, `resource type`, `resource types` (case-insensitive), and that the names listed in spec.md AC-6 are present (FR-004).
- [x] T014 [P] [US1] AC-7: new file `RES.IntegrationTests/ResourceSchemas/ResourceSchemaMessagesTests.cs` - for each row 1-6 of the spec's Message list provoke the situation and assert status, error code and the exact new text.
- [x] T015 [P] [US1] AC-10: new file `RES.IntegrationTests/Access/MemberAccessBaselineTests.cs` - as a member who is not the owner, every row 1-15 of the spec's Access baseline (column "member"), including row 14 (`includeDeleted=true` returns the soft-deleted instances - intended, DEC-3) and, for the refused writes, that nothing changed in the database.
- [x] T016 [P] [US1] AC-11: new file `RES.IntegrationTests/Access/OutsiderAccessBaselineTests.cs` - as a signed-in user who is neither owner nor member, every row 1-15 (column "outsider"); no response contains data of the group.
- [x] T017 [P] [US1] AC-12: new file `RES.IntegrationTests/Access/ForeignSchemaFilterTests.cs` - as owner of G with a schema S' of another group: `groupId=G&resourceSchemaId=S'` -> 200, empty `items`, null `nextCursor`; `resourceSchemaId=S'` alone -> 403 `resources_forbidden` "Access to this group is forbidden.".
- [x] T018 [P] [US2] AC-13: new file `RES.IntegrationTests/Database/ResourcesDatabaseNamesTests.cs` - list table, column, constraint and index names of schema `resources` from the database catalog; none matches `resource_type` / `ResourceType` case-insensitively; the six names of spec.md AC-13 exist (raw catalog query with a comment: metadata cannot be read through the model).
- [x] T019 [P] [US2] AC-9: new file `RES.IntegrationTests/Database/ResourcesUpgradeFromMainTests.cs` - on a separate empty database: migrate to `20260928111659_SoftDeleteResourceTypeUniqueIndex`, insert one schema (plus one soft-deleted schema), one property definition, one instance and one property value with the old names, migrate to latest without error, read every row back through EF (shape and the reason for raw SQL: research.md R5; SC-003).
- [x] T020 [P] [US2] AC-14: in `RES.IntegrationTests/Database/ResourcesUpgradeFromMainTests.cs` or a sibling file - the Resources model reports no pending changes against its snapshot after all migrations, and the test host starts.
- [x] T021 [P] [US3] AC-8 frontend tests titled `[AC-8] …`: `FE/features/groups/services/resources-api.service.spec.ts` (NEW file - no spec exists for the service today; each of the five schema calls and the create-instance call goes to `/resources/schemas…` / sends `resourceSchemaId`), `FE/features/groups/resources-panel/resources-panel.component.spec.ts` (header `Schema`; rows map `resourceSchemaId` to the schema name; rename the two existing test titles), `FE/features/groups/schemas-panel/schemas-panel.component.spec.ts` (empty-state sentence and the add-button label of spec.md AC-8).
- [x] T022 CONDUCTOR Build the test projects; `node tools/fleet/red-first-prover.js --story 016-rename-resource-schema --ac AC-1,AC-2,AC-6,AC-7,AC-10,AC-11,AC-12` -> RED (every test of these criteria compiles and fails); the tests of AC-9, AC-13, AC-14 are green (ASM-18); `node tools/fleet/ac-matrix.js --story 016-rename-resource-schema` -> every one of the 11 criteria has a test.

**Checkpoint**: RED proven for the contract criteria.

---

## Phase 5: User Story 1 - the API says "schema" (Priority: P1)

**Goal**: FR-001..FR-007. **Independent test**: `dotnet test` of `RES.IntegrationTests` and `MG.IT` green.

- [x] T023 [US1] `RES.Api/ResourcesModule.cs` and `RES.Api/Requests/`: the five schema operations live at `/resources/schemas` and `/resources/schemas/{id:guid}` with operation names `Create / Get / Update / Delete resource schema` and `Get resource schemas`; create answers `Location: /resources/schemas/{id}`; request records are `CreateResourceSchemaRequest` / `UpdateResourceSchemaRequest`; the create-instance request field and the list filter are `resourceSchemaId`; no `/resources/types` route remains (AC-1, AC-2, AC-6). Order of checks and authorization untouched (FR-007).
- [x] T024 [P] [US1] `RES.Core/Features/GetResourceInstances/ResourceInstanceRowDto.cs` and `RES.Core/Features/GetResourceInstance/GetResourceInstanceQueryResult.cs` (and the data providers / handlers that build them): the JSON-visible property is `ResourceSchemaId` (AC-1, FR-003).
- [x] T025 [P] [US1] Message texts 1-6 of the spec's Message list in the handlers under `RES.Core/Features/` (create / update / delete schema, create instance, list instances); status codes and error codes unchanged (AC-7, FR-005, ASM-13).
- [x] T026 [P] [US1] `RES.Core/Exceptions/ResourcesExceptionToResponseMapper.cs`: the name-conflict code is `RESOURCE_SCHEMA_NAME_TAKEN`; message unchanged (DEC-2, FR-006).
- [x] T027 [US1] Whole projects green: `dotnet test RES.IntegrationTests` and `dotnet test MG.IT` (new acceptance tests and every pre-existing test); lifecycle event tests of story 015 unchanged and green (FR-013).

**Checkpoint**: US1 done - AC-1, 2, 6, 7, 10, 11, 12 green.

---

## Phase 6: User Story 2 - the database says "schema" (Priority: P2)

**Goal**: FR-008. Delivered by T008 + T009; this phase only verifies and guards.

- [x] T028 [US2] CONDUCTOR Confirm in the gate run that the tests of AC-9, AC-13, AC-14 (`RES.IntegrationTests/Database/`) are green and that `runs/016-rename-resource-schema/migration-check.json` matches the migration in the tree with G2b PASSED. If the branch was rebased onto `main` after T009, the developer regenerates the migration (`migrations remove` + `migrations add`, constitution VI), and migration-check + G2b are repeated (AC-14).

---

## Phase 7: User Story 3 - the screens keep working and say "schema" (Priority: P3)

**Goal**: FR-011, FR-012. **Actor**: frontend production code has no fleet writer yet - decided by the owner in the delivery session before Phase 4 (research.md R6).

- [x] T029 [US3] `FE/features/groups/services/resources-api.service.ts`: DTOs, methods, addresses and the payload field per the "Frontend" table of data-model.md (AC-8).
- [x] T030 [US3] Callers follow T029: `FE/features/groups/group-context.store.ts`, `FE/features/schemas/schema-designer-page/schema-designer-page.component.ts`, `FE/features/groups/create-resource-modal/create-resource-modal.component.ts`, `FE/features/groups/resources-panel/resources-panel.component.ts` and `.html`, `FE/features/groups/group-detail-page/group-detail-page.component.ts` (AC-8).
- [x] T031 [P] [US3] The three texts of spec.md AC-8 in `FE/features/groups/resources-panel/resources-panel.component.html` and `FE/features/groups/schemas-panel/schemas-panel.component.html`; `FE/features/auth/auth-page/auth-page.component.html` is NOT changed (DEC-4).
- [x] T032 [P] [US3] Bring the generated files `FE/api/Resources.ts` and `FE/api/data-contracts.ts` in line with the re-exported swagger (regenerate with `swagger-typescript-api`; if the output layout differs from the current files beyond the rename, replace only the renamed identifiers by hand - ASM-15).
- [x] T033 [US3] `cd frontend && npm run build` and `npm test` green, including the `[AC-8]` tests of T021.
- [x] T034 [US3] OWNER Walk through "After the frontend change" of quickstart.md on the running application (list schemas, create, edit, create a resource; three texts) - the role-owner check of AC-8 (SC-004).

---

## Phase 8: Polish & gates

- [x] T035 Sighted test pass over the coverage-gap report (`node tools/fleet/coverage-gaps.js --story 016-rename-resource-schema`): branches of the changed production lines not exercised by the blind tests get a test tagged with an AC or `UNSPECIFIED` (an `UNSPECIFIED` test is a decision for the owner). No unit-test task: the story adds no domain logic and `*.Tests.Unit` projects do not exist.
- [x] T036 CONDUCTOR Search rules of quickstart.md "After the behaviour phase" step 3 print nothing (FR-009, FR-011, SC-002; this is also the "text search finds no resource type" clause of AC-7); record the three commands and their empty output as a `NOTE` in the journal.
- [x] T037 `dotnet format backend/ThingsBooksy.slnx` then `--verify-no-changes`.
- [x] T038 CONDUCTOR `node tools/fleet/gate.js --story 016-rename-resource-schema` -> GREEN (build, format, tests, swagger re-export, `contract-diff` CLEAR against `runs/016-rename-resource-schema/contract-next.json`, migration step, AC matrix).
- [x] T039 CONDUCTOR Review rounds, guards, DoD and G3 per `docs/agent-fleet-v4/runbook-delivery.md`; update `runs/016-rename-resource-schema/story.md` only if an AC wording had to change; show the diff to the owner; no commit without the owner's yes.

---

## Dependencies & Execution Order

- Phase 1 -> Phase 2 (T003 -> T004 -> T005 -> T006 -> T007, one actor, sequential) -> `baseline.js` -> Phase 3 (T008 -> T009) -> Phase 4 (T010 first; T011-T021 parallel; T022 last) -> Phase 5 (T023; T024-T026 parallel with it; T027 last) -> Phase 6 (T028) -> Phase 7 (T029 -> T030; T031, T032 parallel; T033 -> T034) -> Phase 8.
- T002 (fleet) blocks T022. The frontend actor decision blocks Phase 7 only; Phase 7 may run in parallel with Phase 5 once T021 exists, because backend and frontend ship together and the frontend tests mock HTTP.

### User story dependency

- US1 needs Phases 2-4. US2 is delivered by Phase 3 and verified in Phase 6. US3 needs the contract of US1 (addresses and field name) but not its code to pass its own tests; the owner walk-through T034 needs US1 and US2 running.

## Parallel Example

```text
# Phase 4, one wave after T010:
T011, T012, T013, T014, T015, T016, T017, T018, T019, T020 (backend, separate files) + T021 (frontend specs)
# Phase 5, one wave:
T023 | T024 | T025 | T026
```

## Implementation Strategy

- **Step 0 first, alone, and committed**: it is the bulk of the diff and is proven by the unchanged suite, not by review.
- **MVP = US1 + US2 together** (the API and the database flip are both needed for a consistent backend); US3 follows in the same story because the frontend is the only client and old addresses have no alias.
- Stop points for the owner: end of Phase 2 (commit yes/no), T009 (migration + G2b), T034 (walk-through), G3.
