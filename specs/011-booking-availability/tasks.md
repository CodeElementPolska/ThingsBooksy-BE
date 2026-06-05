# Tasks: Booking Availability

**Input**: Design documents from `specs/011-booking-availability/`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ api-contract.md ✅

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story label — [US1] Schema rules, [US2] Resource rules, [US3] Holiday calendar, [US4] Group timezone
- Integration tests required per constitution — included in each module's phase

---

## Phase 1: Setup — Shared.Abstractions (Pre-Wave)

**Purpose**: Extend the shared event contracts. Must complete before ANY module implementation since GroupCreated is BREAKING and Resources/Availability/Calendar all depend on the new event shapes.

**⚠️ CRITICAL**: All downstream modules will fail to compile until this phase is complete.

- [ ] T001 [US4] Extend `GroupCreated` record to add `string TimeZoneId` parameter in `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/ManagementGroups/GroupCreated.cs`
- [ ] T002 [P] Create `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/Resources/` directory and add `ResourceSchemaCreatedEvent.cs`: `public record ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId, int BufferMinutes) : IEvent;`
- [ ] T003 [P] Create `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/Resources/ResourceInstanceCreatedEvent.cs`: `public record ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId) : IEvent;`
- [ ] T004 [P] Create `backend/src/Shared/ThingsBooksy.Shared.Abstractions/Events/Calendar/` directory with `HolidaysRefreshedEvent.cs` (`record HolidaysRefreshedEvent(int Year, IReadOnlyList<PublicHolidayDto> Holidays) : IEvent`) and `PublicHolidayDto.cs` (`record PublicHolidayDto(DateOnly Date, string LocalName, string Name)`)

**Checkpoint**: `dotnet build backend/ThingsBooksy.slnx` will have compile errors in Resources and ManagementGroups until Phase 2 fixes them — that is expected and acceptable. Shared.Abstractions itself must build cleanly.

---

## Phase 2: Foundational — Modified Modules (Wave 1)

**Purpose**: Fix BREAKING compile errors from Phase 1, add TimeZoneId to ManagementGroups (US4), add BufferMinutes + event emission to Resources. Both modules can be worked on in parallel.

**⚠️ CRITICAL**: No Availability or Calendar module work should begin until this phase is complete (Availability reads GroupReadModel which depends on GroupCreated with TimeZoneId).

### ManagementGroups module (US4 — Group timezone context)

- [ ] T005 [US4] Add `TimeZoneId` property to `ManagementGroup` domain entity in `backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.Core/Domain/ManagementGroup.cs`: add `public string TimeZoneId { get; private set; } = null!;` and update `Create` factory to set it from command
- [ ] T006 [US4] Update `CreateManagementGroupCommand` in `backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.Core/Features/CreateManagementGroup/CreateManagementGroupCommand.cs` to add `string TimeZoneId` parameter
- [ ] T007 [US4] Update `CreateManagementGroupRequest` in `backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.Api/Requests/CreateManagementGroupRequest.cs` to add `string TimeZoneId` property
- [ ] T008 [US4] Update `CreateManagementGroupCommandHandler` in `backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.Core/Features/CreateManagementGroup/CreateManagementGroupCommandHandler.cs`: add IANA timezone validation (`TimeZoneInfo.TryFindSystemTimeZoneById`), update `GroupCreated` emission to `new GroupCreated(group.Id, group.OwnerId, group.TimeZoneId)`
- [ ] T009 [US4] Update `ManagementGroupConfiguration` in `backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.Core/DAL/Configurations/ManagementGroupConfiguration.cs` to map `TimeZoneId` column
- [ ] T010 [US4] Update `ManagementGroupsModule.cs` endpoint lambda to pass `request.TimeZoneId` into `CreateManagementGroupCommand`
- [ ] T011 [US4] Add EF migration `AddTimeZoneIdToManagementGroup` for ManagementGroups module: `dotnet ef migrations add AddTimeZoneIdToManagementGroup --project backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.Migrations --startup-project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`
- [ ] T012 [P] [US4] Write integration tests for timezone validation in `backend/src/Modules/ManagementGroups/ThingsBooksy.Modules.ManagementGroups.IntegrationTests/`: `CreateManagementGroup_WithValidTimeZone_ReturnsCreated`, `CreateManagementGroup_WithInvalidTimeZone_ReturnsBadRequest`, `CreateManagementGroup_GroupCreatedEventContainsTimeZoneId`

