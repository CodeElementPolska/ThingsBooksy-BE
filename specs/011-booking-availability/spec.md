# Feature Specification: Booking Availability

**Feature Branch**: `011-booking-availability`
**Created**: 2026-05-28
**Status**: Draft
**Scope**: both

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Configure availability rules for a resource schema (Priority: P1)

A group owner opens the Schema Designer, clicks the **Availability** tab, and sees an empty rule list with an "Add rule" form. They define working hours for their organisation: Monday–Friday, 08:00–17:00, AVAILABLE. They save and the rule is immediately persisted — all resources of this schema now inherit these hours as their default availability. A second rule, Saturday 09:00–14:00 AVAILABLE, is added without conflict; the form resets for the next entry.

**Why this priority**: This is the foundation of the whole feature. Without schema-level rules there is nothing to inherit at the resource level and no availability data to reason about.

**Independent Test**: Open a group with at least one schema → navigate to Availability tab → add a RECURRING rule (Mon–Fri, 08:00–17:00, AVAILABLE) → save → assert the rule appears in the list and the schema rule count shown in any badge matches.

**Acceptance Scenarios**:

1. **Given** a schema has no availability rules, **When** an admin opens the Availability tab, **Then** an empty state with "No rules defined" and an "Add rule" button is shown; the calendar preview shows all days as unavailable.
2. **Given** the admin fills in a RECURRING rule (Mon–Fri, 08:00–17:00, AVAILABLE) and clicks Save, **When** the request succeeds, **Then** the rule appears in the list with a "RECURRING · Available" badge and the calendar preview marks weekdays in the selected hours as available (green).
3. **Given** the admin adds an AVAILABLE RECURRING rule (Mon–Fri 09:00–17:00) and then adds a second AVAILABLE RECURRING rule that fully overlaps (Mon 10:00–12:00 AVAILABLE), **When** they attempt to save the second rule, **Then** an error "Conflict: overlapping available rules" is shown and the rule is not saved.
4. **Given** an existing AVAILABLE rule and a new UNAVAILABLE rule covering a subset of the same time window, **When** the admin saves the UNAVAILABLE rule, **Then** it is accepted silently (unavailability wins) and the calendar preview shows the UNAVAILABLE segment in red.
5. **Given** the admin sets EndTime (e.g. 02:00) earlier than StartTime (e.g. 22:00), **When** they move focus away from the EndTime field, **Then** the form automatically sets EndDayOffset = 1 and shows a badge "+1 day" next to EndTime.

---

### User Story 2 — Override availability rules for an individual resource (Priority: P1)

A group owner opens a specific resource (e.g. "Room A") and clicks the **Availability** tab. They see the schema's inherited rules in a read-only "Inherited from schema" section with a "Copied from schema" badge. They add a resource-specific UNAVAILABLE rule (the room is under renovation Mon 2026-06-10, all day) — a ONE_OFF rule. The resource now shows two sections: inherited rules and its own override rules. The calendar preview merges both layers, showing the renovation day in red.

**Why this priority**: Closes the primary real-world use-case — a specific room or vehicle that differs from the schema defaults. Without this, availability is uniformly schema-level only, which is not enough for most businesses.

**Independent Test**: Open a resource that has schema rules → go to Availability tab → add a ONE_OFF UNAVAILABLE rule for a future date → assert the rule appears under "Resource rules" and the calendar preview shows that day as unavailable even though schema rules would otherwise allow it.

**Acceptance Scenarios**:

1. **Given** a resource whose schema has a RECURRING AVAILABLE rule (Mon–Fri 09:00–17:00), **When** the admin opens the resource's Availability tab, **Then** the inherited schema rules are shown read-only with a "Copied from schema" badge; an "Add rule" form for resource-specific rules is also present.
2. **Given** the admin adds a ONE_OFF UNAVAILABLE rule for a date range in the future and saves, **When** the request succeeds, **Then** the rule appears under "Resource rules" and the calendar preview overrides the schema availability for those days.
3. **Given** the resource has its own override rules, **When** the admin clicks "Reset to schema defaults", **Then** a confirmation dialog asks "This will remove all resource-specific rules. Continue?" and on confirmation, all resource rules are deleted and only inherited schema rules remain.
4. **Given** the admin tries to add a ONE_OFF rule with a date in the past (before today), **When** they submit, **Then** an error "Start date must not be in the past" is shown.
5. **Given** null BufferMinutes (inherit from schema), **When** the admin overrides the buffer to a specific value, **Then** the resource uses its own buffer and the schema default is ignored.

---

### User Story 3 — Calendar preview with public holidays (Priority: P2)

