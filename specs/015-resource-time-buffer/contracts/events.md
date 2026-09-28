# Contracts — 015-resource-time-buffer

## HTTP contract

**No change.** `runs/015-resource-time-buffer/contract-delta.overlay.json` contains a single annotation action on `$.info`; `contract-next.json` equals `generated/swagger.base.json` operation-for-operation. `DELETE /resources/types/{id}` keeps `204 No Content`; its persistence semantics change (soft delete) without any visible contract change.

## Integration events (C#)

Folder: `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/Resources/` — one file per record.

```csharp
namespace ThingsBooksy.Shared.Abstractions.Events.Resources;

public record ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId) : IEvent;
public record ResourceSchemaDeletedEvent(Guid SchemaId, Guid GroupId) : IEvent;
public record ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId) : IEvent;
public record ResourceInstanceDeletedEvent(Guid ResourceId, Guid SchemaId) : IEvent;
```

Namespace follows the existing `Events/ManagementGroups` files (verify the exact namespace of `GroupCreated.cs` when implementing and mirror it).

### Semantics promised to consumers

| Event | Guarantee |
|---|---|
| `ResourceSchemaCreatedEvent` | The schema row is committed before publication. |
| `ResourceSchemaDeletedEvent` | The schema row has `DeletedAt` set and all its instances are soft-deleted before publication. **Exactly one** event per delete; consumers must remove everything they hold for `SchemaId` (rules and instance read models) — no per-instance events follow. |
| `ResourceInstanceCreatedEvent` | The instance row is committed; `SchemaId` links it to a previously announced schema. |
| `ResourceInstanceDeletedEvent` | The instance row has `DeletedAt` set. Not emitted for instances deleted as a side effect of schema deletion or group deletion. |

Delivery: in-process asynchronous dispatch (`InMemoryMessageBroker` → channel → `IEventHandler<T>`); at-most-once, no outbox (ASM-18). Group deletion continues to be announced only by `GroupDeleted` (ManagementGroups).