### Resources module (US1/US2 — event emission + buffer minutes)

- [ ] T013 [P] Add `BufferMinutes` property to `ResourceType` entity in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Domain/ResourceType.cs`: add `public int BufferMinutes { get; private set; }` and update `Create` factory
- [ ] T014 [P] Update `CreateResourceTypeCommand` in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Features/CreateResourceType/CreateResourceTypeCommand.cs` to add `int BufferMinutes = 0` parameter
- [ ] T015 [P] Update `CreateResourceTypeRequest` in the Resources API layer (create if missing: `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Api/Requests/CreateResourceTypeRequest.cs`) to include `BufferMinutes` field; update the endpoint lambda to pass it to the command
- [ ] T016 Inject `IMessageBroker` into `CreateResourceTypeCommandHandler` in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Features/CreateResourceType/CreateResourceTypeCommandHandler.cs`; emit `new ResourceSchemaCreatedEvent(resourceType.Id, resourceType.GroupId, resourceType.BufferMinutes)` after `SaveChangesAsync`
- [ ] T017 Inject `IMessageBroker` into `CreateResourceInstanceCommandHandler` in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Features/CreateResourceInstance/CreateResourceInstanceCommandHandler.cs`; emit `new ResourceInstanceCreatedEvent(instance.Id, instance.ResourceTypeId)` after `SaveChangesAsync`
- [ ] T018 Fix compile error in `GroupCreatedHandler` in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/Events/Handlers/GroupCreatedHandler.cs` — `GroupReadModel.Upsert(@event)` must handle the new `TimeZoneId` field (update `GroupReadModel.Upsert` to compile; Resources' GroupReadModel does not need to store TimeZoneId)
- [ ] T019 Update `ResourceTypeConfiguration` in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core/DAL/Configurations/ResourceTypeConfiguration.cs` to map `BufferMinutes` column
- [ ] T020 Add EF migration `AddBufferMinutesToResourceType` for Resources module: `dotnet ef migrations add AddBufferMinutesToResourceType --project backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Migrations --startup-project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`
- [ ] T021 [P] Write integration tests for Resources event emission in `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests/`: `CreateResourceType_EmitsResourceSchemaCreatedEvent`, `CreateResourceInstance_EmitsResourceInstanceCreatedEvent`, `CreateResourceType_WithBufferMinutes_PersistsCorrectly`

**Checkpoint**: `dotnet build backend/ThingsBooksy.slnx` must pass cleanly. All existing ManagementGroups and Resources integration tests must still pass.

---

## Phase 3: Calendar Module (Wave 2-A) [US3]

**Purpose**: New Calendar module that periodically fetches Polish public holidays from Nager.Date API and emits `HolidaysRefreshedEvent`. No HTTP endpoints — background service only.

**Goal**: Holiday data is available in the system within 24h of application startup for the current calendar year.

**Independent Test**: Start the application with a fresh DB → wait for `HolidayRefreshService` to run → assert `HolidaysRefreshedEvent` was published → assert `PublicHolidayFetchLog` row exists for the current year.

