# Discovery round 2 — 015-resource-time-buffer (G2 package assembly)

Date: 2026-09-24. No owner questions in this round (ZAPYTAJ bucket empty after round 1); the round produced new facts and the G2 artifacts.

## New facts
- F-7 (Q6): tests run with outbox disabled + async in-process dispatcher; no event sink exists; existing tests prove publication via consumer side effects only. → ASM-11 (recording IMessageBroker decorator in ThingsBooksyWebAppFactory).
- F-8 (Q7): app-level name check already excludes soft-deleted rows, DB unique index on (GroupId, Name) is NOT filtered → partial index required (folded into DEC-5 consequences; ASM-12 removed as a hard-list assumption); two existing tests assert hard delete (UpdateDeleteResourceTypeTests.cs:185/:283) → ASM-14.

## Assumptions added
ASM-11, ASM-13..ASM-19 (ASM-12 removed — covered by DEC-5, decided by owner). Active set: ASM-3, 4, 5, 6, 10, 11, 13, 14, 15, 16, 17, 18, 19.

## Story changes
- AC-8 (schema soft delete) and AC-9 (name reuse) added; `touches_schema: true`.

## Artifacts written
- `specs/015-resource-time-buffer/spec.md` (AC-3..AC-9, FR-001..FR-012, SC-001..SC-005)
- `plan.md`, `research.md`, `data-model.md`, `contracts/events.md`, `quickstart.md` (written by dev-analyst — SpecKit `setup-plan.ps1` mis-targeted `specs/010-…` because `.specify/feature.json` still pointed there; its `plan.md` was overwritten by the template and restored with `git checkout`; `feature.json` now points to `specs/015-resource-time-buffer`)
- `tasks.md` — 30 tasks, phases: setup 2 · skeleton 10 · acceptance tests RED 5 · US1 5 · US2 3 · polish 5
- `runs/015-resource-time-buffer/contract-delta.overlay.json` (annotation-only) → `contract-next.json`; endpoint diff vs base: 0 added / 0 removed / 0 changed (28 operations)
- No `ui-sketch.md` (touches_ui: false)

## Lint (clarify + analyze, read-only)
| Id | Severity | Finding | Routed to |
|---|---|---|---|
| C1 | LOW | Spec did not state that failed commands (403/400) publish nothing | spec.md Edge Cases (added) |
| C2 | LOW | Restore of a soft-deleted schema not mentioned | spec.md Edge Cases (added: out of scope) |
| C3 | LOW | Race on name reuse after soft delete | spec.md Edge Cases (added: DB constraint, unchanged 500 path) |
| A1 | MEDIUM | AC-7 test as a pure reflection test would pass before behaviour → violates constitution V red-first | tasks.md T014/T017 rewritten: AC-7 asserts on *recorded* events, red until Phase 4 |
| A2 | MEDIUM | FR-011 (GroupDeleted path untouched) had no task | tasks.md T029: diff must not include GroupDeletedHandler.cs |
| A3 | MEDIUM | Test client helper names assumed (filtered vs unfiltered read) — F-8 hints `GetResourceTypeFromDbAsync` may already ignore filters | tasks.md T011: read first, then name both helpers |
| A4 | LOW | `Event` suffix vs `GroupCreated` style | ASM-5 rationale: constitution Art. IV example uses the suffix |
| A5 | LOW | Unit test project `Resources.Tests.Unit` does not exist | tasks.md T027: record UNSPECIFIED instead of scaffolding in this story |
Coverage: FR-001..FR-012 and SC-001..SC-005 each map to ≥1 task; AC-3..AC-9 each have a tagged test task. Constitution: no violations. Open decisions: 0. Hard-list assumptions: 0.

## Fleet defects observed (for the fleet owner, not for this story)
1. `templates/code-researcher.md` line "Input files (read these; nothing else was given to you)" was read as a read boundary by one worker (Q3) → `unknown`; a scope note in the question fixed it.
2. All workers return `id: F-1`; dev-analyst renumbers. Some exceed `excerpt` 300 chars.
3. `status.js` reports `G2: PASSED` before the owner accepted G2 — `owner-answer-hook` logs every decision answer as `GATE_ANSWER phase G2`.
4. SpecKit `.specify/feature.json` overrides the branch → `setup-plan.ps1` silently targets another story's folder (overwrote `specs/010-…/plan.md`; restored).
5. `SendMessage` is disabled in this session — a worker cannot be continued; re-ask = new instance.

## G2 gate
Owner answered "Tak — akceptuję G2" via AskUserQuestion (captured by owner-answer-hook in journal.jsonl as the latest OWNER_ANSWER). No assumption vetoed. Discovery session ends; delivery pipeline takes specs/015-resource-time-buffer/ + runs/015-resource-time-buffer/ as input. Nothing committed — owner decides.
