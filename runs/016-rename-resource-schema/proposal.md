---
title: "Rename resource type to resource schema across API, backend, database and frontend"
epic: "Resource availability rules (new epic, GitHub number assigned at G1; see runs/016-rename-resource-schema/epic.md)"
why: "The business and the frontend call the resource template a 'resource schema', but the backend and its API call it a 'resource type'. The owner wants one name everywhere before the availability epic builds new code on top of schemas, so that every later story, test and conversation uses the same word. There are no external API clients and no real data yet, so this is the cheapest moment to do it."
acceptance_criteria:
  - id: AC-1
    role: owner
    given: "a signed-in group owner"
    when: "they create, list (by group), read, update and delete resource schemas through the /resources/schemas addresses"
    then: "each operation behaves exactly as the matching /resources/types operation behaved before this story (same results, same validation, same access rules)"
  - id: AC-2
    given: "the application after this story"
    when: "anyone calls an old /resources/types address"
    then: "the address no longer exists (404); no alias is kept"
  - id: AC-3
    role: owner
    given: "a signed-in group owner and an existing resource schema"
    when: "they create a resource instance and name its schema in the field resourceSchemaId"
    then: "the instance is created from that schema exactly as before this story"
  - id: AC-4
    role: owner
    given: "a group with resource instances"
    when: "the owner lists instances"
    then: "every row names its schema in the field resourceSchemaId"
  - id: AC-5
    role: owner
    given: "a group with instances of two different schemas"
    when: "the owner lists instances filtered by resourceSchemaId"
    then: "only the instances of that schema are returned"
  - id: AC-6
    given: "the generated API description of the Resources module"
    when: "it is searched for the words 'ResourceType', 'resourceTypeId', 'resource type' and 'resource types'"
    then: "no address, request or response model, field or operation name contains them"
  - id: AC-7
    given: "every message returned to the user by the Resources module that mentions a 'resource type'"
    when: "the same situation occurs after this story"
    then: "the message says 'resource schema' instead and is otherwise unchanged"
  - id: AC-8
    role: owner
    given: "the frontend after this story"
    when: "the owner opens a group's schema list, creates a schema, opens and edits it, and creates an instance of it"
    then: "every step works as before this story, now talking to the /resources/schemas addresses"
  - id: AC-9
    given: "a development database created before this story"
    when: "the new version of the application starts"
    then: "it starts without errors; existing resource schemas and instances may be removed in the process (owner decision: development data only)"
out_of_scope:
  - "Any change of schema or instance behaviour (fields, validation, access rules) other than the name"
  - "Renaming 'instance' — the API already uses /resources/instances"
  - "Lifecycle event names — story 015 already named them ResourceSchema*/ResourceInstance*"
  - "Everything in the availability epic (rules, buffer, time zone, holidays, calendar)"
  - "Group manager role"
  - "Preserving existing development data in Resources"
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
blast_radius:
  modules:
    - "Resources"
  touches_contract: true
  touches_authz: false
  touches_schema: true
  touches_ui: true
depends_on:
  - "015-resource-time-buffer"
capability_map_version: "461ba2aa78c0c9a87c1770a052ad83697165a8f78b8421d2cab7675ddfc9d6d4"
critique_refs:
  - "runs/016-rename-resource-schema/critique/scope-critic.json"
  - "runs/016-rename-resource-schema/critique/premortem-critic.json"
---

# Proposal 016 — Rename resource type to resource schema

## Journey

This story has no new user journey. The group owner keeps doing what they do today — list the group's schemas, create a schema,
open and edit it, create instances from it — and sees no difference. What changes is the vocabulary of the system underneath:
the addresses, field names and messages of the backend now use the same word the owner and the screens use: **resource schema**.

## Actors

- **Group owner** — the only role that manages schemas and instances today (fact F-4: a group has an owner and members; no manager role).
- **Our frontend** — the only client of the API; it is adapted in the same story.

## What the application does today (facts)

- F-1: the API uses "type": `/resources/types`, `/resources/types/{id}`, `CreateResourceTypeRequest`, `UpdateResourceTypeRequest`,
  field and filter `resourceTypeId` on instances; no Resources API name contains "schema".
- F-2: the frontend routes use "schema" only: `groups/:groupId/schemas`, `schemas/new`, `schemas/:schemaId`.
- F-3: no buffer exists on schemas or instances (relevant for the epic, not for this story).
- F-4: roles in a group are owner and members only.

## Visible results

- New addresses `/resources/schemas` and `/resources/schemas/{id}`; old `/resources/types*` addresses disappear (AC-1, AC-2).
- Field and filter `resourceSchemaId` on instances (AC-3..AC-5).
- The API description contains no "resource type" naming (AC-6); user messages say "resource schema" (AC-7).
- The screens keep working (AC-8); the development database may be reset (AC-9).

## Rejected alternatives

See front matter: partial renames (without DB, code only), a transition alias, data migration, and starting the epic with the time zone story —
each with the owner's reason.

## Notes for discovery (owner wishes and context, not acceptance criteria)

- Base branch is `015-resource-time-buffer`, which is not merged to `main` (28 commits ahead); expect a rebase when 015 changes.
- Full depth: entity, commands, queries, handlers, requests, DTOs, endpoints, operation names, EF configuration, table/column names, tests,
  frontend API client and services. Development data may be wiped instead of migrated (journaled owner decision).
- Story 015 already introduced `ResourceSchemaCreatedEvent` / `ResourceSchemaDeletedEvent` / `ResourceInstance*Event` — keep them.
- `PropertyDataType` / `dataType` are a property's data kind, not the resource type — they are NOT renamed.
- Owner's term for the concrete resource is **resource instance**; check internal naming for consistency and report any mismatch to the owner rather than renaming silently.
