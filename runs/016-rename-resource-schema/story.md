---
title: "Rename resource type to resource schema across API, backend, database and frontend"
epic: "#56 Resource availability rules"
why: "The business and the frontend call the resource template a 'resource schema', but the backend and its API call it a 'resource type'. The owner wants one name everywhere before the availability epic builds new code on top of schemas, so that every later story, test and conversation uses the same word. There are no external API clients and no real data yet, so this is the cheapest moment to do it."
acceptance_criteria:
  - id: AC-1
    given: "a signed-in user with the access they have today"
    when: "they use the schema operations at /resources/schemas (create, list by group, read, update, delete) and the instance operations that name a schema (create with resourceSchemaId, list whose rows carry resourceSchemaId, list filtered by resourceSchemaId)"
    then: "every operation gives the same result as the matching operation under the old names (/resources/types, resourceTypeId) gave before this story, apart from the message wording of AC-7 and one error code: a taken schema name answers 409 with code RESOURCE_SCHEMA_NAME_TAKEN instead of RESOURCE_TYPE_NAME_TAKEN (DEC-2)"
  - id: AC-2
    given: "the application after this story"
    when: "anyone calls an old /resources/types address"
    then: "the address no longer exists (404); no alias is kept"
  - id: AC-6
    given: "the generated API description of the Resources module"
    when: "it is searched for the words 'ResourceType', 'resourceTypeId', 'resource type' and 'resource types'"
    then: "no address, request or response model, field or operation name contains them"
  - id: AC-7
    given: "the list of user-visible messages of the Resources module that mention a 'resource type', compiled by discovery and written into the spec before G2"
    when: "the same situation occurs after this story"
    then: "each listed message says 'resource schema' instead and is otherwise unchanged, and a text search of the module's user-visible messages finds no 'resource type'"
  - id: AC-8
    role: owner
    given: "the frontend after this story"
    when: "the group owner opens a group's schema list, creates a schema, opens and edits it, and creates an instance of it"
    then: "every step works as before this story, now talking to the /resources/schemas addresses; three texts on the group screen say 'schema' instead of 'type' (DEC-4): the resources table column header 'Schema', the empty-state sentence 'Create the first schema to describe your resources.' and the add-button label 'Add resource to schema <name>'"
  - id: AC-9
    given: "a development database created before this story"
    when: "the new version of the application starts"
    then: "it starts without errors; development data may be removed in the process (owner decision: development database only, any data may be wiped)"
  - id: AC-10
    role: member
    given: "the responses that a group member who is not the owner receives today from each /resources/types operation and each instance operation that names a schema, recorded by discovery before the rename"
    when: "the same member makes the same calls through /resources/schemas and resourceSchemaId"
    then: "every response (status and body) is the same as recorded, apart from the message wording of AC-7; this includes a member listing resources with includeDeleted=true and receiving the deleted ones (declared intended, DEC-3)"
  - id: AC-11
    role: user from another group
    given: "the responses that a signed-in user from another group receives today from each /resources/types operation and each instance operation that names a schema, recorded by discovery before the rename"
    when: "the same user makes the same calls through /resources/schemas and resourceSchemaId"
    then: "every response (status and body) is the same as recorded, apart from the message wording of AC-7; an existing schema of another group answers 403 and a missing one answers 404 (read) or 400 (other operations) (declared intended, DEC-3)"
  - id: AC-12
    role: owner
    given: "the response a group owner receives today when the instance list filter names a schema of another group, recorded by discovery before the rename"
    when: "the owner lists instances filtered by resourceSchemaId of another group's schema"
    then: "the response is the same as recorded for both call shapes: own groupId plus the foreign schema id gives 200 with an empty list; the foreign schema id alone gives 403 (ASM-6)"
  - id: AC-13
    given: "the Resources area of the database after the migrations of this story"
    when: "the names of its tables, columns, constraints, indexes and sequences are listed"
    then: "none contains 'resource_type' or 'ResourceType' (property data-kind names such as data_type / PropertyDataType are not affected)"
  - id: AC-14
    given: "main with story 015 merged and a database created from main"
    when: "the migrations of this story, regenerated after the final rebase onto main, are applied and the application starts"
    then: "it starts without errors and the model reports no pending changes"