- [ ] T022 [US3] Scaffold Calendar module using `module-scaffolder` agent for `Calendar` module — creates `ThingsBooksy.Modules.Calendar.Api`, `ThingsBooksy.Modules.Calendar.Core`, `ThingsBooksy.Modules.Calendar.Migrations`, `ThingsBooksy.Modules.Calendar.IntegrationTests` projects, registers in `backend/ThingsBooksy.slnx`, patches `ThingsBooksyWebAppFactory.cs`
- [ ] T023 [P] [US3] Create `PublicHolidayFetchLog` domain entity in `backend/src/Modules/Calendar/ThingsBooksy.Modules.Calendar.Core/Domain/PublicHolidayFetchLog.cs` with properties `Year (int PK)`, `FetchedAt (DateTime)`, `HolidayCount (int)`; include `private` constructor, static `Create(int year, DateTime now, int count)` factory
- [ ] T024 [P] [US3] Create `CalendarDbContext` in `backend/src/Modules/Calendar/ThingsBooksy.Modules.Calendar.Core/DAL/CalendarDbContext.cs` with `HasDefaultSchema("calendar")`; `DbSet<PublicHolidayFetchLog> FetchLogs`; create EF configuration `PublicHolidayFetchLogConfiguration.cs` with year as integer PK (not Guid)
- [ ] T025 [US3] Create `HolidayRefreshService : IHostedService` in `backend/src/Modules/Calendar/ThingsBooksy.Modules.Calendar.Core/Services/HolidayRefreshService.cs` using `PeriodicTimer` with 24h interval; on each tick check if `FetchLogs` has entry for current year; if not, call Nager.Date API for current year
- [ ] T026 [US3] Add Nager.Date HTTP client to `HolidayRefreshService`: use `IHttpClientFactory` to GET `https://date.nager.at/api/v3/PublicHolidays/{year}/PL`; deserialize JSON array of `{date, localName, name}`; map to `PublicHolidayDto` list
- [ ] T027 [US3] After successful fetch: save `PublicHolidayFetchLog` entry via `CalendarDbContext`; publish `HolidaysRefreshedEvent(year, holidays)` via `IMessageBroker` in `HolidayRefreshService`
- [ ] T028 [US3] Create `CalendarModule.cs` in `backend/src/Modules/Calendar/ThingsBooksy.Modules.Calendar.Api/CalendarModule.cs`: register `IHostedService`, `IHttpClientFactory` with base address `https://date.nager.at/`, `CalendarDbContext`; no HTTP endpoints (empty `Expose()`)
- [ ] T029 [US3] Create `Extensions.cs` in `backend/src/Modules/Calendar/ThingsBooksy.Modules.Calendar.Core/Extensions.cs` with `AddCalendarCore()` extension; add 4 `InternalsVisibleTo` attributes: `.Api`, `.Migrations`, `.IntegrationTests`, `DynamicProxyGenAssembly2`; register `HolidayRefreshService` as `IHostedService`
- [ ] T030 [US3] Add EF migration `InitialCalendarSchema`: `dotnet ef migrations add InitialCalendarSchema --project backend/src/Modules/Calendar/ThingsBooksy.Modules.Calendar.Migrations --startup-project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`; verify `"calendar"` schema is created
- [ ] T031 [US3] Register Calendar module in Bootstrapper — add `new CalendarModule()` to the modules list in `backend/src/Bootstrapper/ThingsBooksy.Bootstrapper/`; add `ThingsBooksy.Modules.Calendar.IntegrationTests` to `SchemasToInclude` in `ThingsBooksyWebAppFactory.cs`
- [ ] T032 [P] [US3] Write integration tests for Calendar in `backend/src/Modules/Calendar/ThingsBooksy.Modules.Calendar.IntegrationTests/`: `RefreshHolidays_WhenNoLogForCurrentYear_EmitsHolidaysRefreshedEvent`, `RefreshHolidays_WhenLogExists_SkipsRefresh`, `RefreshHolidays_PersistsFetchLog`

**Checkpoint**: Calendar module builds, its migration applies, and the integration tests pass. `HolidaysRefreshedEvent` is published to the in-memory broker on startup.

---

## Phase 4: Availability Module (Wave 2-B) [US1, US2, US3]

**Purpose**: New Availability module — the core of this feature. Stores availability rules for schemas and resources. Maintains read-models from events. Exposes 4 HTTP endpoints (GET + PUT for schemas and resources).