While configuring rules, the admin navigates the monthly calendar preview. Days that are Polish public holidays are highlighted with an amber overlay and a tooltip showing the holiday name (e.g. "Constitution Day"). This allows the admin to visually validate that their UNAVAILABLE ONE_OFF rules align with planned closures.

**Why this priority**: Public holidays are an important real-world trigger for unavailability. The calendar visual is informational only — it does not auto-create rules — but it significantly improves the quality of configuration decisions.

**Independent Test**: Navigate the calendar preview to a month containing a known Polish public holiday → assert the day has an amber background and a tooltip with the holiday name when hovered.

**Acceptance Scenarios**:

1. **Given** the calendar preview is open and today's month contains a Polish public holiday, **When** the calendar renders, **Then** the holiday day shows an amber overlay and hovering it reveals a tooltip with the holiday name.
2. **Given** the admin navigates to the next month using the arrow controls, **When** the navigation completes, **Then** the calendar re-renders with that month's holidays highlighted correctly.
3. **Given** a day has both an UNAVAILABLE rule and a public holiday, **When** the calendar renders that day, **Then** it shows the unavailable style (red) with the holiday amber overlay combined; the tooltip mentions both.
4. **Given** the admin views the calendar for a month with no public holidays, **When** the calendar renders, **Then** no amber overlays appear and no holiday tooltips are present.

---

### User Story 4 — Group timezone context (Priority: P1)

When a group owner creates a new group they are prompted for a timezone. All availability rules for schemas and resources belonging to that group are interpreted in that timezone. The Availability tabs always display times in wall-clock format with the group timezone shown as a contextual label (e.g. "All times in Europe/Warsaw").

**Why this priority**: Without timezone context, availability times are ambiguous. This is a prerequisite for the whole feature being correct — a group operating in London and another in Warsaw must be able to define working hours independently.

**Independent Test**: Create a group with TimeZoneId "Europe/Warsaw" → add a schema with a RECURRING AVAILABLE rule 09:00–17:00 Mon–Fri → verify the rule is stored with wall-clock times and the Availability tab shows "All times in Europe/Warsaw".

**Acceptance Scenarios**:

1. **Given** the Create Group modal is open, **When** the admin fills in the group name, **Then** a timezone picker (searchable dropdown of IANA timezones) is required and cannot be left empty before submitting.
2. **Given** a group has TimeZoneId "Europe/London", **When** an admin views its schema's Availability tab, **Then** a contextual label reads "All times in Europe/London" above the rule list.
3. **Given** a group was created with TimeZoneId "Europe/Warsaw", **When** availability rules are saved, **Then** the stored times are wall-clock values (e.g. 09:00) referencing that timezone; no UTC conversion happens at save time.

---

### Edge Cases

- **Overnight rule**: EndTime < StartTime → EndDayOffset auto-set to 1; "+1 day" badge shown; calendar correctly marks the rule as spanning into the next day.
- **Empty rule set PUT**: sending an empty rules array via PUT is valid and clears all rules — treated as "no availability defined" (all slots unavailable by default).
- **Resource with no schema rules**: the "Inherited from schema" section shows empty state; the resource operates with only its own rules (which may also be empty).
- **Service buffer zero**: BufferMinutes = 0 means no buffer; this is the schema default and is stored explicitly.
- **Very large rule sets**: no explicit cap is enforced per rule set; the replace-whole-set PUT handles any count correctly (server-side max cap: 100 rules per rule set to prevent abuse).
- **Duplicate UNAVAIL rules**: two overlapping UNAVAILABLE rules are accepted with a 200 response; no 400 is raised (unavailability-wins model).
- **Past ONE_OFF rule**: start date in the past is rejected with 400 at the API boundary.
- **Delete schema with availability rules**: on schema deletion, all availability rules for that schema's SchemaRuleSet are also hard-deleted.
- **Group deletion cascade**: deleting a group removes all associated GroupReadModel, SchemaReadModel, ResourceReadModel entries in the Availability module via events.

## Requirements *(mandatory)*

### Functional Requirements

**Rule management — schema level**