out_of_scope:
  - "Any change of schema or instance behaviour (fields, validation, access rules) other than the name — if discovery finds that today's access rules let a non-owner or another group read or change data, it is reported to the owner as a decision (D-1), not fixed or preserved silently"
  - "Renaming 'instance' — the API already uses /resources/instances; any internal mismatch found while renaming becomes a follow-up item outside this story"
  - "Lifecycle event names — story 015 already named them ResourceSchema*/ResourceInstance*"
  - "Everything in the availability epic (rules, buffer, time zone, holidays, calendar)"
  - "Group manager role"
  - "Preserving existing development data"
  - "Rejecting the old field name resourceTypeId: a request that still sends it (stale browser tab, cached bundle) has it silently ignored — accepted risk because backend and frontend ship together as one deployable (premortem-4)"
rejected_alternatives:
  - option: "Rename without the database (tables keep the old name)"
    reason: "Owner chose the full rename: no real data exists, the old name would stay in the database forever"
  - option: "Rename only inside the backend code, keep the API and database"
    reason: "The inconsistency the owner wants to remove is visible exactly in the API; the frontend already says 'schema'"
  - option: "Keep /resources/types as an alias of /resources/schemas for a transition period"
    reason: "There are no external API clients; the only client (our frontend) changes in the same story; an alias would keep the old word alive"
  - option: "Migrate existing development data instead of allowing it to be removed"
    reason: "Owner decision 2026-09-29: development environment only, no real data; wiping is acceptable when simpler"
  - option: "Start the epic with the group time zone story instead of the rename"
    reason: "Owner decision: rename first so that every later story of the epic uses the correct names from the start"
  - option: "Limit data removal to Resources tables and keep groups, members and users intact (premortem-2)"
    reason: "Owner decision 2026-09-29: only development databases exist, no shared or demo database; wiping any development data is acceptable (allowed, not required)"
  - option: "Reject requests that still send resourceTypeId with a validation error naming resourceSchemaId (premortem-4)"
    reason: "Backend and frontend ship as one deployable; the guard would be permanent code that keeps the old word alive; accepted as a risk in out_of_scope"
  - option: "Split into two deliveries: API and frontend contract first, internal and database names later (scope-5)"
    reason: "Owner keeps the full rename in one story; the ordering guard (AC-14, merge only after 015) removes the churn risk that motivated the split"
  - option: "Separate acceptance criteria per instance operation (original AC-3..AC-5)"
    reason: "Accepted scope-2: they restated AC-1; merged into AC-1"
blast_radius:
  modules:
    - "Resources"
  touches_contract: true
  touches_authz: true
  touches_schema: true
  touches_ui: true
depends_on:
  - "015-resource-time-buffer"
capability_map_version: "01040357aff3a256ea13ee13ac2d7dc16e6e41b283ac5a2827c136803735e18d"
critique_refs:
  - "runs/016-rename-resource-schema/critique/scope-critic.json"
  - "runs/016-rename-resource-schema/critique/premortem-critic.json"
source_issue: "#57"
---

# Story 016 — Rename resource type to resource schema

## Journey

This story has no new user journey. The group owner keeps doing what they do today — list the group's schemas, create a schema,
open and edit it, create instances from it — and sees no difference. What changes is the vocabulary of the system underneath:
the addresses, field names, messages and database names now use the same word the owner and the screens use: **resource schema**.

## Actors

- **Group owner** — manages schemas and instances in the UI (fact F-4: a group has an owner and members; no manager role).
- **Group member (not owner)** and **signed-in user from another group** — their access must stay exactly as today (AC-10, AC-11).
- **Our frontend** — the only client of the API; it is adapted in the same story.

## What the application does today (facts)

- F-1: the API uses "type": `/resources/types`, `/resources/types/{id}`, `CreateResourceTypeRequest`, `UpdateResourceTypeRequest`,
  field and filter `resourceTypeId` on instances; no Resources API name contains "schema".
