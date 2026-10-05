# Feature Specification: Rename resource type to resource schema

**Feature Branch**: `016-rename-resource-schema`  
**Created**: 2026-10-02  
**Status**: Draft (G2 package, discovery rounds 1-2)  
**Input**: GitHub Story #57 (epic #56 Resource availability rules). Source of truth for scope: `runs/016-rename-resource-schema/story.md`. Facts: `runs/016-rename-resource-schema/discovery/facts.jsonl` (F-0..F-7). Owner decisions: `discovery/decisions.jsonl` (DEC-1..DEC-5). Assumptions: `discovery/assumptions.jsonl` (ASM-1..ASM-19).

This story adds no behaviour. The thing the business and the screens call a **resource schema** is called a **resource type** in the API, the backend code and the database. After this story one word is used everywhere. `PropertyDataType` / `dataType` (the data kind of a property) is a different concept and is not renamed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The API says "schema" and behaves exactly as before (Priority: P1)

As the group owner (through our frontend, the only API client) I keep creating, listing, reading, editing and deleting schemas and creating and listing resources of a schema; the only difference is the vocabulary of the API: addresses under `/resources/schemas`, the field and filter `resourceSchemaId`, operation and model names, and messages that say "resource schema". Members and users of other groups get exactly the access they have today.

**Why this priority**: the API is where the inconsistency is visible (F-0, F-1); every later story of the availability epic builds on these names.

**Independent Test**: run the existing Resources integration tests against the new addresses and field names (they are the regression net for "same result"), plus new tests for the removed old addresses, the generated API description, the message texts and the access baseline below.

**Acceptance Scenarios**:

1. **AC-1** Given a signed-in user with the access they have today, When they use the schema operations at `/resources/schemas` (create, list by group, read, update, delete) and the instance operations that name a schema (create with `resourceSchemaId`, list whose rows carry `resourceSchemaId`, list filtered by `resourceSchemaId`, read one instance whose body carries `resourceSchemaId`), Then every operation gives the same result (status, body shape, stored data, published events) as the matching operation under the old names gave before this story, apart from the message wording of AC-7 and one error code: a taken schema name answers 409 with `{ "code": "RESOURCE_SCHEMA_NAME_TAKEN", "message": "A schema with this name already exists in the group." }` instead of code `RESOURCE_TYPE_NAME_TAKEN` (DEC-2). `POST /resources/schemas` answers 201 with `Location: /resources/schemas/{id}` and body `{ "id": … }`.
2. **AC-2** Given the application after this story, When anyone calls an old address (`POST` or `GET /resources/types`, `GET`, `PUT` or `DELETE /resources/types/{id}`), Then the address no longer exists (404); no alias is kept.
3. **AC-6** Given the generated API description of the Resources module, When it is searched (case-insensitively) for `ResourceType`, `resourceTypeId`, `resource type` and `resource types`, Then no address, request or response model, field, parameter or operation name contains them. The operation names are `Create resource schema`, `Get resource schemas`, `Get resource schema`, `Update resource schema`, `Delete resource schema`; the request models are `CreateResourceSchemaRequest` and `UpdateResourceSchemaRequest`; the field / parameter is `resourceSchemaId`.
4. **AC-7** Given the list of user-visible messages below ("Message list"), When the same situation occurs after this story, Then each listed message carries the new text and is otherwise unchanged (same status, same error code), and a case-insensitive text search of the Resources module's user-visible messages finds no "resource type".
5. **AC-10** *(role: member, not owner)* Given the baseline below ("Access baseline"), When a group member who is not the owner makes each listed call through `/resources/schemas` and `resourceSchemaId`, Then every response (status, error code, message, body shape) is the one in the "after" column - identical to today apart from the AC-7 wording. This includes listing resources with `includeDeleted=true` and receiving the deleted ones (declared intended, DEC-3).
6. **AC-11** *(role: signed-in user from another group)* Given the same baseline, When a user who is neither owner nor member of the group makes each listed call, Then every response is the one in the "after" column: an existing schema or resource of another group answers 403, a missing schema answers 404 on read and 400 on the other operations (declared intended, DEC-3). No call returns data of the group.
7. **AC-12** *(role: owner of group G)* Given a schema S' that belongs to another group G' of which the caller is neither owner nor member, When the owner of G lists instances (a) with `groupId=G&resourceSchemaId=S'`, Then the response is 200 with an empty `items` list and a null `nextCursor`; and (b) with `resourceSchemaId=S'` only, Then the response is 403 `resources_forbidden` "Access to this group is forbidden." - both as today (ASM-6).

