# Discovery round 2 — 016-rename-resource-schema

Date: 2026-10-02. Persona: dev-analyst.

## Why a second round
While shaping tasks for the delivery phases, dev-analyst checked the phase rules (docs/agent-fleet-v4/runbook-delivery.md, tools/fleet/skeleton-check.js, red-first-prover.js, fleet-acl.json) against the facts of round 1. No new code research; new facts come from fleet scripts and one grep over test traits.

## Findings
- A rename does not fit skeleton -> blind tests -> behaviour: renaming the entity breaks handlers (forbidden in the skeleton phase, skeleton-check.js:30-49); blind acceptance tests seed through entity/DbSet names, so they need the new internal names before the behaviour phase; be-writer may not edit tests and test-designer may not edit production code (fleet-acl.json, .claude/agents/be-writer.md:14-15).
- Fleet defect: existing Resources tests carry `[Trait("AC","AC-6")]` .. `AC-10` from story 015; this story has its own AC-6..AC-10. `red-first-prover` (filter `AC=AC-n`) and `ac-matrix` (regex `"(AC-\d+)"`) select by AC id only, so 015 tests would count as 016 tests (false coverage / false red). Blocks C2 until the fleet scopes traits by story. Not a story artifact; goes to the handoff.

## Owner interaction
AskUserQuestion (one question), recorded by `decide.js`:
| Decision | Chosen | Recommended? |
|---|---|---|
| DEC-5 | Two steps in one story | yes |

## What changed
- Delivery layout for tasks.md: **Step 0** (before `baseline.js`): behaviour-neutral internal rename of C# names in production and test code, database names pinned to today's (explicit ToTable + HasColumnName), nothing client-visible changes; proof = all existing tests green, exported swagger identical, no pending model change; performed by the developer or the delivery conductor; committed with the owner's yes. Then standard phases: skeleton = unpin database names; developer migration (rename operations, DEC-1) + migration-check + G2b; blind acceptance tests on the new contract; behaviour = routes, JSON fields, operation names, messages, error code, frontend.
- No scope change, no AC change, no assumption withdrawn. All decisions DEC-1..DEC-5 are DECIDED. The ZAPYTAJ bucket is empty -> writing the G2 package.

## Addendum (same day, while writing the G2 package)
- New fact **F-7** (Q7): an address with no endpoint answers an empty 404 for every method, with or without a token - no fallback, catch-all, static files or fallback authorization policy (confidence likely: framework default, not exercised). -> ASM-19 (NO_ASK); AC-2 needs nothing beyond removing the endpoints.
- **ASM-17** (ASSUME): "same response" in AC-10..AC-12 is read together with AC-7 and DEC-2.
- **ASM-18** (ASSUME): red-first is provable only for the contract criteria (AC-1, 2, 6, 7, 10, 11, 12); AC-9, AC-13, AC-14 test the migration, which the phase order places before the blind tests. Listed in plan.md Complexity Tracking as the one constitution-V exception; shown to the owner at G2. This corrects the statement made when DEC-5 was asked ("no script is waived"): no script is switched off, but `red-first-prover` runs with `--ac` for seven of the eleven criteria.
- story.md: `then` of AC-1, AC-8, AC-10, AC-11, AC-12 reworded after DEC-2, DEC-3, DEC-4 and ASM-6; section "Discovery decisions" added; front matter re-validated by `backlog-writer.js` (which re-rendered `issue-body.md`; GitHub issue #57 is not updated by discovery).
- `/speckit-plan`: `.specify/scripts/powershell/setup-plan.ps1` is blocked by the PowerShell execution policy on this machine; its only effect (copy the plan template to `specs/<story>/plan.md`) was done by hand. CLAUDE.md has no SPECKIT markers, so the agent-context step was skipped. The optional `before_plan` git-commit hook was not run (commits only with the owner's yes).

## Lint (speckit-clarify, speckit-analyze) - 2026-10-02
- `/speckit-clarify` (lint only, nothing asked, nothing written): no critical ambiguity; every taxonomy category Clear. The prerequisite script is blocked by the PowerShell execution policy; paths were taken from the branch.
- `/speckit-analyze` (read-only): 11/11 AC present in story.md, spec.md and tasks.md; 39 tasks, sequential ids, checklist format valid; no placeholders. Findings, all remediated in dev-analyst's own artifacts: FR-002, FR-004, SC-002, SC-003 had no task reference (added to T012, T013, T036, T019); T021 named a frontend spec file that does not exist (marked NEW); spec.md header and assumptions did not mention F-7, ASM-18, ASM-19 (added). One finding stays by design: constitution V red-first cannot hold for AC-9, AC-13, AC-14 (plan.md Complexity Tracking, ASM-18) - shown to the owner at G2.
- G2 package produced: spec.md, plan.md, research.md, data-model.md, contracts/README.md, quickstart.md, tasks.md, discovery/ui-sketch.md, contract-delta.overlay.json -> contract-next.json (compose OK).
