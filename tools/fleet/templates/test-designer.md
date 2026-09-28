---
agent: test-designer
phase: C2
output_schema: result
max_turns: 80
inputs:
  - runs/{story}/story.md
  - specs/{story}/spec.md
  - specs/{story}/tasks.md
  - runs/{story}/contract-next.json?
  - runs/{story}/discovery/decisions.jsonl
  - generated/core-surface.json
  - generated/capability-map.json?
conventions:
  - .specify/memory/constitution.md
  - .claude/conventions/integration-test-infrastructure.md
  - .claude/conventions/integration-test-naming.md
---
Phase **C2 — acceptance tests (blind pass)** for story `{story}`, module `{module}`.

The skeleton exists (entities, EF configuration, contract records) but NO behaviour: the tests you write must compile and must FAIL. Write the acceptance tests for every `AC-n` of the story (see `story.md` front matter and the `**AC-n**` scenarios in `spec.md`), plus the test infrastructure they need.

Scope of this phase (take the matching tasks from `tasks.md` regardless of the phase heading they sit under):
- test infrastructure: factories for new entities (use `Create(...)` signatures from `generated/core-surface.json`), test-client methods for the endpoints in the contract, shared helpers such as a recording `IMessageBroker` decorator registered in `ThingsBooksyWebAppFactory`;
- one or more test classes per user story; each test tagged `[Trait("AC", "AC-n")]`; existing tests whose expectations the story changes are rewritten (with the AC tag) — say which in `notes`.

Rules:
- Arrange via EF + factories, Act via HTTP, Assert via EF re-read (`IgnoreQueryFilters()`) and recorded events. Never seed through the API.
- Assert exact user-visible messages when the AC spells them out; assert status codes; assert DB state after the act.
- Every test must fail for the right reason. Build the test projects: `dotnet build backend/src/Modules/{module}/ThingsBooksy.Modules.{module}.IntegrationTests` (and `backend/src/Shared/ThingsBooksy.Shared.IntegrationTests` if you changed it). Then run `dotnet test backend/src/Modules/{module}/ThingsBooksy.Modules.{module}.IntegrationTests --filter "AC=AC-3|…"` for your AC ids and confirm they FAIL (report counts in `notes`).
- If an AC cannot be encoded as a test (ambiguous, contradicts the contract or the type surface), return `status: BLOCKED_ON_DECISION` with a D-2 question; do not invent semantics.

Return the `result` JSON: `tasks_completed` (test tasks), `files_changed`, `assumptions` (anything you had to choose — e.g. a factory default), `local_checks.build`.