**Goal**: Admin can PUT a rule set for a schema, GET it back, and resource-level GET merges inherited schema rules with resource overrides.

**Independent Test**: 
- PUT 3 RECURRING rules for a schema → GET → assert 3 rules returned.
- PUT 1 ONE_OFF UNAVAILABLE + 1 RECURRING AVAILABLE for a resource → GET → assert `rules` has 2 entries and `inheritedRules` shows schema rules.
- PUT 2 overlapping AVAILABLE RECURRING rules → assert 400 conflict.

### Scaffold and domain model

- [ ] T033 [US1] Scaffold Availability module using `module-scaffolder` agent for `Availability` module — creates all 4 projects, registers in `.slnx`, patches `ThingsBooksyWebAppFactory.cs`
- [ ] T034 [P] [US1] Create `RuleType.cs` enum and `RuleMode.cs` enum in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Domain/`: `RuleType { Recurring = 0, OneOff = 1 }` and `RuleMode { Available = 0, Unavailable = 1 }`
- [ ] T035 [P] [US1] Create `SchemaRuleSet.cs` domain entity in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Domain/SchemaRuleSet.cs`: `Id (Guid)`, `SchemaId (Guid)`, `GroupId (Guid)`, `BufferMinutes (int)`, private ctor, `Create(Guid schemaId, Guid groupId, int bufferMinutes)` factory, `ReplaceRules(IReadOnlyList<AvailabilityRule>)` method
- [ ] T036 [P] [US1] Create `ResourceRuleSet.cs` domain entity in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Domain/ResourceRuleSet.cs`: `Id (Guid)`, `ResourceId (Guid)`, `SchemaId (Guid)`, `BufferMinutesOverride (int?)`, private ctor, `Create(Guid resourceId, Guid schemaId)` factory, `ReplaceRules(...)` method
- [ ] T037 [US1] Create `AvailabilityRule.cs` entity in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Domain/AvailabilityRule.cs`: all fields per data-model.md (`RuleType`, `RuleMode`, `DaysOfWeek int[]`, `StartTime TimeOnly`, `EndTime TimeOnly`, `EndDayOffset int`, `StartDate DateOnly?`, `EndDate DateOnly?`); private ctor; `CreateForSchemaRuleSet(...)` and `CreateForResourceRuleSet(...)` factory methods that auto-compute `EndDayOffset` when `endTime < startTime`

### Read-models

- [ ] T038 [P] [US1] Create `GroupReadModel.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/ReadModels/GroupReadModel.cs`: `Id (Guid=GroupId)`, `OwnerId (Guid)`, `TimeZoneId (string)`; `internal static GroupReadModel Upsert(GroupCreated @event)`
- [ ] T039 [P] [US1] Create `SchemaReadModel.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/ReadModels/SchemaReadModel.cs`: `Id (Guid=SchemaId)`, `GroupId (Guid)`, `DefaultBufferMinutes (int)`; `internal static SchemaReadModel Upsert(ResourceSchemaCreatedEvent @event)`
- [ ] T040 [P] [US2] Create `ResourceReadModel.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/ReadModels/ResourceReadModel.cs`: `Id (Guid=ResourceId)`, `SchemaId (Guid)`; `internal static ResourceReadModel Upsert(ResourceInstanceCreatedEvent @event)`
- [ ] T041 [P] [US3] Create `PublicHolidayReadModel.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/ReadModels/PublicHolidayReadModel.cs`: `Id (Guid PK, Guid.CreateVersion7())`, `Date (DateOnly)`, `LocalName (string)`, `Name (string)`, `Year (int)`; `internal static PublicHolidayReadModel Create(PublicHolidayDto dto)`

### Persistence

