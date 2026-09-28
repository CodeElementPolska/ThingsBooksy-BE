---
agent: be-writer
phase: C5
output_schema: result
max_turns: 60
inputs:
  - runs/{story}/{review_dir}/dedup.json
  - runs/{story}/story.md
  - specs/{story}/spec.md
  - runs/{story}/contract-next.json?
  - runs/{story}/discovery/decisions.jsonl
  - runs/{story}/tests/red-first.json
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
Phase **C5 — fix after review round {round}** for story `{story}`, module `{module}`.

`dedup.json` is the merged review of the current code. Work through its `findings` in order:
- `BLOCKER` and `MAJOR`: fix each one, or dispute it. `MINOR`: fix when the change is local and cheap, otherwise leave it (say so in `notes`). `OPINION`: ignore unless trivially right.
- A fix changes only what the finding names. No new behaviour, no refactoring beyond the finding, no changes to acceptance tests (they are read-only; a finding that can only be satisfied by changing a test is a `DISPUTE_TEST`).
- To dispute a finding return `status: DISPUTE` with `dispute.target_id` = the finding `id`, your `argument`, the `rule_ref` you rely on (an AC, a convention section, a constitution article) and `evidence` with `file:line`. One dispute per run: fix everything else first, then dispute the one finding, so the round is not held up.
- A finding whose fix would change observable behaviour the spec does not cover (status codes, payloads, authorization, data retention) is not yours to decide: return `status: BLOCKED_ON_DECISION` with a D-2 question.

After the fixes: `dotnet build backend/ThingsBooksy.slnx`, `dotnet test backend/src/Modules/{module}/ThingsBooksy.Modules.{module}.IntegrationTests` (all green, no regression), `dotnet format backend/ThingsBooksy.slnx`. Report them in `local_checks`.

Return the `result` JSON: `tasks_completed` = the finding ids you fixed (as `notes.fixed[]`; `tasks_completed` itself stays for `T`-tasks and may be empty), `files_changed`, `assumptions` for any default you chose while fixing.
