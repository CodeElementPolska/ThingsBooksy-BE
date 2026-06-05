# Data Model: Booking Availability

**Branch**: `011-booking-availability` | **Date**: 2026-05-28

---

## Shared.Abstractions changes

### Modified event

```csharp
// BREAKING — adds TimeZoneId parameter
public record GroupCreated(Guid GroupId, Guid OwnerId, string TimeZoneId) : IEvent;
```

### New events

```csharp
// Events/Resources/ResourceSchemaCreatedEvent.cs
public record ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId, int BufferMinutes) : IEvent;

// Events/Resources/ResourceInstanceCreatedEvent.cs
public record ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId) : IEvent;

// Events/Calendar/HolidaysRefreshedEvent.cs
public record HolidaysRefreshedEvent(int Year, IReadOnlyList<PublicHolidayDto> Holidays) : IEvent;

// Events/Calendar/PublicHolidayDto.cs (value type carried by HolidaysRefreshedEvent)
public record PublicHolidayDto(DateOnly Date, string LocalName, string Name);
```

---

## ManagementGroups module changes

### Domain entity update

```csharp
// ManagementGroup.cs — add TimeZoneId field
public string TimeZoneId { get; private set; } = null!;

// Create factory — add TimeZoneId from command
public static ManagementGroup Create(CreateManagementGroupCommand command, DateTime now)
    => new() { ..., TimeZoneId = command.TimeZoneId };
```

### Command update

```csharp
// CreateManagementGroupCommand.cs
internal record CreateManagementGroupCommand(string Name, string? Description, Guid OwnerId, string TimeZoneId) : ICommand;
```

### Request DTO update (API layer)

```csharp
// Requests/CreateManagementGroupRequest.cs
internal record CreateManagementGroupRequest(string Name, string? Description, string TimeZoneId);
```

### Event emission update

```csharp
// CreateManagementGroupCommandHandler.cs
await _messageBroker.PublishAsync(new GroupCreated(group.Id, group.OwnerId, group.TimeZoneId), cancellationToken);
```

### EF migration required

New column `time_zone_id character varying NOT NULL` on `management_groups` table (schema: `management_groups`).

---

## Resources module changes

### ResourceType domain entity

```csharp
// ResourceType.cs — add BufferMinutes
public int BufferMinutes { get; private set; }

// Create factory
public static ResourceType Create(CreateResourceTypeCommand command, DateTime now)
    => new() { ..., BufferMinutes = command.BufferMinutes };
```

### Command updates

```csharp
// CreateResourceTypeCommand.cs — add BufferMinutes (default 0)
internal record CreateResourceTypeCommand(Guid GroupId, Guid CallerId, string Name, string? Description,
    IEnumerable<CreatePropertyDefinitionDto> PropertyDefinitions, int BufferMinutes = 0) : ICommand<Guid>;
```

### Event emission in command handlers

```csharp
// CreateResourceTypeCommandHandler — inject IMessageBroker, emit after save
await _messageBroker.PublishAsync(
    new ResourceSchemaCreatedEvent(resourceType.Id, resourceType.GroupId, resourceType.BufferMinutes),
    cancellationToken);

// CreateResourceInstanceCommandHandler — inject IMessageBroker, emit after save
await _messageBroker.PublishAsync(
    new ResourceInstanceCreatedEvent(instance.Id, instance.ResourceTypeId),
    cancellationToken);
```

### EF migration required

New column `buffer_minutes integer NOT NULL DEFAULT 0` on `resource_types` table (schema: `resources`).

---

## Calendar module (NEW)

**EF schema**: `"calendar"`

### Entities

```
PublicHolidayFetchLog
├── Year (int) — PRIMARY KEY
├── FetchedAt (DateTime)
└── HolidayCount (int)
```

### Architecture

- `CalendarDbContext` — `HasDefaultSchema("calendar")`; `DbSet<PublicHolidayFetchLog>`
- `HolidayRefreshService : IHostedService` — uses `PeriodicTimer` (24h interval)
  - On timer tick: checks if current year has `PublicHolidayFetchLog` entry
  - If absent: calls `https://date.nager.at/api/v3/PublicHolidays/{year}/PL` via `IHttpClientFactory`
  - Saves log entry, publishes `HolidaysRefreshedEvent`
- No HTTP API endpoints (Calendar is a background-only module)
- `CalendarModule` registers `IHostedService` + `IHttpClient` + `DbContext`

---

## Availability module (NEW)

**EF schema**: `"availability"`

### Entities

```
GroupReadModel
├── Id (Guid) — PRIMARY KEY (= GroupId)
├── OwnerId (Guid)
└── TimeZoneId (string)

SchemaReadModel
├── Id (Guid) — PRIMARY KEY (= SchemaId)
├── GroupId (Guid)
└── DefaultBufferMinutes (int)

ResourceReadModel
├── Id (Guid) — PRIMARY KEY (= ResourceId)
└── SchemaId (Guid)

PublicHolidayReadModel
├── Id (Guid) — PRIMARY KEY (Guid.CreateVersion7() on upsert)
├── Date (DateOnly)
├── LocalName (string)
├── Name (string)
└── Year (int)
— UNIQUE INDEX on (Year, Date)

SchemaRuleSet
├── Id (Guid) — PRIMARY KEY
├── SchemaId (Guid) — UNIQUE
├── GroupId (Guid)
└── BufferMinutes (int, default 0)

ResourceRuleSet
├── Id (Guid) — PRIMARY KEY
├── ResourceId (Guid) — UNIQUE
├── SchemaId (Guid)
└── BufferMinutesOverride (int?) — NULL = inherit from schema

AvailabilityRule
├── Id (Guid) — PRIMARY KEY
├── SchemaRuleSetId (Guid?) — FK → SchemaRuleSet (nullable; either this or ResourceRuleSetId)
├── ResourceRuleSetId (Guid?) — FK → ResourceRuleSet (nullable)
├── RuleType (enum: Recurring=0 | OneOff=1)
├── RuleMode (enum: Available=0 | Unavailable=1)
├── DaysOfWeek (int[]) — PostgreSQL integer[], NULL for ONE_OFF
├── StartTime (TimeOnly)
├── EndTime (TimeOnly)
├── EndDayOffset (int, 0|1)
├── StartDate (DateOnly?) — NULL for RECURRING
└── EndDate (DateOnly?) — NULL for RECURRING
```

