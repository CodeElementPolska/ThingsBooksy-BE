# Discovery round 1 — 015-resource-time-buffer

Date: 2026-09-24. Persona: dev-analyst. Owner answered in Polish; artifacts in English.

## Setup
- Branch renamed by owner decision: `feat/015-ResourceTimeBuffor` → `015-resource-time-buffer` (local only, never pushed).
- Issue text supplied by the owner in chat (issue **#10**, not #22 as first stated; `gh` not installed, GitHub MCP not connected). `story.md` written from it and validated against `story.schema.json`.
- Epic document with decisions no. 2 / no. 3 is not in the repo (grep of docs/ and specs/ for BufferMinutes / Availability epic: nothing). Epic number still unconfirmed.

## Research (facts F-1..F-6, all `certain` except F-1 `likely`)
| Fact | Question | Key finding |
|---|---|---|
| F-1 | impact-analyst | Resources + Shared.Abstractions; no Availability module; no Events/Resources; migration likely; upper limit undefined |
| F-2 | Q1 ResourceType entity | no validation in entity; has DeletedAt/Delete(now) but handler hard-deletes; 3 migrations; unique (GroupId, Name) not filtered |
| F-3 | Q2 HTTP surface | 5 endpoints, RequireAuthorization() + owner/member checks in handlers |
| F-4 | Q4 delete flows | schema delete = bulk soft-delete instances (SQL) + hard delete type + one SaveChangesAsync; GroupDeletedHandler bulk ExecuteDeleteAsync |
| F-5 | Q5 validation | inline guards → ResourcesDomainException → 400 `{Errors:[{Code,Message}]}`; no validator lib |
| F-6 | Q3b events | IMessageBroker.PublishAsync after SaveChangesAsync; records `: IEvent` without `Event` suffix; InMemoryMessageBroker → outbox / async channel; tests observe consumer side effects |

Worker incidents: Q3 first attempt (`raw/q3-first-attempt.invalid.jsonl`) read the prompt line "nothing else was given to you" as a read boundary and answered `unknown`; re-asked as Q3b with an explicit scope note. Template defect to report to the fleet owner. All workers returned `id: F-1`; ids renumbered by dev-analyst. Six excerpts >300 chars truncated in F-2 (full text in `raw/q1.json`).

## Triage
- NO_ASK: ASM-1, ASM-2, ASM-3, ASM-4, ASM-10 (ASM-1/2 later withdrawn).
- ASSUME: ASM-5 (event names as in issue), ASM-6 (deleted payloads), ASM-7, ASM-8, ASM-9 (later withdrawn).
- ZAPYTAJ: DEC-1 (upper limit), DEC-2 (migration default — hard list), DEC-3 (epic decision 3 / source of truth), DEC-4 (cascade events).

## Owner interaction
1. First AskUserQuestion (4 questions) — owner stopped it to clarify the cascade question. Provisional picks (not recorded): Resources-master+Updated, NOT NULL 0, 10080.
2. Explained cascade (layer 1: what Resources already does; layer 2: what consumers are told). Corrected a misunderstanding: the schema is hard-deleted today, instances are already soft-deleted.
3. Owner (free text): **option A** for cascade + new rule **"everything deleted must be a soft delete"** → DEC-5 added (hard list: data).
4. Owner asked for an ownership analysis of the buffer. Analysis given (table + recommendation flipped to Availability-owner; strongest counter-argument stated). DEC-3 options rewritten.
5. AskUserQuestion DEC-3 → **"Availability owns the buffer - rescope this story to lifecycle events only"** (decide.js, ref toolu_01EVfvupSEaGDrTLU7QAUGz3).
6. AskUserQuestion DEC-4 + DEC-5 → **"Only ResourceSchemaDeletedEvent; consumer cascades by SchemaId"**, **"Soft-delete in DELETE command only"** (decide.js, ref toolu_01CtP714WvAAJyoUNKtqauUS).

## What changed
- DEC-1, DEC-2 → WITHDRAWN (BufferMinutes leaves the story). ASM-1, ASM-2, ASM-7, ASM-8, ASM-9 marked `withdrawn`.
- story.md rescoped: AC-1, AC-2 removed; AC-3 payload `(SchemaId, GroupId)`; AC-5 fixed to one event + no per-instance events; AC-6 payload fixed; title/why updated; original issue text kept below the front matter with markers. Epic decision no. 3 closed by DEC-3.
- blast_radius: touches_contract false (no request/response change), touches_schema to be set after Q7 (partial unique index for soft delete).
- New research spawned: Q6 (how tests can observe a published event; broker config under test), Q7 (uniqueness check vs soft delete; index definitions; IgnoreQueryFilters call sites).

## Open at end of round
- ZAPYTAJ bucket: empty. Pending: facts F-7, F-8 → new assumptions for soft-delete details (AC-8) → spec.
- Not asked (metadata): epic issue number; issue #10 title/AC update is a business-layer task after DEC-3.
