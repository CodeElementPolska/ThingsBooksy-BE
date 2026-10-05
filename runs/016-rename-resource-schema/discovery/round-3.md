# Discovery round 3 (G2) — 016-rename-resource-schema

Date: 2026-10-02. Persona: dev-analyst.

## What was shown (one screen)
- Open decisions: none (DEC-1..DEC-5 DECIDED).
- ASSUME batch with consequences: ASM-13, ASM-14, ASM-15, ASM-17, ASM-18 (ASM-18 = the one exception to constitution V: red-first for seven contract criteria; AC-9, AC-13, AC-14 guarded by migration-check + G2b). Correction of the earlier statement "no script is waived" stated explicitly.
- Endpoint diff from `contract-next.json`; UI: three texts, no new screen; unresolved items for session C (frontend production actor; AC-trait collision in the fleet); story.md AC wording changed, GitHub issue #57 not refreshed.

## Owner answer
AskUserQuestion "Akceptujesz paczkę G2?" -> **"Akceptuję G2"**. Recorded by `decide.js --gate G2 --status PASSED --answer-ref latest`. No assumption vetoed.

## State at the end of discovery
- `specs/016-rename-resource-schema/`: spec.md, plan.md, research.md, data-model.md, contracts/README.md, quickstart.md, tasks.md (39 tasks, 8 phases).
- `runs/016-rename-resource-schema/`: story.md (reworded AC-1, 8, 10, 11, 12 + decisions section), issue-body.md (re-rendered), contract-delta.overlay.json, contract-next.json, discovery/ (facts F-0..F-7, assumptions ASM-1..ASM-19 all ACTIVE, decisions DEC-1..DEC-5 all DECIDED, questions 1-7, raw answers, rounds 1-3, ui-sketch.md, fleet-defects.md), prompts/.
- Nothing committed.
