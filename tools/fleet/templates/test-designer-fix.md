---
agent: test-designer-sighted
phase: C5
output_schema: result
max_turns: 40
inputs:
  - runs/{story}/review/round-{round}/dedup.json
  - runs/{story}/story.md
  - specs/{story}/spec.md
  - runs/{story}/tests/red-first.json
conventions:
  - .claude/conventions/integration-test-infrastructure.md
  - .claude/conventions/integration-test-naming.md
---
Phase **C5 — test fixes after review round {round}** for story `{story}`, module `{module}`.

`dedup.json` is the merged review. Work only on the findings with `route: "tester"` (they concern test code): fix each `BLOCKER` and `MAJOR`; fix a `MINOR` when the change is local and cheap, otherwise say why not in `notes`; ignore the rest.

Rules:
- The blind-pass acceptance tests are read-only: the files listed in `red-first.json` → `acceptance_test_files` must not change (their hash is checked by the gate). A finding that could only be satisfied by editing one of them is a `DISPUTE_TEST` with the finding id as `dispute.target_id`, or, if you agree with the finding, a `status: BLOCKED_ON_DECISION` asking the owner to authorise a REBASELINE.
- A fix changes only what the finding names; it must not weaken any assertion or drop an AC tag. Shared infrastructure (factories, test client) may be extended in the convention's shape (`Clients/{Entity}Factory.cs`, DB reads on the TestClient with `IgnoreQueryFilters()`).
- To dispute a finding return `status: DISPUTE` with `dispute.target_id`, your `argument`, the `rule_ref` you rely on and `evidence` with `file:line`. Fix everything else first.

Then `dotnet build` and `dotnet test backend/src/Modules/{module}/ThingsBooksy.Modules.{module}.IntegrationTests` — all green, no regression. Report in `local_checks`.

Return the `result` JSON with `files_changed`, `assumptions`, and `notes.fixed[]` = the finding ids you fixed.
