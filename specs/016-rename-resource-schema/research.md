# Research: Rename resource type to resource schema

Facts come from `runs/016-rename-resource-schema/discovery/facts.jsonl` (F-0..F-7); owner decisions from `decisions.jsonl` (DEC-1..DEC-5). No NEEDS CLARIFICATION remains.

## R1 - Delivery route for a rename (DEC-5)

- **Decision**: two steps in one story. Step 0: behaviour-neutral internal C# rename (production + tests) before the delivery baseline, database names pinned, nothing client-visible changes. Then the standard phases carry the visible change.
- **Rationale**: the pipeline (skeleton -> migration -> blind tests -> behaviour) assumes new behaviour. Renaming the entity breaks every handler (forbidden in the skeleton phase - `tools/fleet/skeleton-check.js:30-49`); blind tests seed through entity and DbSet names; the writer may not edit tests and the tester may not edit production code (`tools/fleet/fleet-acl.json`). After Step 0 the visible change is small and fits the phases unchanged.
- **Alternatives considered**: split into two stories (owner keeps one story); one pass with the skeleton check and red-first switched off (two fleet guarantees lost).
- **How Step 0 stays invisible**: `ToTable("resource_types")` is already explicit (F-4); the two renamed FK properties get `HasColumnName("ResourceTypeId")`, so table, column, PK, FK and index names (EF defaults derived from table and column names) do not move. JSON names follow C# property names of request / result records, so those records keep their property names in Step 0, as does the endpoint parameter `resourceTypeId` (it is the query-string name). Route strings, `WithName`, messages and the 409 code are literals and stay.
- **Risk**: EF Core compares the model with the snapshot at `MigrateAsync`. Step 0 changes only CLR names (entity type and property names) with identical table and column names, which yields no migration operation, so no pending-model-changes error is expected. If the Step 0 test run nevertheless fails on a pending model change, Step 0 stops and the conductor asks the owner (the constitution allows one migration per module per story, so a snapshot-only migration is not an option).

## R2 - Migration shape (DEC-1)

- **Decision**: one migration `RenameResourceTypeToResourceSchema` made only of rename operations: `RenameTable resource_types -> resource_schemas`; `RenameColumn ResourceTypeId -> ResourceSchemaId` on `resource_instances` and `resource_property_definitions`; `RenameIndex` for `IX_resource_types_GroupId_Name` and `IX_resource_property_definitions_ResourceTypeId`; primary key and foreign key re-created under the new names (drop + add of the constraint only, never of a table or column). `Down` mirrors it.
- **Rationale**: works on any existing development database, no data loss, no manual step (AC-9); first rename precedent in the repository (F-4: none exists).
- **Alternatives considered**: drop and re-create the four tables (data lost); reset the migration history (old databases do not start).
- **Developer note**: the scaffolder may emit `DropTable` / `CreateTable` or `DropColumn` / `AddColumn` when the class and the table are renamed together - the generated file must be read and corrected to renames before `migration-check`. Expected verdict `REVIEW` -> G2b question naming the migration sha.
- **Not on the instance side**: `resource_instances.ResourceTypeId` has no foreign key to the schema table today (F-1 (5) lists only the FK from property definitions); the story does not add one.

## R3 - Contract delta

- **Decision**: `runs/016-rename-resource-schema/contract-delta.overlay.json` (13 actions) -> `contract-next.json`: two paths removed and re-added under `/resources/schemas`, two request models renamed, `resourceTypeId` -> `resourceSchemaId` in the create-instance request, the list row and the list filter. Operation names `… resource schema(s)`.
- **Rationale**: generated from the base swagger, so every unchanged detail is copied, not retyped. A search of `contract-next.json` (outside the provenance block) finds no old name.
- **Limits of the description**: schema GET responses, the single-instance response and error bodies are not described in the swagger today (no `Produces`), so `contract-diff` cannot see `resourceSchemaId` in `GET /resources/instances/{id}` nor the 409 code; those are covered by acceptance tests (AC-1, AC-7).

## R4 - "Same response" baseline (AC-10..AC-12)

- **Decision**: the baseline is the table in spec.md, derived from the code (F-2, F-5). Tests assert status, error code and message; not JSON casing (ASM-5).
- **Rationale**: discovery does not run the application; every row cites the handler line that produces it. Error codes `resources_forbidden` / `resources_domain` derive from exception class names that are not renamed (ASM-7).
- **Intended behaviours recorded as such (DEC-3)**: a member may list deleted resources; foreign vs missing ids are distinguishable (403 vs 404/400).

## R5 - Red-first scope (ASM-18)

- **Decision**: `red-first-prover --ac AC-1,AC-2,AC-6,AC-7,AC-10,AC-11,AC-12`. Tests for AC-9, AC-13, AC-14 are written in the same pass and are green from the start, because the migration precedes the blind tests.
- **Rationale**: see plan.md Complexity Tracking.
- **AC-9 test shape**: migrate an empty database to the last migration of `main` (`20260928111659_SoftDeleteResourceTypeUniqueIndex`), insert one row per Resources table with the old names (raw SQL on the module's own schema, with a comment explaining that the old shape cannot be expressed through the current model), migrate to latest, read the rows back through EF.
- **AC-13 test shape**: list table, column, constraint and index names of schema `resources` from the PostgreSQL catalog and assert none matches `resource_type` / `ResourceType` case-insensitively.
- **AC-14 test shape**: the model reports no pending changes against the snapshot.

## R6 - Frontend

- **Decision**: full rename of the hand-written service, DTOs and callers; three texts (DEC-4); the two generated files brought in line with the new swagger.
- **Facts**: the service uses `HttpClient` with literal URLs and imports only types from `api/data-contracts.ts`; the generated class `api/Resources.ts` is imported nowhere; there is no npm script for generation and no lint script (ASM-9, F-3, F-6).
- **Open for the delivery session (not a story decision)**: who edits frontend production code. The fleet has no frontend writer; `docs/agent-fleet-v4/NEXT-SESSION.md` lists the two candidates (conductor by hand, or a minimal `fe-writer`). Frontend spec tests are within the tester's ACL.

## R7 - Fleet preconditions for delivery (not story scope)

- AC-trait collision: existing Resources tests carry `[Trait("AC","AC-6")]`..`AC-10` from story 015; `red-first-prover` and `ac-matrix` select by AC id only. Must be resolved in the fleet before the blind-test phase.
- `skeleton-check` in this story sees only DAL configuration changes; Step 0 is before `baseline.js` by design.