- [ ] T042 [US1] Create `AvailabilityDbContext.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/DAL/AvailabilityDbContext.cs` with `HasDefaultSchema("availability")`; DbSets for `SchemaRuleSets`, `ResourceRuleSets`, `AvailabilityRules`, `GroupReadModels`, `SchemaReadModels`, `ResourceReadModels`, `PublicHolidayReadModels`
- [ ] T043 [US1] Create EF configurations in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/DAL/Configurations/`: `SchemaRuleSetConfiguration.cs` (unique index on SchemaId), `ResourceRuleSetConfiguration.cs` (unique index on ResourceId), `AvailabilityRuleConfiguration.cs` (maps `DaysOfWeek` as `integer[]` PostgreSQL array; cascade delete; maps `TimeOnly` and `DateOnly` columns), `GroupReadModelConfiguration.cs`, `SchemaReadModelConfiguration.cs`, `ResourceReadModelConfiguration.cs`, `PublicHolidayReadModelConfiguration.cs` (unique index on Year+Date)
- [ ] T044 [US1] Add EF migration `InitialAvailabilitySchema`: `dotnet ef migrations add InitialAvailabilitySchema --project backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Migrations --startup-project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper`; verify `"availability"` schema with all 7 tables

### Event handlers

- [ ] T045 [P] [US4] Create `GroupCreatedHandler.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Events/Handlers/GroupCreatedHandler.cs`: `IEventHandler<GroupCreated>` — upserts `GroupReadModel` via `AvailabilityDbContext`
- [ ] T046 [P] [US1] Create `ResourceSchemaCreatedHandler.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Events/Handlers/ResourceSchemaCreatedHandler.cs`: `IEventHandler<ResourceSchemaCreatedEvent>` — upserts `SchemaReadModel`
- [ ] T047 [P] [US2] Create `ResourceInstanceCreatedHandler.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Events/Handlers/ResourceInstanceCreatedHandler.cs`: `IEventHandler<ResourceInstanceCreatedEvent>` — upserts `ResourceReadModel`
- [ ] T048 [P] [US3] Create `HolidaysRefreshedHandler.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Events/Handlers/HolidaysRefreshedHandler.cs`: `IEventHandler<HolidaysRefreshedEvent>` — bulk-upsert `PublicHolidayReadModel` entries for the given year (delete existing for that year, insert new ones)

### Feature: GetSchemaRules (US1)

- [ ] T049 [P] [US1] Create `GetSchemaRulesQuery.cs` and `GetSchemaRulesQueryResult.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Features/GetSchemaRules/`: query takes `(Guid SchemaId, Guid CallerId)`; result contains `AvailabilityRulesQueryResult(int BufferMinutes, IReadOnlyList<AvailabilityRuleResult> Rules, IReadOnlyList<AvailabilityRuleResult>? InheritedRules)`
- [ ] T050 [US1] Create `IGetSchemaRulesQueryDataProvider.cs` and `GetSchemaRulesQueryDataProvider.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Features/GetSchemaRules/DataProviders/`: methods `GetSchemaReadModelAsync`, `GetGroupReadModelAsync`, `GetSchemaRuleSetAsync`
- [ ] T051 [US1] Create `GetSchemaRulesQueryHandler.cs`: authorize caller (compare `callerId` to `GroupReadModel.OwnerId`); if `SchemaRuleSet` does not exist return 200 with empty rules (not 404); map rules to result DTOs; `InheritedRules` is null for schema-level queries

### Feature: UpsertSchemaRules (US1)

- [ ] T052 [P] [US1] Create `UpsertSchemaRulesCommand.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Features/UpsertSchemaRules/`: `record UpsertSchemaRulesCommand(Guid SchemaId, Guid CallerId, int BufferMinutes, IReadOnlyList<NewAvailabilityRuleDto> Rules) : ICommand`
- [ ] T053 [US1] Create `IUpsertSchemaRulesCommandDataProvider.cs` and `UpsertSchemaRulesCommandDataProvider.cs` in `DataProviders/`: methods `GetSchemaReadModelAsync`, `GetGroupReadModelAsync`, `GetSchemaRuleSetAsync`, `AddSchemaRuleSetAsync`, `SaveChangesAsync`
- [ ] T054 [US1] Create `UpsertSchemaRulesCommandHandler.cs`: (1) authorize caller, (2) validate all ONE_OFF rules have `StartDate >= today`, (3) run AVAIL×AVAIL overlap check on the submitted set — return `AvailabilityConflictException` (400) on conflict, UNAVAIL×UNAVAIL overlap is accepted; (4) load or create `SchemaRuleSet`; (5) clear existing `AvailabilityRules` for this set; (6) create and add new `AvailabilityRule` entities; (7) save — all in one EF transaction

### Feature: GetResourceRules (US2)

- [ ] T055 [P] [US2] Create `GetResourceRulesQuery.cs`, `GetResourceRulesQueryResult.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Features/GetResourceRules/`
- [ ] T056 [US2] Create `IGetResourceRulesQueryDataProvider.cs` and `GetResourceRulesQueryDataProvider.cs`: fetches `ResourceReadModel`, `SchemaReadModel`, `GroupReadModel`, `ResourceRuleSet`, `SchemaRuleSet`
- [ ] T057 [US2] Create `GetResourceRulesQueryHandler.cs`: authorize; return resource-specific rules in `Rules` and schema rules in `InheritedRules`; `bufferMinutes` in response is effective value (resource override if not null, else schema default)

### Feature: UpsertResourceRules (US2)

- [ ] T058 [P] [US2] Create `UpsertResourceRulesCommand.cs`: `record UpsertResourceRulesCommand(Guid ResourceId, Guid CallerId, int? BufferMinutesOverride, IReadOnlyList<NewAvailabilityRuleDto> Rules) : ICommand` (null `BufferMinutesOverride` = inherit from schema)
- [ ] T059 [US2] Create `IUpsertResourceRulesCommandDataProvider.cs` and `UpsertResourceRulesCommandDataProvider.cs`
- [ ] T060 [US2] Create `UpsertResourceRulesCommandHandler.cs`: same validation and overlap logic as schema handler; empty `Rules` with null `BufferMinutesOverride` = "Reset to schema defaults"

### API endpoints and request DTOs

- [ ] T061 [US1] Create `RuleSetUpdateRequest.cs` and `NewRuleRequest.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Api/Requests/`
- [ ] T062 [US1] Create `AvailabilityModule.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Api/AvailabilityModule.cs` — implement all 4 endpoints: `GET /availability/schemas/{schemaId}/rules`, `PUT /availability/schemas/{schemaId}/rules`, `GET /availability/resources/{resourceId}/rules`, `PUT /availability/resources/{resourceId}/rules`; route prefix `"availability"`; all endpoints `RequireAuthorization()`; PUT endpoints return `Results.NoContent()` (204)
- [ ] T063 [US1] Add `NewAvailabilityRuleDto.cs` value record in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Features/` (shared across Upsert handlers): `record NewAvailabilityRuleDto(string RuleType, string RuleMode, int[]? DaysOfWeek, string StartTime, string EndTime, string? StartDate, string? EndDate)`

