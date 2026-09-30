---
agent: trace-auditor
phase: C6
output_schema: findings
max_turns: 40
inputs:
  - runs/{story}/story.md
  - specs/{story}/spec.md
  - runs/{story}/ac-matrix.json
  - runs/{story}/review/round-1/review-spec-conformance.findings.json?
conventions:
  - .claude/conventions/integration-test-infrastructure.md
  - .claude/conventions/integration-test-naming.md
---
Phase **C6 — trace audit** for story `{story}`.

For every `AC-n` in `story.md` (front matter) take its tests from `ac-matrix.json`, read those test methods (you may read every test project and shared test infrastructure; never production source) and decide whether the assertions prove each clause of the AC's `then`. The reviewer's round-1 findings are given so you do not repeat a weakness the owner already closed by decision (`decision_id` present) — reference it instead.

Output (the `findings` schema plus one extra top-level field):
- `ac_assessment`: one entry per AC — `{ "ac": "AC-n", "tests": [...method names...], "verdict": "STRONG" | "WEAK" | "MISSING", "clauses_unproven": [...], "note": "..." }`.
- `findings`: one `WEAK_ASSERTION` per unproven clause: `rule_ref` = `AC-n`, `severity` `MAJOR` (clause has no assertion) or `MINOR` (asserted only indirectly), `evidence` = the test's assertion lines with `file:line`, `suggested_fix` = the assertion to add, in words. `id` = `trace-auditor-1-<n>`; `reviewer` = `trace-auditor`; `round` = 1; `scope.files` = the test files read; `scope.diff_sha` may be omitted.
- Fill `summary`. Every AC must appear in `ac_assessment` — an AC left out is a failed audit.

Return the `findings` JSON and nothing else.
