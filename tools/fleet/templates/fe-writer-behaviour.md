---
agent: fe-writer
phase: C3b
output_schema: result
max_turns: 80
inputs:
  - specs/{story}/spec.md
  - specs/{story}/plan.md
  - specs/{story}/tasks.md
  - specs/{story}/data-model.md?
  - runs/{story}/story.md
  - runs/{story}/discovery/ui-sketch.md?
  - runs/{story}/contract-next.json?
  - runs/{story}/discovery/decisions.jsonl
  - runs/{story}/discovery/assumptions.jsonl
conventions:
  - .claude/conventions/angular-folder-structure.md
  - .claude/conventions/angular-component-design.md
  - .claude/conventions/angular-http-pattern.md
  - .claude/conventions/angular-forms-pattern.md
  - .claude/conventions/angular-styling.md
  - .claude/conventions/angular-routing.md
---
Phase **C3b — behaviour (frontend)** for story `{story}`.

The frontend acceptance tests of this story (`frontend/src/app/**/*.spec.ts`, named `it("[AC-n] …")`) were written before you and are RED. Make them GREEN by implementing the frontend tasks of `tasks.md`.

Scope (take the matching tasks from `tasks.md` regardless of the phase heading they sit under):
- yours: tasks on frontend production code under `frontend/src/app/features`, `frontend/src/app/shared`, `frontend/src/app/core`, and the closing "frontend build and tests green" task;
- not yours: tasks marked `CONDUCTOR` or `OWNER`, backend tasks, test tasks (`*.spec.ts`), and any task on the generated client `frontend/src/app/api/` — the conductor regenerates it before your run. List the tasks you skipped for that reason in `tasks_remaining`.

Rules of this phase:
- The acceptance tests are read-only for you. Read them to understand the expected behaviour; run them with `npm --prefix frontend test`; never create, edit or delete a `*.spec.ts`. A test you believe is wrong → `status: DISPUTE_TEST` with evidence, do not work around it.
- `frontend/src/app/api/` is generated and read-only. If a type, field or name you need is missing there or disagrees with `contract-next.json` / `data-model.md`, do not alias or redeclare it locally: return `status: FAILED` with an `error` naming exactly what is missing (the conductor regenerates the client and starts a new run).
- The screens, elements, states and actions are the ones in `ui-sketch.md` and the spec. Do not add behaviour, screens or texts the spec does not ask for. User-visible texts spelled out in an AC are copied exactly. If a branch is needed that no AC covers (e.g. an error state), implement the minimal safe version and record it in `assumptions` with `visible_effect: true` so the reviewer sees it.
- The conventions in your prompt are binding.
- Commands run from the repository root, in exactly this form: `npm --prefix frontend run build`, `npm --prefix frontend test`. A task that says `cd frontend && npm run build` or `npm test` means these two commands — the `cd` form is rejected by your access rules.

Steps:
1. Read the inputs, the frontend tests of the story and the current code of the touched features.
2. Implement task by task; `npm --prefix frontend run build` after each task that changes types or templates.
3. `npm --prefix frontend test` — the story's `[AC-n]` tests green and no pre-existing test regressed; `npm --prefix frontend run build` green.
4. Return the `result` JSON with `tasks_completed`, `tasks_remaining`, `files_changed`, `assumptions`, `local_checks` (`build`, `unit_tests`; `format: SKIPPED`).