### Module registration

- [ ] T064 [US1] Create `Extensions.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Extensions.cs`: add 4 `InternalsVisibleTo` attributes; register all command handlers, query handlers, event handlers, data providers via `AddDataProviders`; register `AvailabilityDbContext`; add `AvailabilityUnitOfWork`; add exception-to-response mapper
- [ ] T065 [US1] Create `AvailabilityUnitOfWork.cs` in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/DAL/AvailabilityUnitOfWork.cs` (mirrors pattern from ManagementGroupsUnitOfWork)
- [ ] T066 [US1] Create domain exceptions in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Core/Exceptions/`: `AvailabilityConflictException.cs`, `AvailabilityForbiddenException.cs`, `AvailabilityNotFoundException.cs`, `AvailabilityDomainException.cs`, `AvailabilityExceptionToResponseMapper.cs`
- [ ] T067 [US1] Register Availability module in Bootstrapper — add `new AvailabilityModule()` to modules list; add `ThingsBooksy.Modules.Availability.IntegrationTests` schema to `SchemasToInclude` in `ThingsBooksyWebAppFactory.cs`

### Integration tests

- [ ] T068 [P] [US1] Write integration tests for schema rules in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.IntegrationTests/`: `UpsertSchemaRules_ValidRecurring_ReturnsNoContent`, `GetSchemaRules_AfterUpsert_ReturnsCorrectRules`, `UpsertSchemaRules_AvailableOverlapConflict_ReturnsBadRequest`, `UpsertSchemaRules_UnavailableOverlap_ReturnsNoContent`, `UpsertSchemaRules_EmptyRules_ClearsAllRules`, `UpsertSchemaRules_OvernightRule_AutoSetsEndDayOffset`
- [ ] T069 [P] [US2] Write integration tests for resource rules in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.IntegrationTests/`: `UpsertResourceRules_WithOverride_ReturnsNoContent`, `GetResourceRules_ReturnsInheritedAndOwnRules`, `UpsertResourceRules_NullBufferMinutes_InheritsSchemaBuffer`, `UpsertResourceRules_PastDate_ReturnsBadRequest`
- [ ] T070 [P] [US4] Write integration tests for event handler pipelines in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.IntegrationTests/`: `GroupCreated_UpsertGroupReadModel_SetsTimeZoneId`, `ResourceSchemaCreated_UpsertSchemaReadModel`, `ResourceInstanceCreated_UpsertResourceReadModel`, `HolidaysRefreshed_UpsertPublicHolidayReadModels`
- [ ] T071 [US1] Write integration tests for authorization in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.IntegrationTests/`: `UpsertSchemaRules_Unauthenticated_Returns401`, `UpsertSchemaRules_NonOwner_Returns403`

