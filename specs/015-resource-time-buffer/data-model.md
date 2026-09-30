# Data Model — 015-resource-time-buffer

## Entities (Resources module, schema `resources`)

### ResourceType (schema) — existing, no new columns

| Field | Type | Notes |
|---|---|---|
| Id | Guid (v7) | PK |
| GroupId | Guid | reference to ManagementGroups group (read model) |
| Name | string(200) | required; unique per group **among non-deleted rows** (this story) |
| Description | string(2000)? | |
| CreatedAt / UpdatedAt | DateTime | |
| DeletedAt | DateTime? | **becomes the delete marker for `DELETE /resources/types/{id}`** (was unused on that path) |
| PropertyDefinitions | ICollection<ResourcePropertyDefinition> | cascade on hard delete only; untouched by soft delete (ASM-15) |

Index change (only schema change of the story):

```csharp
// ResourceTypeConfiguration.cs
builder.HasIndex(t => new { t.GroupId, t.Name })
    .IsUnique()
    .HasFilter("\"DeletedAt\" IS NULL");   // was: no filter
```

Migration `SoftDeleteResourceTypeUniqueIndex`: drop `IX_resource_types_GroupId_Name`, recreate with `unique: true, filter: "\"DeletedAt\" IS NULL"`. No data migration needed (no soft-deleted rows can exist today — deletes were physical).

**State transitions**

```
[active] --DELETE /resources/types/{id} (owner)--> [soft-deleted: DeletedAt = now]
   |                                                  |- hidden by global query filter (GET 404, list excludes, instance creation rejects)
   |                                                  |- name free for a new active schema
   `-- GroupDeleted event --> [physically removed]   (unchanged bulk path, out of scope)
```

### ResourceInstance — existing, unchanged

Already soft-deleted (`DeletedAt`, global filter, partial index `(GroupId, Id)`). Bulk soft delete when its schema is deleted stays as is.

## Event contracts (Shared.Abstractions/Events/Resources/)

| Record | Fields | Published by | When |
|---|---|---|---|
| `ResourceSchemaCreatedEvent` | `Guid SchemaId, Guid GroupId` | `CreateResourceTypeCommandHandler` | after `SaveChangesAsync` |
| `ResourceSchemaDeletedEvent` | `Guid SchemaId, Guid GroupId` | `DeleteResourceTypeCommandHandler` | after `SaveChangesAsync`; exactly one per delete |
| `ResourceInstanceCreatedEvent` | `Guid ResourceId, Guid SchemaId` | `CreateResourceInstanceCommandHandler` | after `SaveChangesAsync` |
| `ResourceInstanceDeletedEvent` | `Guid ResourceId, Guid SchemaId` | `DeleteResourceInstanceCommandHandler` | after `SaveChangesAsync` |

`SchemaId` = `ResourceType.Id`; `ResourceId` = `ResourceInstance.Id`. All records implement `IEvent`; no module-specific types (constitution IV).

## Validation rules touched

- Uniqueness of `Name` per group: unchanged at application level (already `DeletedAt == null`, F-8); DB now consistent with it.
- Delete of an already soft-deleted schema/instance: existing not-found path (`ResourcesDomainException`, 400); no event (ASM-19).