### Design note — single AvailabilityRule table

Both schema-level and resource-level rules share the `AvailabilityRules` table. Each row has either `SchemaRuleSetId` or `ResourceRuleSetId` populated (the other is NULL). This avoids two near-identical tables and simplifies querying. A `CHECK` constraint `(schema_rule_set_id IS NOT NULL) != (resource_rule_set_id IS NOT NULL)` is enforced at the DB level.

Alternatively, two separate tables could be used (`SchemaAvailabilityRules`, `ResourceAvailabilityRules`). Given the identical shape and the YAGNI principle, a single table is preferred.

### EF relationships

```
SchemaRuleSet 1—* AvailabilityRule  (cascade delete)
ResourceRuleSet 1—* AvailabilityRule (cascade delete)
```

### Features (commands / queries)

```
GetSchemaRulesQuery(SchemaId, CallerId)      → AvailabilityRulesQueryResult
UpsertSchemaRulesCommand(SchemaId, CallerId, BufferMinutes, Rules[])  → void (204)

GetResourceRulesQuery(ResourceId, CallerId)   → AvailabilityRulesQueryResult
UpsertResourceRulesCommand(ResourceId, CallerId, BufferMinutesOverride?, Rules[])  → void (204)
```

### Event handlers (in Availability.Core)

```
GroupCreatedHandler         : IEventHandler<GroupCreated>
ResourceSchemaCreatedHandler: IEventHandler<ResourceSchemaCreatedEvent>
ResourceInstanceCreatedHandler: IEventHandler<ResourceInstanceCreatedEvent>
HolidaysRefreshedHandler    : IEventHandler<HolidaysRefreshedEvent>
```

### API endpoints (Availability.Api)

```
GET  /availability/schemas/{schemaId}/rules    → 200 AvailabilityRulesResponse
PUT  /availability/schemas/{schemaId}/rules    ← RuleSetUpdateRequest → 204
GET  /availability/resources/{resourceId}/rules → 200 AvailabilityRulesResponse
PUT  /availability/resources/{resourceId}/rules ← RuleSetUpdateRequest → 204
```

### Response DTOs

```csharp
record AvailabilityRulesResponse(
    int BufferMinutes,
    IReadOnlyList<AvailabilityRuleDto> Rules,
    IReadOnlyList<AvailabilityRuleDto>? InheritedRules);  // null for schema-level GETs

record AvailabilityRuleDto(
    Guid RuleId,
    string RuleType,          // "RECURRING" | "ONE_OFF"
    string RuleMode,          // "AVAILABLE" | "UNAVAILABLE"
    int[]? DaysOfWeek,
    string StartTime,         // HH:mm
    string EndTime,           // HH:mm
    int EndDayOffset,
    string? StartDate,        // ISO date, nullable
    string? EndDate);         // ISO date, nullable

record RuleSetUpdateRequest(int BufferMinutes, IReadOnlyList<NewRuleRequest> Rules);

record NewRuleRequest(
    string RuleType,
    string RuleMode,
    int[]? DaysOfWeek,
    string StartTime,
    string EndTime,
    string? StartDate,
    string? EndDate);
```

---

## Cross-module event flow summary

```
ManagementGroups
  CreateGroup ──► GroupCreated(GroupId, OwnerId, TimeZoneId)
                    ├─► Resources.GroupCreatedHandler (updates GroupReadModel in Resources)
                    └─► Availability.GroupCreatedHandler (upserts GroupReadModel in Availability)

Resources
  CreateResourceType ──► ResourceSchemaCreatedEvent(SchemaId, GroupId, BufferMinutes)
                           └─► Availability.ResourceSchemaCreatedHandler
  CreateResourceInstance ──► ResourceInstanceCreatedEvent(ResourceId, SchemaId)
                               └─► Availability.ResourceInstanceCreatedHandler

Calendar
  HolidayRefreshService ──► HolidaysRefreshedEvent(Year, Holidays[])
                              └─► Availability.HolidaysRefreshedHandler
```

---

## EF migrations needed

| Module           | Migration name                        | Changes |
|------------------|---------------------------------------|---------|
| ManagementGroups | AddTimeZoneIdToManagementGroup        | Add `time_zone_id` column to groups table |
| Resources        | AddBufferMinutesToResourceType        | Add `buffer_minutes` column to resource_types table |
| Calendar         | InitialCalendarSchema                 | Create `public_holiday_fetch_log` table |
| Availability     | InitialAvailabilitySchema             | Create all 6 tables (read-models + rule sets + rules) |
