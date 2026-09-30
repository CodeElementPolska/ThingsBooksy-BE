---
agent: architecture-guard
phase: C6
output_schema: findings
max_turns: 40
inputs:
  - runs/{story}/review/round-1/scope.json
  - runs/{story}/story.md
  - specs/{story}/spec.md
  - runs/{story}/contract-next.json?
conventions:
  - .specify/memory/constitution.md
  - .claude/conventions/ef-schema-isolation.md
  - .claude/conventions/internals-visible-to.md
  - .claude/conventions/data-provider-pattern.md
---
Phase **C6 — architecture guard** for story `{story}`.

`scope.json` lists the files the story changed (round 1 of review). Use them as the entry point, but check the rules across the WHOLE solution: the per-file reviewers cannot see wiring that spans modules.

Checklist (report only what you can prove with quoted code; empty result is fine):
- **Module boundaries** (`constitution#I`, `#IV`): no project reference or `using` from one module's `.Core`/`.Api` to another module's; cross-module data only via events (`IEvent` records in `Shared.Abstractions/Events/<Module>/`) or `IModuleClient`; no module queries another module's tables or schema.
- **Event contracts** (`constitution#IV`): every new event record in `Shared.Abstractions` is published by exactly the module that owns the data; list its publishers and handlers across the solution (a new event with no handler anywhere is reported as `MAJOR` with the note that the owner must confirm the consumer is a future module — do not resolve it yourself); payloads carry identifiers only.
- **Persistence** (`constitution#VI`, `conventions/ef-schema-isolation.md`): each module's `DbContext` uses its own schema (`HasDefaultSchema`), one migration for the story, model snapshot consistent with the configuration (e.g. a filtered unique index present in both).
- **Visibility and registration** (`constitution#XIV`, `conventions/internals-visible-to.md`): `InternalsVisibleTo` set for `.Api`, `.Migrations`, `.IntegrationTests`, `DynamicProxyGenAssembly2`; new handlers/data providers registered where the module registers them (`AddDataProviders`, `Extensions.cs`); the Bootstrapper loads the module.
- **Layering** (`constitution#XI`, `conventions/data-provider-pattern.md`): handlers depend on `I…DataProvider`, never `DbContext`; endpoints construct commands and call `IDispatcher` only.

Rules:
- `id` = `architecture-guard-1-<n>`; `reviewer` = `architecture-guard`; `round` = 1; `scope.diff_sha` = the `tree` value from `scope.json`; `scope.files` = the files you examined.
- A finding that only the owner can resolve (e.g. an event awaiting a future module) carries `"needs_owner_decision": true` — the merge script then routes it to the owner instead of the writer.
- Severity: a violated NON-NEGOTIABLE article ⇒ `BLOCKER`; other articles/conventions ⇒ `MAJOR`; guidance ⇒ `MINOR`; no rule ⇒ `OPINION`. Add `rule_candidate` (`arch-test`) to every finding a NetArchTest-style test could catch.
- Fill `summary`.

Return the `findings` JSON and nothing else.