**Checkpoint**: All 4 availability endpoints work end-to-end. Integration tests pass. `dotnet build` clean. Swagger shows 4 endpoints under `Availability` tag.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Final integration validation and build verification.

- [ ] T072 [Bootstrapper] Run `dotnet format backend/ThingsBooksy.slnx` on the full solution and fix any formatting issues in `backend/src/`
- [ ] T073 [Bootstrapper] Run `dotnet build backend/ThingsBooksy.slnx` and verify zero errors/warnings across all modules in `backend/src/`
- [ ] T074 [Bootstrapper] Run `dotnet test backend/ThingsBooksy.slnx` and verify all integration tests pass in `backend/src/Modules/ManagementGroups/`, `backend/src/Modules/Resources/`, `backend/src/Modules/Calendar/`, `backend/src/Modules/Availability/`
- [ ] T075 [P] [Availability] Verify Swagger UI at `localhost:8080/swagger` shows all 4 Availability endpoints in `backend/src/Modules/Availability/ThingsBooksy.Modules.Availability.Api/AvailabilityModule.cs` correctly after `wsl docker compose up --build`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Shared.Abstractions)**: No dependencies — start immediately
- **Phase 2 (ManagementGroups + Resources)**: Depends on Phase 1 completion — BLOCKS Phases 3 and 4
- **Phase 3 (Calendar)**: Depends on Phase 1 (new events) — can start after Phase 1; independent of Phase 2
- **Phase 4 (Availability)**: Depends on Phase 1 AND Phase 2 (needs GroupCreated with TimeZoneId compiling); can start after Phase 2
- **Phase 5 (Polish)**: Depends on Phases 2, 3, 4

### User Story Dependencies

- **US4 (Group timezone — P1)**: T001, T005–T012 — Phase 1 + Phase 2 ManagementGroups
- **US1 (Schema rules — P1)**: T013–T021 (Resources), T033–T071 (Availability) — after US4 dependency chain
- **US2 (Resource rules — P1)**: T040, T047, T055–T060, T069 — after US1 Availability scaffold
- **US3 (Holiday calendar — P2)**: T004, T022–T032 (Calendar), T041, T048 (Availability handlers) — partially parallel with US1/US2

