# Feature Specification: Resources lifecycle events and schema soft delete

**Feature Branch**: `015-resource-time-buffer`  
**Created**: 2026-09-24  
**Status**: Draft (G2 package, discovery round 1)  
**Input**: GitHub issue #10 "[BE] Resources: BufferMinutes w ResourceType (encja, komendy, requesty, GET, migracja)" — rescoped in discovery: the schema buffer (`BufferMinutes`) is owned by the Availability module (DEC-3); this story delivers the lifecycle events and the schema soft-delete rule. Source of truth for scope: `runs/015-resource-time-buffer/story.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Other modules learn when schemas and resources appear and disappear (Priority: P1)

As a group owner I want the Resources module to announce the creation and deletion of resource schemas (`ResourceType`) and resource instances (`ResourceInstance`), so that the future Availability module can keep its own read models and per-schema rules without querying Resources.

**Why this priority**: it is the whole point of the issue after DEC-3 — without these events Availability cannot exist as a decoupled module (constitution: no cross-module queries).

**Independent Test**: create a schema and an instance through the existing HTTP endpoints, delete them, and assert — via the test-side recording broker (ASM-11) — that exactly the specified events were published with the specified payloads after the rows were persisted. No consumer module is required.

**Acceptance Scenarios**:

1. **AC-3** Given a group owner calls `POST /resources/types` with valid data, When the command handler has completed `SaveChangesAsync`, Then `ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId)` is published through `IMessageBroker` with the new schema id and its group id.
2. **AC-4** Given a group owner calls `POST /resources/instances` for an existing schema, When the command handler has completed `SaveChangesAsync`, Then `ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId)` is published with the new instance id and its schema id.
3. **AC-5** Given a schema with N (N >= 0) instances exists, When the group owner calls `DELETE /resources/types/{id}` and `SaveChangesAsync` has completed, Then exactly one `ResourceSchemaDeletedEvent(Guid SchemaId, Guid GroupId)` is published and no `ResourceInstanceDeletedEvent` is published for the N instances soft-deleted as a side effect (DEC-4: the consumer cascades by `SchemaId`).
4. **AC-6** Given an instance exists, When the group owner calls `DELETE /resources/instances/{id}` and `SaveChangesAsync` has completed, Then `ResourceInstanceDeletedEvent(Guid ResourceId, Guid SchemaId)` is published.
5. **AC-7** Given the four event records, When the solution is built, Then they live in `ThingsBooksy.Shared.Abstractions/Events/Resources/` as public positional records implementing `IEvent`, and no other type crosses the Resources module boundary.

---

### User Story 2 - Deleting a schema keeps its history (soft delete) (Priority: P2)

As a group owner I want a deleted schema to be hidden rather than physically removed, consistent with the rule that everything deleted in ThingsBooksy is a soft delete (owner rule, DEC-5), and I still want to be able to reuse its name.

**Why this priority**: it changes existing behaviour (today the schema row is hard-deleted, F-4/F-8) and requires the story's only migration; it is independent of the events but touches the same handler.

**Independent Test**: delete a schema through the API, then read the row with `IgnoreQueryFilters` and re-create a schema with the same name in the same group.

**Acceptance Scenarios**:

1. **AC-8** Given a resource schema with instances exists, When the group owner deletes it through `DELETE /resources/types/{id}`, Then the response is 204, the schema row remains in `resources.resource_types` with `DeletedAt` set (visible only with `IgnoreQueryFilters`), `GET /resources/types/{id}` returns 404, `GET /resources/types?groupId=` no longer lists it, and its instances are soft-deleted exactly as today.
2. **AC-9** Given a resource schema named X was soft-deleted in group G, When the group owner creates a new schema named X in group G, Then the response is 201 Created — the unique name constraint applies only to non-deleted schemas.
3. **AC-10** *(added after G2 by owner decision DEC-6, C5 round 1 finding review-spec-conformance-1-1)* Given a resource schema with property definitions was soft-deleted and had instances with property values, When the group owner calls `GET /resources/instances?groupId={G}&includeDeleted=true`, Then the soft-deleted instances of that schema are listed with their property values carrying the definition's real `Name` and `DataType` (not empty strings) — the schema's property definitions are kept on soft delete (ASM-15) and this read path is the one place where that is user-visible. FR-009 ("read paths MUST NOT change") is narrowed accordingly: no route, request or response *shape* changes; this value change is accepted.