---

### User Story 2 - The database says "schema" and existing development databases keep working (Priority: P2)

As the developer I start the new version on a development database created before this story; it starts without errors, my schemas and resources are still there, and no table, column, key or index of the Resources area carries the old name.

**Why this priority**: the owner chose the full rename including the database (rejected alternative: keep old table names); it is the story's only migration and the only step with a data risk.

**Independent Test**: on a database migrated to the last migration of `main` and seeded with a schema, a property definition, an instance and a property value, apply the story's migration; list the names in the `resources` area; read the seeded rows back.

**Acceptance Scenarios**:

1. **AC-9** Given a development database created before this story (any content), When the new version of the application starts, Then it starts without errors. By DEC-1 the migration only renames, so existing schemas, property definitions, instances and property values are still present afterwards (stronger than the story's "data may be removed", which stays allowed but is not used).
2. **AC-13** Given the Resources area of the database (`resources`) after the migrations of this story, When the names of its tables, columns, constraints and indexes are listed (the area has no sequences, ASM-11), Then none contains `resource_type` or `ResourceType` (case-insensitive); the names are `resource_schemas`, column `ResourceSchemaId` (in `resource_instances` and `resource_property_definitions`), `PK_resource_schemas`, `FK_resource_property_definitions_resource_schemas_ResourceSchemaId`, `IX_resource_property_definitions_ResourceSchemaId`, `IX_resource_schemas_GroupId_Name`. Property data-kind names (`DataType`) are not affected.
3. **AC-14** Given `main` with story 015 merged and a database created from `main`, When the migrations of this story (regenerated if the branch is rebased again) are applied and the application starts, Then it starts without errors and the model reports no pending changes.

---

### User Story 3 - The screens keep working and say "schema" (Priority: P3)

As the group owner I open a group's schema list, create a schema, open and edit it, and create a resource from it; everything works as before, and the three places on the group screen that still said "type" now say "schema".

**Why this priority**: the frontend is the only client and ships in the same deployable; it must follow the API in the same story, but it carries no logic of its own.

**Independent Test**: frontend unit tests of the Resources API service (requests go to `/resources/schemas`, payload field `resourceSchemaId`) and of the resources panel (column header, mapping by `resourceSchemaId`); manual walk through the four steps on a running application.

**Acceptance Scenarios**:

1. **AC-8** *(role: owner)* Given the frontend after this story, When the group owner opens a group's schema list, creates a schema, opens and edits it, and creates an instance of it, Then every step works as before this story, now talking to the `/resources/schemas` addresses and sending `resourceSchemaId`; and three texts on the group screen read: resources table column header `Schema` (was `Type`), empty state `Create the first schema to describe your resources.` (was `Create the first schema to define a resource type.`), add-button label `Add resource to schema <name>` (was `Add resource of type <name>`) (DEC-4). The sign-in slogan `One platform for every resource type.` is unchanged.

---

### Message list (AC-7)

All are thrown by the Resources module; status and error code do not change.

| # | Situation | Status / code | Today | After |
|---|---|---|---|---|
| 1 | schema id does not exist: update schema, delete schema, create instance, list instances by schema only | 400 `resources_domain` | `Resource type not found.` | `Resource schema not found.` |
| 2 | non-owner creates a schema | 403 `resources_forbidden` | `Only the group owner may create a resource type.` | `Only the group owner may create a resource schema.` |
| 3 | non-owner updates a schema | 403 `resources_forbidden` | `Only the group owner may update a resource type.` | `Only the group owner may update a resource schema.` |
| 4 | non-owner deletes a schema | 403 `resources_forbidden` | `Only the group owner may delete a resource type.` | `Only the group owner may delete a resource schema.` |
| 5 | schema name empty on create | 400 `resources_domain` | `Resource type name cannot be empty.` | `Resource schema name cannot be empty.` |
| 6 | instance list without `groupId` and without schema filter (ASM-13) | 400 `resources_domain` | `Either GroupId or ResourceTypeId must be provided.` | `Either GroupId or ResourceSchemaId must be provided.` |
| 7 | schema name taken (DEC-2; code, not message) | 409, bare body | code `RESOURCE_TYPE_NAME_TAKEN` | code `RESOURCE_SCHEMA_NAME_TAKEN` (message `A schema with this name already exists in the group.` unchanged) |

Unchanged messages (already neutral): `Access to this group is forbidden.`, `Group not found.`, `Only the group owner may create / update / delete a resource instance.`, `Resource instance not found.`.

### Access baseline (AC-10, AC-11, AC-12)

Recorded by discovery from the code before the rename (F-2, F-5; derived by reading, not captured from a running application - ASM-5). Error bodies are `{ "errors": [ { "code": …, "message": … } ] }` unless stated; G = the group, S = a schema of G, I = an instance of S. "member" = member of G who is not the owner; "outsider" = signed-in user who is neither owner nor member of G. The "after" response equals today's with the route / field renamed and the AC-7 wording.

| # | Call (after the story) | member | outsider |
|---|---|---|---|
| 1 | `POST /resources/schemas` (groupId = G, valid name) | 403 `resources_forbidden` "Only the group owner may create a resource schema." | same as member |
| 2 | `GET /resources/schemas?groupId=G` | 200, list of G's schemas | 403 `resources_forbidden` "Access to this group is forbidden." |
| 3 | `GET /resources/schemas/{S}` | 200, schema with property definitions | 403 `resources_forbidden` "Access to this group is forbidden." |
| 4 | `GET /resources/schemas/{missing id}` | 404, empty body | 404, empty body |
| 5 | `PUT /resources/schemas/{S}` | 403 "Only the group owner may update a resource schema." | same as member |
| 6 | `PUT /resources/schemas/{missing id}` | 400 `resources_domain` "Resource schema not found." | same as member |
| 7 | `DELETE /resources/schemas/{S}` | 403 "Only the group owner may delete a resource schema."; schema and its instances stay active | same as member |
| 8 | `DELETE /resources/schemas/{missing id}` | 400 `resources_domain` "Resource schema not found." | same as member |
| 9 | `POST /resources/instances` (`resourceSchemaId` = S) | 403 "Only the group owner may create a resource instance." | same as member |
| 10 | `POST /resources/instances` (`resourceSchemaId` = missing id) | 400 `resources_domain` "Resource schema not found." | same as member |
| 11 | `GET /resources/instances?resourceSchemaId=S` | 200, instances of S, each row carries `resourceSchemaId` | 403 "Access to this group is forbidden." |
| 12 | `GET /resources/instances?groupId=G&resourceSchemaId=S` | 200, instances of S | 403 "Access to this group is forbidden." |
| 13 | `GET /resources/instances?resourceSchemaId={missing id}` | 400 `resources_domain` "Resource schema not found." | same as member |
| 14 | `GET /resources/instances?groupId=G&includeDeleted=true` | 200, including soft-deleted instances (intended, DEC-3) | 403 "Access to this group is forbidden." |
| 15 | `GET /resources/instances/{I}` | 200, body carries `resourceSchemaId` | 403 "Access to this group is forbidden." |

AC-12 (owner of G, schema S' of another group G'): `GET /resources/instances?groupId=G&resourceSchemaId=S'` -> 200 `{ "items": [], "nextCursor": null }`; `GET /resources/instances?resourceSchemaId=S'` -> 403 `resources_forbidden` "Access to this group is forbidden.".

### Edge Cases

- A request that still sends the old field (stale browser tab): `resourceTypeId` in the create-instance body is ignored, the schema id is empty -> 400 "Resource schema not found."; `resourceTypeId` as the only list filter is ignored -> 400 "Either GroupId or ResourceSchemaId must be provided." No guard, no alias (story out_of_scope, ASM-16).
- A schema name that is empty AND sent by a non-owner: the empty-name check runs before the owner check today (F-2), so the answer is 400, not 403; this order is kept.
- A request without a token: 401 as today on every Resources address; an old `/resources/types` address answers 404 with or without a token - the application has no fallback page, catch-all route or fallback authorization policy (F-7, ASM-19).
- A development database that already contains soft-deleted schemas (`DeletedAt` set): rows and the filtered unique index (`"DeletedAt" IS NULL`) survive the rename; the index keeps its filter under the new name.
- The lifecycle events of story 015 (`ResourceSchemaCreatedEvent`, `ResourceSchemaDeletedEvent`, `ResourceInstanceCreatedEvent`, `ResourceInstanceDeletedEvent`) keep their names, payloads and publishing points.
- `GroupDeleted` still removes the group's schemas and resources (cross-module flow checked by ManagementGroups integration tests, which read the renamed table).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Resources API MUST expose the five schema operations at `/resources/schemas` and `/resources/schemas/{id}` with the same methods, request and response shapes, status codes and authorization as today's `/resources/types` operations (AC-1).
- **FR-002**: The old `/resources/types` addresses MUST NOT exist (AC-2).
- **FR-003**: The schema reference MUST be named `resourceSchemaId` wherever the API carries it: create-instance request body, instance list rows, single instance body, instance list filter (AC-1, ASM-4).
- **FR-004**: The generated API description MUST contain no `ResourceType`, `resourceTypeId`, `resource type(s)` in addresses, model names, fields, parameters or operation names (AC-6).
- **FR-005**: The messages of the Message list MUST carry the new text; status codes and the codes `resources_forbidden` / `resources_domain` MUST NOT change (AC-7, ASM-7).
- **FR-006**: The name-conflict error code MUST be `RESOURCE_SCHEMA_NAME_TAKEN` (DEC-2).
- **FR-007**: Access rules MUST NOT change: writes owner-only, reads owner-or-member, order of checks inside each operation as today (AC-10, AC-11, AC-12, DEC-3).
- **FR-008**: The Resources database objects MUST be renamed by rename operations only (table, columns, indexes) plus re-creation of the primary key and the foreign key under the new names; no table or column is dropped; all rows survive (DEC-1, AC-9, AC-13).
- **FR-009**: Backend code MUST use the new name at full depth (ASM-3): entity `ResourceSchema`, `ResourceSchemaId`, `DbSet ResourceSchemas`, feature slices, requests, results, exception, EF configuration. After the story a case-insensitive search for `resource[ _]?type` in `backend/src` finds only the historical migration files of earlier stories and their designer files (ASM-14).
- **FR-010**: Test code MUST follow the rename (folders, classes, methods, factories, test client, the raw SQL of the ManagementGroups test client); existing tests keep their arrange and assert logic (ASM-14).
- **FR-011**: The frontend MUST call `/resources/schemas` and send `resourceSchemaId`; its hand-written service, DTOs, callers and the two generated API files use the new names; after the story a case-insensitive search for `resourceType` / `resources/types` in `frontend/src` finds nothing, and "resource type" only in the sign-in slogan (ASM-15, DEC-4).
- **FR-012**: The three on-screen texts of AC-8 MUST read as specified (DEC-4).
- **FR-013**: Lifecycle event contracts in `Shared.Abstractions`, `PropertyDataType` / `DataType` / `dataType`, and the "instance" naming MUST NOT change (story out_of_scope).
- **FR-014**: Delivery MUST go in two steps (DEC-5): first a behaviour-neutral internal rename (no change of any address, JSON name, message, error code or database name; proven by the unchanged existing tests, an identical exported API description and no pending model change), then the visible change through the standard delivery phases.

### Key Entities

- **Resource schema** (was: resource type): the template of a kind of resource in a group - name, description, property definitions, soft-delete mark. Table `resources.resource_schemas`.
- **Resource property definition**: a field of a schema (name, data kind, required); refers to its schema by `ResourceSchemaId`.
- **Resource instance**: a concrete resource created from a schema; refers to its schema by `ResourceSchemaId`.
- **Resource property value**: unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the Resources and ManagementGroups integration tests that existed before the story pass after it, changed only in names, addresses, field names and the one error-code literal.
- **SC-002**: A text search for the old name returns zero hits in the generated API description, in the names of the Resources database objects, and in the frontend sources except the sign-in slogan.
- **SC-003**: A development database created before the story starts on the new version with every schema and resource still present.
- **SC-004**: The owner completes the four-step journey (list schemas, create, edit, create a resource) on the running application without noticing any difference other than the three texts.

## Assumptions

- Story 015 is merged; no prerequisite is missing (ASM-1). Blast radius: Resources (all layers), ManagementGroups integration tests, 12 frontend files (ASM-2).
- The baseline was derived by reading the code; JSON property casing is the framework default and is not asserted (ASM-5).
- "Same response" in AC-10..AC-12 is read together with AC-7 and DEC-2 (ASM-17).
- The red-first proof covers the contract criteria (AC-1, 2, 6, 7, 10, 11, 12); the tests of AC-9, AC-13 and AC-14 are green as soon as the developer's migration exists, because the delivery order puts the migration before the blind tests (ASM-18; the one justified exception to constitution V, see plan.md Complexity Tracking).
- The frontend never reads error codes; the generated API client class is imported nowhere and no script regenerates it; bringing the two generated files in line is part of the story (ASM-8, ASM-9, ASM-15).
- The development database is a persistent volume and the application migrates at startup, so the migration alone decides AC-9 (ASM-10).
- Historical migration files keep the old name (they are history); AC-13 speaks about the live database (DEC-1 option "Reset migration history" was not chosen).
- Out of scope, as in the story: any behaviour change, the "instance" naming, event names, the availability epic, the group manager role, rejecting the old field name.
