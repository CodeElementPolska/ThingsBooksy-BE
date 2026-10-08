**Epic:** #56 Resource availability rules

## Why

The business and the frontend call the resource template a 'resource schema', but the backend and its API call it a 'resource type'. The owner wants one name everywhere before the availability epic builds new code on top of schemas, so that every later story, test and conversation uses the same word. There are no external API clients and no real data yet, so this is the cheapest moment to do it.

## Acceptance criteria

- **AC-1** Given a signed-in user with the access they have today, when they use the schema operations at /resources/schemas (create, list by group, read, update, delete) and the instance operations that name a schema (create with resourceSchemaId, list whose rows carry resourceSchemaId, list filtered by resourceSchemaId), then every operation gives the same result as the matching operation under the old names (/resources/types, resourceTypeId) gave before this story, apart from the message wording of AC-7 and one error code: a taken schema name answers 409 with code RESOURCE_SCHEMA_NAME_TAKEN instead of RESOURCE_TYPE_NAME_TAKEN (DEC-2)
- **AC-2** Given the application after this story, when anyone calls an old /resources/types address, then the address no longer exists (404); no alias is kept
- **AC-6** Given the generated API description of the Resources module, when it is searched for the words 'ResourceType', 'resourceTypeId', 'resource type' and 'resource types', then no address, request or response model, field or operation name contains them
- **AC-7** Given the list of user-visible messages of the Resources module that mention a 'resource type', compiled by discovery and written into the spec before G2, when the same situation occurs after this story, then each listed message says 'resource schema' instead and is otherwise unchanged, and a text search of the module's user-visible messages finds no 'resource type'
- **AC-8** _(owner)_ Given the frontend after this story, when the group owner opens a group's schema list, creates a schema, opens and edits it, and creates an instance of it, then every step works as before this story, now talking to the /resources/schemas addresses; three texts on the group screen say 'schema' instead of 'type' (DEC-4): the resources table column header 'Schema', the empty-state sentence 'Create the first schema to describe your resources.' and the add-button label 'Add resource to schema <name>'
- **AC-9** Given a development database created before this story, when the new version of the application starts, then it starts without errors; development data may be removed in the process (owner decision: development database only, any data may be wiped)
- **AC-10** _(member)_ Given the responses that a group member who is not the owner receives today from each /resources/types operation and each instance operation that names a schema, recorded by discovery before the rename, when the same member makes the same calls through /resources/schemas and resourceSchemaId, then every response (status and body) is the same as recorded, apart from the message wording of AC-7; this includes a member listing resources with includeDeleted=true and receiving the deleted ones (declared intended, DEC-3)
- **AC-11** _(user from another group)_ Given the responses that a signed-in user from another group receives today from each /resources/types operation and each instance operation that names a schema, recorded by discovery before the rename, when the same user makes the same calls through /resources/schemas and resourceSchemaId, then every response (status and body) is the same as recorded, apart from the message wording of AC-7; an existing schema of another group answers 403 and a missing one answers 404 (read) or 400 (other operations) (declared intended, DEC-3)
- **AC-12** _(owner)_ Given the response a group owner receives today when the instance list filter names a schema of another group, recorded by discovery before the rename, when the owner lists instances filtered by resourceSchemaId of another group's schema, then the response is the same as recorded for both call shapes: own groupId plus the foreign schema id gives 200 with an empty list; the foreign schema id alone gives 403 (ASM-6)
- **AC-13** Given the Resources area of the database after the migrations of this story, when the names of its tables, columns, constraints, indexes and sequences are listed, then none contains 'resource_type' or 'ResourceType' (property data-kind names such as data_type / PropertyDataType are not affected)
- **AC-14** Given main with story 015 merged and a database created from main, when the migrations of this story, regenerated after the final rebase onto main, are applied and the application starts, then it starts without errors and the model reports no pending changes

## Out of scope

- Any change of schema or instance behaviour (fields, validation, access rules) other than the name — if discovery finds that today's access rules let a non-owner or another group read or change data, it is reported to the owner as a decision (D-1), not fixed or preserved silently
- Renaming 'instance' — the API already uses /resources/instances; any internal mismatch found while renaming becomes a follow-up item outside this story
- Lifecycle event names — story 015 already named them ResourceSchema*/ResourceInstance*
- Everything in the availability epic (rules, buffer, time zone, holidays, calendar)
- Group manager role
- Preserving existing development data
- Rejecting the old field name resourceTypeId: a request that still sends it (stale browser tab, cached bundle) has it silently ignored — accepted risk because backend and frontend ship together as one deployable (premortem-4)

## Rejected alternatives

- **Rename without the database (tables keep the old name)** — Owner chose the full rename: no real data exists, the old name would stay in the database forever
- **Rename only inside the backend code, keep the API and database** — The inconsistency the owner wants to remove is visible exactly in the API; the frontend already says 'schema'
- **Keep /resources/types as an alias of /resources/schemas for a transition period** — There are no external API clients; the only client (our frontend) changes in the same story; an alias would keep the old word alive
- **Migrate existing development data instead of allowing it to be removed** — Owner decision 2026-09-29: development environment only, no real data; wiping is acceptable when simpler
- **Start the epic with the group time zone story instead of the rename** — Owner decision: rename first so that every later story of the epic uses the correct names from the start
- **Limit data removal to Resources tables and keep groups, members and users intact (premortem-2)** — Owner decision 2026-09-29: only development databases exist, no shared or demo database; wiping any development data is acceptable (allowed, not required)
- **Reject requests that still send resourceTypeId with a validation error naming resourceSchemaId (premortem-4)** — Backend and frontend ship as one deployable; the guard would be permanent code that keeps the old word alive; accepted as a risk in out_of_scope
- **Split into two deliveries: API and frontend contract first, internal and database names later (scope-5)** — Owner keeps the full rename in one story; the ordering guard (AC-14, merge only after 015) removes the churn risk that motivated the split
- **Separate acceptance criteria per instance operation (original AC-3..AC-5)** — Accepted scope-2: they restated AC-1; merged into AC-1

## Blast radius

Modules: Resources; contract: true, authz: true, schema: true, UI: true

Depends on: 015-resource-time-buffer

<sub>Shaped by the fleet (session A) against capability map `01040357aff3…`; story id `016-rename-resource-schema`; critiques: runs/016-rename-resource-schema/critique/scope-critic.json, runs/016-rename-resource-schema/critique/premortem-critic.json. Tasks live in `specs/016-rename-resource-schema/tasks.md` (D-6).</sub>
