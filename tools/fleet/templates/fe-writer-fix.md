---
agent: fe-writer
phase: C5
output_schema: result
max_turns: 60
inputs:
  - runs/{story}/{review_dir}/dedup.json
  - runs/{story}/story.md
  - specs/{story}/spec.md
  - runs/{story}/discovery/ui-sketch.md?
  - runs/{story}/contract-next.json?
  - runs/{story}/discovery/decisions.jsonl
conventions:
  - .claude/conventions/angular-folder-structure.md
  - .claude/conventions/angular-component-design.md
  - .claude/conventions/angular-http-pattern.md
  - .claude/conventions/angular-forms-pattern.md
  - .claude/conventions/angular-styling.md
  - .claude/conventions/angular-routing.md
---
Phase **C5 — fix after review round {round} (frontend)** for story `{story}`.

`dedup.json` is the merged review of the current code. Yours are the `findings` whose file is under `frontend/`; findings on backend files belong to the backend writer — leave them untouched and list their ids in `notes.not_mine[]`. Work through your findings in order:
- `BLOCKER` and `MAJOR`: fix each one, or dispute it. `MINOR`: fix when the change is local and cheap, otherwise leave it (say so in `notes`). `OPINION`: ignore unless trivially right.
- A fix changes only what the finding names. No new behaviour, no refactoring beyond the finding, no changes to `*.spec.ts` (tests are read-only; a finding that can only be satisfied by changing a test is a `DISPUTE_TEST`) and no changes to the generated client `frontend/src/app/api/` (a finding that needs one → fix the rest, then `status: FAILED` with an `error` naming the finding and what must be regenerated; the conductor regenerates the client).
- To dispute a finding return `status: DISPUTE` with `dispute.target_id` = the finding `id`, your `argument`, the `rule_ref` you rely on (an AC, a convention section) and `evidence` with `file:line`. One dispute per run: fix everything else first, then dispute the one finding, so the round is not held up.
- A finding whose fix would change observable behaviour the spec does not cover (screens, texts, navigation, what a role can see or do) is not yours to decide: return `status: BLOCKED_ON_DECISION` with a D-2 question.

After the fixes, from the repository root and in exactly this form: `npm --prefix frontend run build`, `npm --prefix frontend test` (all green, no regression). Report them in `local_checks` (`build`, `unit_tests`; `format: SKIPPED`).

Return the `result` JSON: the finding ids you fixed as `notes.fixed[]` (`tasks_completed` stays for `T`-tasks and may be empty), `files_changed`, `assumptions` for any default you chose while fixing.