- **FR-001**: The system MUST allow an authorised admin to define a set of availability rules for a resource schema using a replace-whole-set operation: a single PUT replaces all existing rules for that schema.
- **FR-002**: The system MUST support two rule types: RECURRING (repeating weekly, defined by days-of-week + time window) and ONE_OFF (non-repeating, defined by a date range + time window).
- **FR-003**: Each rule MUST have a RuleMode of AVAILABLE or UNAVAILABLE.
- **FR-004**: RECURRING rules MUST specify one or more days of the week, a start time (wall-clock), an end time (wall-clock), and an end-day offset (0 = same day, 1 = next calendar day — auto-computed when end time is earlier than start time).
- **FR-005**: ONE_OFF rules MUST specify a start date, an end date, a start time, an end time, and an end-day offset.
- **FR-006**: The system MUST reject ONE_OFF rules whose start date is in the past (relative to today's date at save time) with an appropriate validation error.
- **FR-007**: The system MUST reject a PUT request when two AVAILABLE rules in the submitted set overlap in time (AVAIL × AVAIL conflict). UNAVAILABLE × UNAVAILABLE overlap MUST be accepted with a 200 response.
- **FR-008**: When an AVAILABLE and an UNAVAILABLE rule overlap, the system MUST accept the set silently (UNAVAIL wins; this is the intended merge behaviour).
- **FR-009**: Each schema's rule set MUST carry a `bufferMinutes` (integer, default 0) representing the service buffer — a gap of empty time added before or after each booking slot.
- **FR-010**: The system MUST allow retrieval of the current rule set for a schema via a GET endpoint.

**Rule management — resource level**

- **FR-011**: The system MUST allow an authorised admin to define resource-specific rules using the same replace-whole-set semantics as schema rules.
- **FR-012**: Resource-specific rules MUST follow the same rule types, validation, and overlap logic as schema rules.
- **FR-013**: Each resource's rule set MAY carry a nullable `bufferMinutes` override; if null the resource inherits the schema's buffer value.
- **FR-014**: The GET endpoint for a resource's rules MUST return two rule collections: `rules` (resource-specific) and `inheritedRules` (from the schema), so the client can display both layers.
- **FR-015**: The "Reset to schema defaults" operation MUST be equivalent to a PUT with an empty rules array and null bufferMinutes, clearing all resource-level overrides.

**Cross-module data (read-models)**

- **FR-016**: The Availability module MUST maintain a GroupReadModel (GroupId, TimeZoneId) sourced from the `GroupCreated` event.
- **FR-017**: The Availability module MUST maintain a SchemaReadModel (SchemaId, GroupId, BufferMinutes) sourced from the `ResourceSchemaCreatedEvent`.
- **FR-018**: The Availability module MUST maintain a ResourceReadModel (ResourceId, SchemaId) sourced from the `ResourceInstanceCreatedEvent`.
- **FR-019**: The Availability module MUST maintain a PublicHolidayReadModel (Date, Name, Year) sourced from the `HolidaysRefreshedEvent` emitted by the Calendar module.
- **FR-020**: Communication between modules MUST use events via the message broker exclusively — no IModuleClient queries between modules.

**Calendar / public holidays**

- **FR-021**: The system MUST automatically refresh Polish public holidays at least once per calendar year; on first startup or when the data is stale (no data for the current year), a refresh is triggered immediately.
- **FR-022**: Public holidays MUST be sourced from the Nager.Date public API.

**GroupCreated extension (BREAKING)**

- **FR-023**: The `GroupCreated` event MUST include `TimeZoneId` (string, IANA format). All existing subscribers MUST be updated to handle the new field.
- **FR-024**: The group creation endpoint and its request model MUST require a valid, non-empty `TimeZoneId` in IANA format. Invalid or empty timezone identifiers MUST be rejected with a 400 error.

**New shared events**

- **FR-025**: The Resources module MUST emit `ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId, int BufferMinutes)` when a new resource schema is created.
- **FR-026**: The Resources module MUST emit `ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId)` when a new resource instance is created.

**Time and timezone**

- **FR-027**: All availability rule times MUST be stored as wall-clock values (TimeOnly) without timezone conversion. Timezone interpretation occurs only when computing slot availability.
- **FR-028**: The timezone used for interpretation is the `TimeZoneId` on the group that owns the resource/schema, not the caller's local time.

**Authorization**

- **FR-029**: All availability endpoints MUST require an authenticated caller. Unauthenticated requests MUST receive 401.
- **FR-030**: Mutating availability endpoints (PUT) MUST authorise the caller as the group owner or an admin; non-authorised attempts MUST receive 403. Validation happens via the SchemaReadModel / ResourceReadModel to determine group ownership.

### Key Entities

- **SchemaRuleSet**: A replace-whole-set container owned by a resource schema. Carries `bufferMinutes` (int, default 0) and a list of `AvailabilityRule` entries.
- **ResourceRuleSet**: A replace-whole-set container owned by a resource instance. Carries nullable `bufferMinutes` (null = inherit schema value) and a list of `AvailabilityRule` entries.
- **AvailabilityRule**: One rule within a rule set. Has RuleType (RECURRING | ONE_OFF), RuleMode (AVAILABLE | UNAVAILABLE), time window fields (StartTime, EndTime, EndDayOffset), and for ONE_OFF: StartDate, EndDate. Also holds DaysOfWeek[] for RECURRING.
- **GroupReadModel** *(Availability module)*: Stores GroupId and TimeZoneId, populated from GroupCreated events.
- **SchemaReadModel** *(Availability module)*: Stores SchemaId, GroupId, and default BufferMinutes, populated from ResourceSchemaCreatedEvent.
- **ResourceReadModel** *(Availability module)*: Stores ResourceId and SchemaId, populated from ResourceInstanceCreatedEvent.
- **PublicHolidayReadModel** *(Availability module)*: Stores Date, Name, and Year for Polish public holidays, populated from HolidaysRefreshedEvent.

## Frontend Surface

**FE Surface:**
1. **SchemaAvailabilityTabComponent** — embedded tab in the existing schema detail/designer view; displays current rules list + "Add rule" form for RECURRING and ONE_OFF rules; PUT on save; calendar preview component embedded below the list.
2. **ResourceAvailabilityTabComponent** — embedded tab in the existing resource detail view; shows inherited schema rules read-only (badge "Copied from schema") and resource-specific override rules; "Reset to schema defaults" button; PUT on save; same calendar preview embedded.
3. **AvailabilityCalendarPreviewComponent** — read-only monthly calendar component reused in both tabs; displays computed availability with colour coding (green = available, red = unavailable, amber overlay for public holidays); navigation arrows for month browsing; overnight badge "+1 day"; tooltips on holiday days.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can complete the full schema availability configuration (open tab, add 3 RECURRING rules, save) in **under 60 seconds** with no prior training.
- **SC-002**: A PUT rules request — including overlap validation — completes in **under 500 ms** for rule sets of up to 50 rules under normal load.
- **SC-003**: The calendar preview renders the current month's availability in **under 300 ms** after the rules data is loaded, with no visible layout shift.
- **SC-004**: Public holiday data is always present for the current calendar year within **24 hours** of the year changing, without manual intervention.
- **SC-005**: **100%** of attempts to save an AVAIL × AVAIL overlapping rule set are rejected with a clear error message visible in the UI, with no silent failures.
- **SC-006**: Resource override rules are displayed in a distinct section from inherited schema rules, achieving **zero user confusion** between the two layers (validated by user testing — no participant clicks "Reset to schema defaults" by mistake).
- **SC-007**: Overnight rules (EndTime < StartTime) automatically trigger the EndDayOffset badge in **100%** of cases — the admin never has to set it manually.
- **SC-008**: The timezone label "All times in [TZ]" appears on every availability tab view, eliminating ambiguity about which clock the times refer to.

## Assumptions

- All times are wall-clock; no UTC storage of availability times. UTC conversion happens only at booking-slot computation time, which is out of scope for this spec.
- The system targets Polish public holidays only (PL country code with Nager.Date). Multi-country holiday support is out of scope.
- Slot computation (pre-calculating open/closed time windows) is out of scope for this spec; this feature only defines and stores the rules.
- The existing Schema Designer and Resource detail views already have a tabbed navigation pattern; adding an "Availability" tab follows the same pattern.
- The Nager.Date API is publicly accessible from the backend at runtime. No API key is required.
- A maximum of 100 rules per rule set is enforced server-side to prevent abuse; the UI does not need a hard cap but should surface the error gracefully.
- Editing individual rules (PATCH by rule ID) is out of scope; the replace-whole-set PUT is the only mutation operation.
- The Calendar module is a separate module to preserve separation of concerns (different lifecycle, future consumers: Billing, Reporting) — its sole purpose is to fetch and emit holiday data.
- `TimeZoneInfo.FindSystemTimeZoneById` with IANA IDs is supported on the deployment platform (.NET 10 on Linux/Docker); no NodaTime dependency is required.
- Changing a group's timezone after creation is out of scope for this iteration.
- Resource deletion and schema deletion cascade cleanup for Availability read-models is handled via events emitted by Resources/ManagementGroups modules (existing `ResourceDeleted` / `GroupDeleted` events or new analogues — to be confirmed during planning).

## Out of Scope

- Slot computation / pre-calculating availability windows for booking.
- Booking creation, reservation, or any booking flow.
- Multi-country public holidays (only Poland in this iteration).
- Editing an individual rule in place (replace-whole-set only).
- Changing a group's timezone after creation.
- Audit history of rule changes.
- Per-resource availability calendar accessible to end-users or customers (admin-only in this spec).
- Notifications or webhooks triggered by availability changes.
- Role-based access beyond "group owner as admin"; member-level access to availability rules is out of scope.
