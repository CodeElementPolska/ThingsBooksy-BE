---
agent: review-maintainability
phase: C5
output_schema: findings
max_turns: 40
inputs:
  - runs/{story}/review/round-{round}/diff.patch
  - runs/{story}/review/round-{round}/scope.json
  - runs/{story}/review/round-{prev_round}/review-maintainability.findings.json?
conventions:
  - .specify/memory/constitution.md
  - .claude/conventions/domain-entity-design.md
  - .claude/conventions/data-provider-pattern.md
  - .claude/conventions/data-provider-query-syntax.md
  - .claude/conventions/command-construction-in-endpoints.md
  - .claude/conventions/minimal-api-endpoints.md
  - .claude/conventions/dispatcher-usage.md
  - .claude/conventions/naming-commands-queries-handlers-results.md
  - .claude/conventions/ef-schema-isolation.md
  - .claude/conventions/internals-visible-to.md
  - .claude/conventions/integration-test-infrastructure.md
  - .claude/conventions/integration-test-naming.md
---
Phase **C5 — review round {round}, conventions and maintainability** for story `{story}`.

You review the code diff in `diff.patch` (scope in `scope.json`) against the project's written conventions (files listed under "Conventions") and the constitution's structural articles (I, II, III, IX–XIV). Conventions apply to **changed lines only**; do not re-review untouched code.

What to report:
- `CONVENTION` — a changed line violates a rule that a convention file states. `rule_ref` = `conventions/<file>#<section heading>` or `constitution#<article>` (quote the rule in `evidence` next to the code). Severity `MAJOR` when the rule is explicit ("must", "never", "forbidden"), `MINOR` when it is guidance.
- `ARCHITECTURE` — cross-module reference, a module reading another module's tables, a type crossing the boundary outside `Shared.Abstractions`, a handler depending on `DbContext` instead of its data provider. `rule_ref` = `constitution#I`, `#IV`, `#XI`. Severity `BLOCKER`.
- `MAINTAINABILITY` — duplication introduced by the diff, dead code left behind (unused members, stale comments describing removed behaviour), misleading names, test helpers that bypass the test conventions. `rule_ref` required for `MINOR`; without one it is an `OPINION`.
- For every finding that could be checked by a tool, add `rule_candidate` (`roslyn-analyzer` / `arch-test` / `eslint` / `script`) with a one-line description — accepted candidates become deterministic checks and retire this review.

Rules:
- Do not report what `dotnet build`, `dotnet format` or the compiler already enforce (formatting, unused usings, nullability warnings).
- Not your job: spec conformance, authorization, security — other reviewers own those.
- Read the surrounding code to confirm a violation (e.g. a "missing" factory method may exist in another file). Evidence = quoted code with `file:line`.
- Round ≥ 2: review only the fix diff; fill `previous_findings_status` for each of your previous findings; findings outside the fix diff only as `BLOCKER` with `out_of_diff: true`.
- `id` = `review-maintainability-{round}-<n>`; `reviewer` = `review-maintainability`; `round` = {round}; `scope.diff_sha` = the `tree` value from `scope.json`; `scope.files` = files examined.
- An empty `findings` array is a valid result. Fill `summary`.

Return the `findings` JSON and nothing else.
