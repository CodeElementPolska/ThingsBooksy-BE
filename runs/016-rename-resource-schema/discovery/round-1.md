# Discovery round 1 — 016-rename-resource-schema

Dates: 2026-09-30 (setup) and 2026-10-02 (research, triage, decisions). Persona: dev-analyst. Owner answered in Polish; artifacts in English.

## Setup
- Branch `016-rename-resource-schema` = story id. `story.md` complete after G1 (AC-1, 2, 6-14).
- 2026-09-30: `.specify/feature.json` pointed at 015 and `prompt-builder` refused `impact-analyst` / `code-researcher` (ACL without `runs/*/prompts`). Both fixed with the owner's consent; superseded on 2026-10-01 by commit `247acc0` (feature.json removed, ACL fix committed, template->ACL test). `discovery/fleet-defects.md` is the session note of that day.
- 2026-10-02: session resumed; prompts rebuilt with the current `prompt-builder`; `status.js` exit 0.

## Research (facts F-0..F-6)
| Fact | Worker / question | Confidence | Key finding |
|---|---|---|---|
| F-0 | impact-analyst | likely | Single module (Resources, all layers) + ManagementGroups.IntegrationTests (test code) + 12 frontend files; no event / IModuleClient change; schema change in one module; flags RESOURCE_TYPE_NAME_TAKEN, includeDeleted for members, 403-vs-404 |
| F-1 | Q1 backend inventory | likely | 80 files / 681 matches; entity, 5 feature slices, routes, operation names, EF names, 4 migrations, message list, test inventory; nothing in Shared or Bootstrapper |
| F-2 | Q2 authorization today | certain | 10 endpoints, bare RequireAuthorization(); writes owner-only (403), reads owner-or-member (403 otherwise); 404 only on single GET; other "not found" = 400; list filter: own groupId + foreign schema -> 200 empty |
| F-3 | Q3 frontend inventory | likely | hand-written service with literal URLs; generated client (swagger-typescript-api) ; 4 visible texts with "type"; routes already "schemas" |
| F-4 | Q4 migrations | likely | no rename precedent; MigrateAsync at startup and in tests (Testcontainers + Respawn); EF default names, PascalCase columns, schema `resources`; raw SQL on resources.resource_types in ManagementGroups tests |
| F-5 | Q5 exact error bodies | likely | 403 `{errors:[{code:"resources_forbidden",message}]}`, 400 `resources_domain`, 409 bare `{code:"RESOURCE_TYPE_NAME_TAKEN",message}`; two tests assert the 409 code; member may pass includeDeleted=true; no test for it |
| F-6 | Q6 response fields / FE coupling | certain | schema responses carry no "type" field; only resourceTypeId (instance request, row, detail, query); frontend never reads error codes; generated client class imported nowhere |

Worker incidents: (1) impact-analyst returned `id: F-1` (its template assigns no id) - stored as **F-0** in `facts.jsonl`, raw answer untouched in `raw/impact-analyst.json`. (2) F-4 and F-5 came back without the `provenance` block (schema does not require it). (3) code-researcher cannot read `frontend/package.json` or the repo root (`docker-compose.yml`); dev-analyst read those two config files directly (ASM-9, ASM-10). (4) Subagent transcript files were empty, so worker answers were saved to `raw/*.json` from the hand-back messages. (5) The swagger digest "mismatch" reported in F-0 is raw-byte hash (capability-map) vs LF-normalised hash (prompt-builder) - not a defect of the story.

## Triage
- NO_ASK: ASM-1 (015 merged), ASM-2 (blast radius), ASM-3 (naming map), ASM-4 (contract delta), ASM-5 (baseline = code-derived table), ASM-6 (AC-12 two call shapes), ASM-7 (derived codes unchanged), ASM-8 (FE ignores codes), ASM-9 (generated client unused, no generator script), ASM-10 (dev DB volume, migrate at startup), ASM-11 (DB names to change, no sequences), ASM-12 (AC-7 message list), ASM-16 (old field silently ignored).
- ASSUME: ASM-13 (message naming ResourceTypeId joins the AC-7 list), ASM-14 (test code renamed too; repo-wide search rule), ASM-15 (frontend full rename, generated files brought in line).
- ZAPYTAJ: DEC-1 migration shape (hard list: data), DEC-2 error code, DEC-3 access observations (hard list: authz), DEC-4 UI texts.

## Owner interaction
One AskUserQuestion with four questions (ref `toolu_01B9xQL8G1YfpW9a8qQn5dDR`, 2026-10-02T11:10Z), recorded by `decide.js`:
| Decision | Chosen | Recommended? |
|---|---|---|
| DEC-1 | Rename in place | yes |
| DEC-2 | Rename to RESOURCE_SCHEMA_NAME_TAKEN | yes |
| DEC-3 | Both intended, no follow-up | no - owner declared both behaviours intended (members may list deleted resources; 403-vs-404 difference accepted) |
| DEC-4 | Change 3 group-screen texts | yes |

## What changed
- DEC-1: migration = rename operations only (table, 2 columns, 2 indexes) + re-created PK and FK; data survives; AC-9 wording stays; developer checks the scaffolded migration; G2b expected REVIEW.
- DEC-2: 409 code becomes RESOURCE_SCHEMA_NAME_TAKEN - the single named exception to "same result" in AC-1; joins the AC-7 list; two existing tests change their literal.
- DEC-3: no behaviour change, no backlog note; the AC-10 / AC-11 baseline records both behaviours as intended and tests assert them.
- DEC-4: column header "Schema"; empty state "Create the first schema to describe your resources."; aria-label "Add resource to schema <name>"; sign-in slogan unchanged (one intentional exception to the frontend search rule of ASM-14).
- No assumption withdrawn. No open decision. Next: check how a rename fits the delivery phases (skeleton / red-first), then spec.md.
