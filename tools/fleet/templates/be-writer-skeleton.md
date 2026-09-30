---
agent: be-writer
phase: C3a
output_schema: result
max_turns: 60
inputs:
  - specs/{story}/spec.md
  - specs/{story}/plan.md
  - specs/{story}/tasks.md
  - specs/{story}/data-model.md?
  - specs/{story}/contracts/*.md?
  - runs/{story}/story.md
  - runs/{story}/contract-next.json?
  - runs/{story}/discovery/decisions.jsonl
  - runs/{story}/discovery/assumptions.jsonl
conventions:
  - .specify/memory/constitution.md
  - .claude/conventions/domain-entity-design.md
  - .claude/conventions/ef-schema-isolation.md
  - .claude/conventions/naming-commands-queries-handlers-results.md
---
Phase **C3a — skeleton** for story `{story}`, module `{module}`.

Implement ONLY the skeleton tasks of `tasks.md` (the "Foundational"/skeleton phase) that are within the skeleton scope:
- new or changed **entities and read models** (properties with private setters, `Create` factory taking the command record, no domain methods with business logic),
- **EF configuration** and `DbSet`s,
- **command/query/result records** (data only, no handlers),
- **event / IModuleClient contract records** in `Shared.Abstractions` when the plan defines them.

Explicitly NOT in this phase, even if `tasks.md` lists them under the same heading: handlers, endpoints, event handlers, domain methods that change state, anything in test projects (factories, test clients, recording brokers — those belong to the test designer in the next phase), and migrations (`dotnet ef migrations add` is run by the developer; list the migration task under `tasks_remaining` with note `developer-only`).

Steps:
1. Read the inputs; list the task ids you will do and the ones you will leave (with the reason) — this list goes into the result.
2. Implement; keep each file in the module's existing style.
3. `dotnet build backend/ThingsBooksy.slnx` must be green.
4. `node tools/fleet/skeleton-check.js --story {story}` must report 0 violations — if it reports one, fix it or move the task to `tasks_remaining` with the reason.
5. Return the `result` JSON (`schema_changes`: `ADDITIVE` if you added/changed columns or indexes, `DESTRUCTIVE` if a column/table is dropped or narrowed, else `NONE`).