- F-2: the frontend routes use "schema" only: `groups/:groupId/schemas`, `schemas/new`, `schemas/:schemaId`.
- F-3: no buffer exists on schemas or instances (relevant for the epic, not for this story).
- F-4: roles in a group are owner and members only; today's access rules of the Resources endpoints are not visible in the generated artifacts (confidence: unknown).

## Visible results

- New addresses `/resources/schemas` and `/resources/schemas/{id}`, field and filter `resourceSchemaId`; old `/resources/types*` addresses disappear (AC-1, AC-2).
- The API description, the user messages and the database contain no "resource type" naming (AC-6, AC-7, AC-13).
- Access for non-owners and other groups is provably unchanged (AC-10..AC-12).
- The screens keep working (AC-8); the development database may be reset (AC-9); the story lands only after 015 (AC-14).

## Acceptance criteria ids

AC ids are stable. AC-3, AC-4 and AC-5 of the proposal were merged into AC-1 after scope-2 and are not reused.

## Critique decisions (owner, 2026-09-29, journaled)

| Item | Decision |
|---|---|
| scope-1, premortem-3 | Accepted — AC-14 and `depends_on: 015`; 016 merges only after 015 is on main |
| scope-2 | Accepted — AC-3..AC-5 merged into AC-1 |
| scope-3 | Accepted — AC-7 bound to a list compiled by discovery |
| scope-4, premortem-1 | Accepted with audit — AC-10..AC-12; `touches_authz: true`; holes in today's rules go back to the owner |
| scope-5 | No change — full rename stays (see rejected alternatives) |
| scope-6 | Accepted — the 'instance' naming note is removed; mismatches become follow-up items |
| premortem-2 | Rejected by the owner — any development data may be wiped |
| premortem-4 | Rejected by the owner — silent ignore of `resourceTypeId` accepted as a risk (out_of_scope) |
| premortem-5 | Accepted — AC-13 |

## Notes for discovery (owner wishes and context, not acceptance criteria)

- Story 015 is merged to `main` (PR #59, 2026-09-30) and this branch was rebased onto `main` before discovery, so AC-14 holds by construction; keep it as the guard for any later rebase.
- Full depth: entity, commands, queries, handlers, requests, DTOs, endpoints, operation names, EF configuration, table/column/constraint/index names,
  tests, frontend API client and services. Development data may be wiped instead of migrated (journaled owner decision).
- Before renaming, record today's responses for the non-owner member, the other-group user and the foreign-schema filter (AC-10..AC-12).
  If those rules turn out to leak data, stop and ask the owner (D-1) — do not preserve or fix silently.
- Compile the list of user-visible messages mentioning "resource type" into the spec before G2 (AC-7).
- Story 015 already introduced `ResourceSchemaCreatedEvent` / `ResourceSchemaDeletedEvent` / `ResourceInstance*Event` — keep them.
- `PropertyDataType` / `dataType` are a property's data kind, not the resource type — they are NOT renamed.

## Discovery decisions (owner, 2026-10-02, journaled)

| Decision | Chosen | Effect on this story |
|---|---|---|
| DEC-1 migration shape | Rename in place | one migration of rename operations (table, two columns, two indexes) plus re-created primary and foreign key; development data survives; AC-9 wording unchanged |
| DEC-2 error code | Rename to RESOURCE_SCHEMA_NAME_TAKEN | the single named exception to "same result" in AC-1 |
| DEC-3 access observations | Both intended, no follow-up | members may list deleted resources (includeDeleted=true); the 403-vs-404/400 difference for foreign vs missing ids is accepted; AC-10 and AC-11 record both as expected behaviour |
| DEC-4 on-screen texts | Change 3 group-screen texts | AC-8 lists them; the sign-in slogan "One platform for every resource type." stays |
| DEC-5 delivery route | Two steps in one story | step 0 = behaviour-neutral internal rename before the delivery baseline; then the standard phases carry the visible change and the migration |

The baseline responses for AC-10..AC-12 and the message list for AC-7 are in `specs/016-rename-resource-schema/spec.md`.
