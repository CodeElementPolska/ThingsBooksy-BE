# Research: Booking Availability

**Branch**: `011-booking-availability` | **Date**: 2026-05-28

## Decision 1 — Overlap validation strategy

**Decision**: Perform AVAIL×AVAIL conflict detection in the command handler by iterating the submitted rule set in memory (no DB round-trips needed since replace-whole-set sends the entire set in one PUT).

**Rationale**: Replace-whole-set semantics means the full candidate rule set is always available in the request body. An O(n²) check over ≤100 rules is trivial; no need for an interval-tree or range-index query.

**Alternatives considered**: DB-side exclusion query (rejected — complex, non-portable), interval tree (rejected — over-engineering for ≤100 rules).

---

## Decision 2 — TimeOnly / DateOnly EF Core mapping

**Decision**: Use EF Core 10 native `TimeOnly` and `DateOnly` column mappings with PostgreSQL provider (`Npgsql.EntityFrameworkCore.PostgreSQL`). These are natively supported since EF 6 / Npgsql 6.

**Rationale**: Native mapping avoids manual conversion code. `.NET 10` + `Npgsql 8+` support `time without time zone` → `TimeOnly` and `date` → `DateOnly` out of the box.

**Alternatives considered**: Store as `int` (minutes-since-midnight) — rejected as unreadable in SQL; store as `string` — rejected as fragile.

---

## Decision 3 — DaysOfWeek storage in PostgreSQL

**Decision**: Store `DaysOfWeek` as a PostgreSQL `integer[]` array column using Npgsql's array mapping. EF Core property: `int[]` mapped with `.HasColumnType("integer[]")`.

**Rationale**: Avoids a separate join table for what is a simple value-type list on a rule. Npgsql supports array columns natively; queries over arrays use PostgreSQL `&&` (overlap) operator when needed.

**Alternatives considered**: Comma-separated string (rejected — lossy, no type safety); JSON column (accepted fallback if array type causes issues, but native array is preferred); separate `AvailabilityRuleDayOfWeek` join table (rejected — over-normalisation for a value list bounded at 7 items).

---

## Decision 4 — Calendar module holiday fetching (Nager.Date)

**Decision**: Use the Nager.Date public REST API (`https://date.nager.at/api/v3/PublicHolidays/{year}/PL`) via `HttpClient` registered with `IHttpClientFactory`. No NuGet package needed — the API returns simple JSON.

**Rationale**: Nager.Date's free API is stable and publicly documented. The NuGet package `Nager.Date` (offline DB) requires frequent updates; the HTTP API is always current. The Calendar module's `IHostedService` fetches once per year per run.

**Alternatives considered**: `Nager.Date` NuGet package (offline) — rejected because it bundles static holiday data and requires updates; custom static list — rejected as unmaintainable.

---

## Decision 5 — Calendar module state tracking

**Decision**: Calendar module maintains a `PublicHolidayFetchLog` entity in its own `"calendar"` schema with columns `(Year int PK, FetchedAt DateTime, HolidayCount int)`. On `IHostedService` startup, it checks whether the current year has a log entry. If not (or if the log is absent), it fetches from Nager.Date and emits `HolidaysRefreshedEvent`. The `PeriodicTimer` repeats the check every 24 hours.

**Rationale**: This keeps Calendar fully self-contained. It doesn't query the Availability module's read-model (which would violate no-cross-module rule). The log is minimal — one row per year.

**Alternatives considered**: In-memory flag (rejected — lost on restart); querying Availability's `PublicHolidayReadModel` (rejected — cross-module DB query).

---

## Decision 6 — Authorization in Availability endpoints

**Decision**: The Availability module resolves the group owner by looking up `SchemaReadModel.GroupId` → `GroupReadModel` to get the `TimeZoneId` (and implicitly to confirm the group exists). Authorization check: the JWT-derived `callerId` must match the group owner. Since Availability has no direct access to ManagementGroups DB, it stores the `OwnerId` in `GroupReadModel` alongside `TimeZoneId`.

**Rationale**: The GroupReadModel in Availability module must include `OwnerId` (mirroring the Resources module's pattern) to enable auth checks without cross-module queries. This is the established pattern already in use in Resources.GroupReadModel.

**Alternatives considered**: IModuleClient query to ManagementGroups (rejected — spec says no IModuleClient); skip auth in Availability (rejected — FR-029/030 require it).

---

## Decision 7 — SchemaRuleSet / ResourceRuleSet as aggregate roots

**Decision**: `SchemaRuleSet` and `ResourceRuleSet` are separate aggregate root entities (not value objects) with their own PKs. `AvailabilityRule` is a child entity belonging to exactly one rule set. Replace-whole-set PUT deletes all existing child rules and inserts the new ones within a single EF transaction.

**Rationale**: Using a rule set as the aggregate boundary makes the replace-whole-set semantics natural: load the rule set, clear its children collection, add new children, save. EF change tracking handles the DELETEs automatically with cascade configured in `OnDelete(DeleteBehavior.Cascade)`.

**Alternatives considered**: Treating `AvailabilityRule` as independent root (rejected — requires manual cascade); storing rules as JSON column (rejected — rules need to be individually queryable for potential future slot computation).

---

## Decision 8 — GroupReadModel in Availability module includes OwnerId

**Decision**: `GroupReadModel` in the Availability module stores `(Id=GroupId, OwnerId, TimeZoneId)`. This mirrors the existing `GroupReadModel` in the Resources module which already stores `(Id, OwnerId)`.

**Rationale**: Needed for authorization checks on availability PUT endpoints (see Decision 6). The GroupCreated event already carries OwnerId so no new event fields are required beyond the planned TimeZoneId addition.

---

## Decision 9 — IANA timezone validation

**Decision**: Validate `TimeZoneId` using `TimeZoneInfo.TryFindSystemTimeZoneById(id, out _)` on .NET 10 running on Linux (Docker). On Linux, .NET uses the system's IANA timezone database via `tzdata`. The Docker image already has `tzdata` installed (confirmed by standard ASP.NET 10 Docker images).

**Rationale**: No NodaTime dependency needed. .NET 6+ on Linux natively maps IANA timezone IDs via `TimeZoneInfo`. `TryFindSystemTimeZoneById` returns false for invalid IDs.

**Alternatives considered**: NodaTime (rejected — unnecessary dependency for this feature); custom IANA list (rejected — maintenance burden).

---

## Decision 10 — ResourceType terminology mapping

**Decision**: In the codebase, what the spec calls "resource schema" is implemented as `ResourceType` (the existing domain entity in the Resources module). The spec uses "schema" for user-facing language; the code uses "ResourceType". New events are named to follow spec language: `ResourceSchemaCreatedEvent` (despite the DB entity being ResourceType). This is intentional — event names represent the ubiquitous language, not the internal class name.

**Rationale**: Consistent with the existing pattern (e.g., ManagementGroup in code = "Group" in spec language). Events are contracts — they should use the business vocabulary.