---

### Edge Cases

- Deleting a schema or instance that is already soft-deleted: the load goes through the global query filter, so the existing "not found" domain error (HTTP 400, `ResourcesDomainException`) is returned and **no event is published** (ASM-19).
- Deleting a schema with zero instances: one `ResourceSchemaDeletedEvent`, nothing else (AC-5 with N = 0).
- Group deletion (`GroupDeleted` handled by `GroupDeletedHandler`): bulk SQL path, **no per-schema or per-instance events**; Availability reacts to `GroupDeleted` itself (out of scope, story.md).
- Creating an instance for a soft-deleted schema: rejected with the existing "Resource type not found." error via the query filter (ASM-13) — no new code.
- Publish failure after a successful `SaveChangesAsync`: the row is committed and the event is lost (in-process broker, outbox disabled — ASM-18); this is the existing ManagementGroups behaviour and is accepted for this story.
- Property definitions of a soft-deleted schema stay in place and are hidden with the schema (ASM-15).
- A command that fails before persistence (403 not owner, 400 not found / validation) publishes **no** event — publication is reachable only after a successful `SaveChangesAsync` (FR-005).
- Restoring a soft-deleted schema is not part of this story (no endpoint, no `Restore` call); the row is kept for history and for a future restore feature only.
- Two owners racing to create the same name after a soft delete: the partial unique index still rejects the second insert at the database (existing 409 mapping via the app-level check is best-effort; a DB unique violation surfaces as 500 today — unchanged behaviour, out of scope).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST publish `ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId)` after a resource schema has been persisted by `CreateResourceTypeCommandHandler` (AC-3).
- **FR-002**: The system MUST publish `ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId)` after a resource instance has been persisted by `CreateResourceInstanceCommandHandler` (AC-4).
- **FR-003**: The system MUST publish exactly one `ResourceSchemaDeletedEvent(Guid SchemaId, Guid GroupId)` after `DeleteResourceTypeCommandHandler` has persisted the deletion, and MUST NOT publish per-instance events for the instances soft-deleted in the same command (AC-5, DEC-4).
- **FR-004**: The system MUST publish `ResourceInstanceDeletedEvent(Guid ResourceId, Guid SchemaId)` after `DeleteResourceInstanceCommandHandler` has persisted the deletion (AC-6).
- **FR-005**: Every publication MUST happen after `SaveChangesAsync` and inside the same command handler, via `IMessageBroker.PublishAsync`, following the ManagementGroups convention (F-6) — never before persistence and never from a data provider.
- **FR-006**: The four event contracts MUST be defined in `ThingsBooksy.Shared.Abstractions/Events/Resources/`, one public positional record per file, implementing `IEvent` (AC-7, ASM-17); names as written in the issue (ASM-5).
- **FR-007**: `DeleteResourceTypeCommandHandler` MUST soft-delete the schema by calling the existing `ResourceType.Delete(now)` instead of removing the row (AC-8, DEC-5); instance soft-deletion stays as today (bulk `ExecuteUpdateAsync`).
- **FR-008**: The unique index on `resource_types (GroupId, Name)` MUST become partial (`"DeletedAt" IS NULL`), delivered in the story's single migration, so that the name of a soft-deleted schema can be reused (AC-9, DEC-5); the app-level uniqueness check already ignores soft-deleted rows (F-8).
- **FR-009**: Read paths MUST NOT change: soft-deleted schemas are hidden by the existing global query filter (GET 404, list excludes, instance creation rejects) (ASM-13).
- **FR-010**: The HTTP contract MUST NOT change: same routes, request and response shapes and status codes as `generated/swagger.base.json` (`runs/015-resource-time-buffer/contract-delta.overlay.json` is annotation-only).
- **FR-011**: The `GroupDeleted` bulk path MUST stay as is and MUST NOT publish per-schema/per-instance events (out of scope, ASM-4).
- **FR-012**: Command handlers gaining `IMessageBroker` MUST keep their constructor at or below 4 parameters (convention `domain-entity-design.md` applies to entities; handlers follow `data-provider-pattern.md`: `IXxxDataProvider`, `IClock`, `IMessageBroker`).

