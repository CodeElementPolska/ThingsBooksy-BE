# Epic: Resource availability rules

> Shaped in session A of story 016 (2026-09-29). GitHub Epic issue: #56.
> Owner's original notes were written in Polish; this is the agreed English version including all owner decisions from the session.
> Owner instruction: existing GitHub epics/stories/tasks on this topic are an old draft and MUST NOT be read or reused.

## Why (owner's words, translated)

The goal is to make resources bookable. Group owners decide when each resource is available and when it is not
(some resources need time between uses, e.g. for cleaning — the buffer). This is the foundation for the next epic:
reserving resources and showing members which dates are free and which are blocked.

## Actors and access (owner decision, journaled 2026-09-29)

| Action | Who |
|---|---|
| Edit availability rules (schema and instance) | Group owner only (owner decision 2026-09-29: no manager role exists today — fact F-4; managers come with a separate group-roles story outside this epic) |
| Read availability rules / calendar | Members of the group only |
| Ordinary group member | Read only, never edits |
| People outside the group | No access (future: "public" flag, e.g. a barber's customers — out of scope) |

## Terminology (owner decision)

- **Resource schema** — the template (today called `ResourceType` in the backend; to be renamed).
- **Resource instance** — a concrete resource created from a schema.

## Business rules agreed so far

1. **Rule kinds.** Recurring (days of week + time window) and one-off (date range + time window); each rule is either AVAILABLE or UNAVAILABLE.
2. **Overnight rules.** A window whose end time is earlier than its start ends on the next day and is marked "+1 day".
3. **Default when no rule applies: UNAVAILABLE** (owner decision, journaled). Nothing is bookable until an admin configures it.
4. **Past one-off rules are not rejected.** They are shown as "Expired" and the admin may delete them (prevents a stale rule from blocking every later save).
5. **Schema → instance: copy, not live inheritance.** When an instance is created, the schema's rules (and buffer) are copied to it.
   The instance's list is then independent: it can be edited, extended, deleted. Later changes to the schema do NOT change existing instances.
   Copied rules carry an "Inherited from schema" marker which is informational only; it disappears when that rule is edited or deleted.
   "Reset to schema defaults" deletes the instance's rules and copies the CURRENT schema rules again (confirmation dialog first).
6. **Buffer.** Story 015 did NOT deliver a buffer (fact F-3); by DEC-3 of story 015, confirmed by the owner 2026-09-29, the buffer is an availability rule of the schema owned by the Availability module (story 2) and copied to instances (story 3). Resources never knows it.
7. **Overlapping rules.** Policy NOT decided — to be decided in the rules story after critics and a UX review.
   Options: reject overlaps / accept both (UNAVAILABLE wins) / newest rule wins. Constraint from the owner: editing must stay easy for group owners; the options must be explained in plain, user-facing language.
8. **Group time zone.** Required IANA time zone when a group is created; rule times are wall-clock times in the group's time zone; "All times in {TimeZoneId}" shown on availability screens. Changing the zone later is out of scope.
9. **Polish public holidays.** Fetched from the public Nager.Date API (country PL) at every application start and on 1 January, always for the current and the next year.
   A failed fetch is logged as a critical error; the calendar keeps working without holidays.
   **A holiday is UNAVAILABLE by default unless an explicit rule makes that day available** (owner decision, journaled). Holidays are shown in their own colour (e.g. pink) with the holiday name.
10. **Availability calculation + calendar.** Computing day/hour availability for the calendar IS in this epic (green = available, red = unavailable, pink = holiday).
    Slot calculation for bookings is NOT.

## Stories (order agreed with the owner)

| # | Story | Depends on |
|---|---|---|
| 0 | **016** Rename `ResourceType` to `ResourceSchema` — full depth: API, code, database, frontend adapted (owner decision) | 015 |
| 1 | Group time zone (required at group creation) | — |
| 2 | Availability rules for a resource schema + buffer | 0, 1 |
| 3 | Instance rules: copy on creation, edit, "Inherited from schema" marker, reset | 2 |
| 4 | Polish public holidays (Calendar module) | — |
| 5 | Day/hour availability calculation and calendar view with holidays | 3, 4 |

Base branch: `main` (story 015, which delivered the Resources lifecycle events the epic builds on, was merged to `main` on 2026-09-30 via PR #59).

## Out of scope for the whole epic

- Slot calculation for bookings; creating bookings; the booking flow.
- Purple "reserved" colour in the calendar — belongs to the booking epic (there are no bookings yet).
- Holidays of countries other than Poland.
- Conflict-resolution window "holiday × recurring rule" (like a merge conflict, the manager decides each colliding day) — recorded as an important future idea, deliberately not solved now.
- Changing a group's time zone after creation.
- Audit history of rule changes.
- Notifications / webhooks after availability changes.
- Group manager role and its permissions (separate story; until then only the owner edits).
- Access for people outside the group (future "public" flag for services open to everyone).
- Editing a single rule in place through a dedicated API operation (the whole set is replaced on save) — revisit in story 2 if the overlap policy needs it.

## Data (owner decision, journaled 2026-09-29)

Only a development environment exists, no real data. Existing groups/schemas/instances may be wiped instead of migrated when that is simpler.

## Notes for discovery (owner's technical wishes — not acceptance criteria)

- Communication between modules through events only (message broker), no `IModuleClient` queries.
- New modules: `Availability` (rules, read models fed by events) and `Calendar` (holidays).
- Already delivered by story 015: `ResourceSchemaCreatedEvent(SchemaId, GroupId)`, `ResourceInstanceCreatedEvent(ResourceId, SchemaId)`, `ResourceSchemaDeletedEvent`, `ResourceInstanceDeletedEvent` (no BufferMinutes — DEC-3). Owner's original wish was `ResourceSchemaCreatedEvent(SchemaId, GroupId, BufferMinutes)` and `ResourceInstanceCreatedEvent(ResourceId, SchemaId)`; `GroupCreated` gets `TimeZoneId` (breaking — update all subscribers).
- Availability read models: Group(GroupId, TimeZoneId), Schema(SchemaId, GroupId, BufferMinutes), Resource(ResourceId, SchemaId), PublicHoliday(Date, Name, Year).
- Calendar emits `HolidaysRefreshedEvent(int Year, IReadOnlyList<PublicHolidayDto>)`, `PublicHolidayDto(DateOnly Date, string LocalName, string Name)`.
- Endpoints (Bearer): GET/PUT `/availability/schemas/{schemaId}/rules`, GET/PUT `/availability/resources/{resourceId}/rules` (PUT → 204, replaces the whole set; empty list clears it; max 100 rules).
- DTO: DaysOfWeek as 0–6 (0 = Sunday), times as HH:mm; PostgreSQL `integer[]` for DaysOfWeek; rule times stored as TimeOnly/DateOnly, no UTC conversion.
- Time zones via `TimeZoneInfo.FindSystemTimeZoneById` with IANA ids; no NodaTime.
- Deleting a schema hard-deletes its rules; deleting a group cleans Availability read models through events.
- Frontend: SchemaAvailabilityTabComponent (tab "Availability" in Schema Designer, empty state "No rules defined"), ResourceAvailabilityTabComponent, shared AvailabilityCalendarPreviewComponent (month view, arrows), searchable IANA time-zone picker in the create-group modal.
