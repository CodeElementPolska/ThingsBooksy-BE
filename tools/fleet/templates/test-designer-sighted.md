---
agent: test-designer-sighted
phase: C4b
output_schema: result
max_turns: 60
inputs:
  - runs/{story}/story.md
  - specs/{story}/spec.md
  - specs/{story}/tasks.md
  - runs/{story}/coverage-gaps.json
  - runs/{story}/ac-matrix.json
  - runs/{story}/tests/red-first.json
conventions:
  - .claude/conventions/integration-test-infrastructure.md
  - .claude/conventions/integration-test-naming.md
---
Phase **C4b — second test pass (sighted)** for story `{story}`, module `{module}`.

`coverage-gaps.json` lists lines and branches of the story's NEW production code that the acceptance tests do not exercise. For each gap: read the code, decide whether it belongs to an AC (`[Trait("AC","AC-n")]`), is unspecified behaviour (`[Trait("AC","UNSPECIFIED")]` + an `assumptions` entry starting with `UNSPECIFIED:`), or is unreachable (explain in `notes`). Write the tests in the story's test files (new methods or new files) — never modify the blind-pass tests (`red-first.json` carries their hash).

Line coverage cannot see behaviour that flows through UNCHANGED branches (e.g. "deleting an already-deleted row must publish nothing"). So also take the test tasks of this phase from `tasks.md` (regardless of the phase heading they sit under) and write every test they name, with the tag they prescribe; if `coverage-gaps.json` has no gaps, those tasks are your whole scope.

Run `dotnet test backend/src/Modules/{module}/ThingsBooksy.Modules.{module}.IntegrationTests` before returning; all tests green. Return the `result` JSON.