### Parallel Opportunities

- T002, T003, T004 can run simultaneously (different files in Shared.Abstractions)
- T013–T015 (Resources domain) can run parallel to T005–T008 (ManagementGroups domain)
- T034–T041 (domain entities + read-models) can all run in parallel after T033
- T045–T048 (event handlers) can run in parallel after T042
- T049, T052, T055, T058 (query/command definitions) can run in parallel
- T068–T071 (integration tests) can run in parallel after T067

---

## Implementation Strategy

### BE Wave 1 (Phase 1 + Phase 2)

1. Phase 1: Shared.Abstractions — parallel T001–T004
2. Phase 2 ManagementGroups: T005–T012
3. Phase 2 Resources: T013–T021 (parallel with ManagementGroups where indicated)
4. Verify: `dotnet build` clean + all existing tests pass

### BE Wave 2 (Phase 3 + Phase 4)

1. Calendar scaffold: T022–T032 (can run parallel with Availability)
2. Availability scaffold + domain: T033–T044
3. Availability event handlers: T045–T048
4. Availability features: T049–T063 (query/command handlers in parallel)
5. Availability registration: T064–T067
6. All integration tests: T068–T071

### FE Wave (after BE + contract-definer + swagger-emit)

1. T076 api-client regen (foundational)
2. T077, T078, T079 component implementations (parallel)
3. T080 routes (after all components)

---

## Phase 6: FE Wave — Availability UI

**Purpose**: Angular implementation of the three availability UI components. Runs after BE Wave completes, `contract-definer` finalizes `api-contract.md`, and `/swagger-emit` produces `specs/011-booking-availability/swagger.json`.

**⚠️ CRITICAL**: T076 (api-client) must complete before any component task starts.

- [ ] T076 [US1] [FR-001] Regenerate TypeScript HTTP client at `frontend/src/app/api/` from `specs/011-booking-availability/swagger.json`. Consumes: —. Depends on: —.

- [ ] T077 [US1] [FR-001] Implement `SchemaAvailabilityTabComponent` at `frontend/src/app/features/availability/components/schema-availability-tab` — displays current schema rule list + add-rule form (RECURRING and ONE_OFF), PUT on save, shows buffer minutes field, AVAIL×AVAIL conflict error handling. Consumes: GET /availability/schemas/{schemaId}/rules; PUT /availability/schemas/{schemaId}/rules. Depends on: T076.
  Requires: T051 (GetSchemaRulesQueryHandler), T054 (UpsertSchemaRulesCommandHandler).

- [ ] T078 [US2] [FR-011] Implement `ResourceAvailabilityTabComponent` at `frontend/src/app/features/availability/components/resource-availability-tab` — shows inherited schema rules (read-only, "Copied from schema" badge), resource-specific override rules, "Reset to schema defaults" button, PUT on save. Consumes: GET /availability/resources/{resourceId}/rules; PUT /availability/resources/{resourceId}/rules. Depends on: T076.
  Requires: T057 (GetResourceRulesQueryHandler), T060 (UpsertResourceRulesCommandHandler).

- [ ] T079 [US3] [FR-019] Implement `AvailabilityCalendarPreviewComponent` at `frontend/src/app/features/availability/components/availability-calendar-preview` — read-only monthly calendar; green = available, red = unavailable; amber overlay with tooltip for public holidays; "+1 day" badge for overnight rules; month navigation arrows; accepts `rules`, `inheritedRules`, `holidays` as `@Input()`. Consumes: —. Depends on: T076.

- [ ] T080 [US1] [FR-001] Create `availability.routes.ts` at `frontend/src/app/features/availability/availability.routes.ts` and register lazy-loaded feature in `frontend/src/app/app.routes.ts`. Consumes: —. Depends on: T077, T078, T079.

> **WARNING — shared state.** If multiple components in this feature share state (e.g. the rule list is shared between tab and calendar preview), add an `AvailabilityStore` task explicitly. This skill does not generate store tasks automatically.
