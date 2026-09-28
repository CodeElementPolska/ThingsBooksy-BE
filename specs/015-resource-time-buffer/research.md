# Research — 015-resource-time-buffer

There are no `NEEDS CLARIFICATION` items in the Technical Context: every unknown was resolved in discovery (`runs/015-resource-time-buffer/discovery/`) with owner decisions or code facts. This file records the resolutions in the SpecKit format.

## R1 — Who owns the schema buffer (`BufferMinutes`)

- **Decision**: Availability owns it as a per-schema rule; it leaves this story (DEC-3, owner).
- **Rationale**: the buffer is a scheduling rule with one consumer; epic decision no. 2 already assumes Availability keeps rules keyed by `SchemaId`; keeping it in Resources would create two sources of truth and a permanent sync (Updated event + handler) for a value Resources never uses.
- **Alternatives considered**: Resources master + `ResourceSchemaUpdatedEvent`; Resources master without update event (cache drift); RETURN to business session.

## R2 — Events on schema delete

- **Decision**: exactly one `ResourceSchemaDeletedEvent(SchemaId, GroupId)`; consumers cascade by `SchemaId` (DEC-4, owner).
- **Rationale**: matches the epic rule ("deleting a schema hard-deletes its rules"), keeps the bulk `ExecuteUpdateAsync` path (F-4) untouched, and the consumer already knows instance→schema from `ResourceInstanceCreatedEvent`.
- **Alternatives considered**: schema event + N per-instance events (handler would need to load ids; event burst).

## R3 — Soft delete of the schema

- **Decision**: `DELETE /resources/types/{id}` soft-deletes via existing `ResourceType.Delete(now)`; `GroupDeleted` bulk path unchanged (DEC-5, owner; rule "everything deleted is a soft delete").
- **Rationale**: entity, `DeletedAt` column and global query filter already exist (F-2); only the handler and the unique index change.
- **Consequence**: `IX_resource_types_GroupId_Name` must become partial — the app-level uniqueness check already excludes `DeletedAt != null` (F-8 `CreateResourceTypeCommandDataProvider.cs:21`), so without the partial index a name reuse would pass validation and fail on the DB constraint. Pattern: `ManagementGroupConfiguration.cs:14` `HasIndex(...).IsUnique().HasFilter("\"DeletedAt\" IS NULL")`.
- **Alternatives considered**: soft delete in both paths (scope creep into group deletion); keep hard delete (contradicts owner rule).

## R4 — Publishing convention

- **Decision**: `await _messageBroker.PublishAsync(new X(...), ct)` immediately after `await _dataProvider.SaveChangesAsync(ct)` inside the command handler (F-6 `CreateManagementGroupCommandHandler.cs:46`).
- **Rationale**: identical to ManagementGroups; `InMemoryMessageBroker` routes to the in-process channel (outbox disabled, F-7) — no transactional guarantee is claimed by this story (ASM-18).
- **Alternatives considered**: enabling the outbox (out of scope; would be a cross-module infrastructure change).

## R5 — Event record shape and placement

- **Decision**: `public record ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId) : IEvent;` etc., one file each in `Shared.Abstractions/Events/Resources/` (ASM-6, ASM-17; names per ASM-5).
- **Rationale**: mirrors `GroupCreated.cs:3`; deleted payloads mirror created payloads so a consumer can remove what it stored.

## R6 — Observing events in integration tests

- **Decision**: a `RecordingMessageBroker` decorator over `IMessageBroker`, registered in `ThingsBooksyWebAppFactory.ConfigureTestServices`, exposing the captured `IMessage` list; tests assert captured events + persisted rows (ASM-11).
- **Rationale**: F-7 — no sink exists; existing tests prove publication only via consumer side effects, and the consumer module does not exist. The decorator captures at the call site, independent of async dispatch timing, and stays in test projects only.
- **Alternatives considered**: test-only `IEventHandler<T>` per event (4 classes, needs assembly registration); polling a consumer read model (no consumer).

## R7 — Existing tests that contradict the new behaviour

- **Decision**: rewrite `UpdateDeleteResourceTypeTests.cs:185` and `:283` to assert the row exists with `IgnoreQueryFilters` and has `DeletedAt` set (ASM-14).
