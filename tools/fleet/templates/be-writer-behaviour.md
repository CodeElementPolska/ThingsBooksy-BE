---
agent: be-writer
phase: C3b
output_schema: result
max_turns: 80
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
  - runs/{story}/tests/red-first.json
  - runs/{story}/ac-matrix.json?
conventions:
  - .specify/memory/constitution.md
  - .claude/conventions/domain-entity-design.md
  - .claude/conventions/data-provider-pattern.md
  - .claude/conventions/data-provider-query-syntax.md
  - .claude/conventions/command-construction-in-endpoints.md
  - .claude/conventions/minimal-api-endpoints.md
  - .claude/conventions/dispatcher-usage.md
  - .claude/conventions/naming-commands-queries-handlers-results.md
  - .claude/conventions/internals-visible-to.md
---
Phase **C3b — behaviour** for story `{story}`, module `{module}`.

The acceptance tests listed in `runs/{story}/tests/red-first.json` are RED. Make them GREEN by implementing the behaviour tasks of `tasks.md` (user-story phases): handlers, endpoints, domain methods, event publishing, plus unit tests for domain logic where a `Tests.Unit` project exists.

Rules of this phase:
- The acceptance tests are read-only for you (`backend/src/Modules/*/…IntegrationTests`, `frontend/**/*.spec.ts`). Read them to understand the expected behaviour; run them with `dotnet test <module IntegrationTests project>`; never edit them. A test you believe is wrong → `status: DISPUTE_TEST` with evidence, do not work around it.
- Do not add behaviour the spec does not ask for. If a branch is needed that no AC covers (e.g. an error path), implement the minimal safe version and record it in `assumptions` with `visible_effect: true` so the reviewer sees it.
- Constitution and conventions in your prompt are binding; keep `SaveChangesAsync` before `PublishAsync`; keep handler constructors ≤ 4 params; DataProvider per handler.
- Do not touch migrations. If the model must change again, stop and return `status: BLOCKED_ON_DECISION` (a second migration is not allowed; the developer regenerates the single one).

Steps:
1. Read the inputs, the red tests and the current module code for the touched features.
2. Implement task by task; after each task `dotnet build`.
3. `dotnet test <module IntegrationTests>` — all story tests (AC filter) green and no pre-existing test regressed; `dotnet format backend/ThingsBooksy.slnx` at the end.
4. Return the `result` JSON with `tasks_completed`, `files_changed`, `assumptions`, `local_checks`.