### Key Entities *(include if feature involves data)*

- **ResourceType (schema)**: existing entity; gains no new columns. Its `DeletedAt` (already present) becomes the delete marker; unique `(GroupId, Name)` index becomes partial.
- **ResourceInstance**: unchanged; already soft-deleted.
- **ResourceSchemaCreatedEvent / ResourceSchemaDeletedEvent**: `(Guid SchemaId, Guid GroupId)` — `SchemaId` is the `ResourceType.Id`.
- **ResourceInstanceCreatedEvent / ResourceInstanceDeletedEvent**: `(Guid ResourceId, Guid SchemaId)` — `ResourceId` is the `ResourceInstance.Id`.

## Decisions taken in discovery (owner, `decisions.jsonl`)

| Id | Decision |
|---|---|
| DEC-3 | Availability owns the schema buffer; `BufferMinutes` leaves this story (AC-1, AC-2 removed; event payloads without buffer). |
| DEC-4 | On schema delete only `ResourceSchemaDeletedEvent` is published; consumers cascade by `SchemaId`. |
| DEC-5 | Schema delete becomes a soft delete in the DELETE command only; partial unique index in the story's migration; `GroupDeleted` path unchanged. |
| DEC-1, DEC-2 | Withdrawn (buffer limit and buffer migration no longer apply). |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For each of the four lifecycle commands, an integration test proves that exactly the specified event with the specified payload is published after the row is persisted (AC-3..AC-6 each carry a `[Trait("AC","AC-n")]` test).
- **SC-002**: After `DELETE /resources/types/{id}`, the schema row still exists in the database with `DeletedAt` set, and all read endpoints behave as if it did not exist (AC-8).
- **SC-003**: A schema name can be reused in the same group after its predecessor was soft-deleted, without a 409 or a database error (AC-9).
- **SC-004**: `generated/swagger.base.json` re-exported after implementation equals `runs/015-resource-time-buffer/contract-next.json` (contract-diff CLEAR) — zero HTTP contract drift.
- **SC-005**: Exactly one new migration in `Resources.Migrations`; no new table or column.

## Assumptions

Full list with scores and consequences: `runs/015-resource-time-buffer/discovery/assumptions.jsonl` (ASM-1..ASM-19; ASM-1, 2, 7, 8, 9 withdrawn after DEC-3, ASM-12 folded into DEC-5). The ones that shape the implementation:

- ASM-3 / ASM-18: in-process async broker, outbox disabled; publish order is guaranteed by call order in the handler only.
- ASM-5: event names keep the `Event` suffix as written in the issue, although existing events (`GroupCreated`) have none.
- ASM-6: deleted-event payloads mirror the created ones.
- ASM-11: tests observe events through a recording `IMessageBroker` decorator registered in `ThingsBooksyWebAppFactory` (test projects only).
- ASM-14: the two existing tests asserting hard delete (`UpdateDeleteResourceTypeTests.cs:185`, `:283`) are rewritten to assert soft delete.
- ASM-15: property definitions of a soft-deleted schema are left untouched.
- ASM-16: migration name `SoftDeleteResourceTypeUniqueIndex`.
- ASM-19: deleting an already deleted schema/instance returns the existing not-found error and publishes nothing.
- ASM-10: no frontend change; the TS client is regenerated only when a screen needs it.
