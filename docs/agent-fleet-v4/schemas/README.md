# Schematy artefaktów floty v4

Każdy handoff między agentami to plik o schemacie z tego katalogu. Agent dostaje **tylko schemat swojego wyjścia** (przez `prompt-builder`), skrypty walidują wejścia i wyjścia, `status` wylicza stan z artefaktów. Draft 2020-12; `$ref: "fleet-v4/provenance"` = wspólny nagłówek.

| Schemat | Producent | Konsument | Walidator / kto odrzuca |
|---|---|---|---|
| `provenance` | każdy agent (wstrzykiwane przez prompt-builder) | każdy skrypt | skrypt konsumujący: hash wejścia ≠ aktualny ⇒ artefakt nieaktualny |
| `story` (front matter `story.md`) | scrum (sesja A) | backlog-writer, dev-analyst | backlog-writer przed zapisem do GitHuba; `track` wylicza skrypt z `blast_radius` |
| `critique` | scope-critic, premortem-critic | właściciel (niefiltrowane), scrum | schemat; `weight ≥ 3` bez `evidence` ⇒ obniżane do 2 |
| `decision` (`decisions.jsonl`) | dev-analyst, writerzy (`decisions_needed`), arbiter (`escalation`) | bramki G2/G3, DoD | **`decided_by`/`answer_ref` wpisuje tylko skrypt bramki** z `journal` (`OWNER_ANSWER`) |
| `assumption` (`assumptions.jsonl`) | dev-analyst, writerzy | paczka G2/G3, DoD | DoD: `score.hard_list=true` bez powiązanej decyzji ⇒ FAIL |
| `fact` (`facts.jsonl`) | code-researcher, capability-analyst | dev-analyst, contract-author | schemat; `confidence=unknown` dozwolone |
| `result` | be-writer, fe-writer, test-designer | dyrygent (branch po `status`), gate, closer | schemat + `files_changed ⊆ write_allow` (git diff vs ACL) |
| `findings` | reviewerzy, plan-guard, architecture-guard, trace-auditor | dedup-findings → writer / arbiter | schemat: severity > OPINION bez `rule_ref` ⇒ odrzucone; runda ≥ 2 bez `previous_findings_status` ⇒ odrzucone |
| `arbiter-verdict` | review-arbiter | dyrygent, writer, G3 | ESCALATE bez `escalation` ⇒ odrzucone |
| `fleet-acl` | człowiek (konfiguracja) | hook PreToolUse | walidacja przy starcie dyrygenta |
| `state` | skrypt `status` | dyrygent, właściciel | nigdy agent |
| `journal-event` (`journal.jsonl`) | hooki + skrypty faz | `status`, metryki, wznowienie | append-only |

## Artefakty bez JSON Schema (markdown o ustalonej strukturze)

- `spec.md`, `plan.md`, `tasks.md` — format SpecKita; dodatkowo AC muszą mieć id `AC-n` zgodne ze `story`.
- `ui-sketch.md` — sekcje: ekran → elementy → stany → akcje → endpoint (z `contract-delta`). Lint: każdy endpoint w akcjach istnieje w kontrakcie.
- `contract-delta` — **OpenAPI Overlay 1.0** (`overlay: 1.0.0`, `extends: generated/swagger.base.json`, `actions[]`), a nie własny format; skrypt `contract-validate` składa `base ⊕ overlay = contract-next.json` (pełny OpenAPI) dla generatora klienta TS i `contract-diff`. Każdy `action` zmieniający istniejącą ścieżkę wymaga `x-evidence` (fact id).
- `core-surface.json` — generowany (reflection po C3a): typy, właściwości, sygnatury `Create`, `DbSet`. Bez schematu ręcznego — kształt narzuca generator.
- `capability-map.json`, `swagger.base.json`, `ac-matrix.json`, `coverage-gaps.json` — generowane; kształt = wyjście skryptu.

## Definicje, od których zależą bramki

- **BLOCKER** = naruszenie AC, artykułu konstytucji, kontraktu lub kategorii D-1 (authz/dane/UI). Blokuje gate. **MAJOR** = naruszenie konwencji z `rule_ref` lub `UNSPECIFIED_BEHAVIOR`; musi być naprawione lub zakwestionowane przed zamknięciem. **MINOR** = `rule_ref` jest, kosmetyka; naprawa jeśli tania, inaczej issue długu. **OPINION** = brak `rule_ref`; nigdy nie blokuje; zbiorczo do właściciela.
- **„Blockery muszą maleć"**: `summary.blockers` w rundzie n+1 < rundzie n, inaczej eskalacja przed limitem rund.
- **Statusy writera**: `DONE | BLOCKED_ON_DECISION | DISPUTE | DISPUTE_TEST | FAILED` — dyrygent nie interpretuje prozy, tylko to pole.
- **Provenance**: artefakt, którego `inputs[].sha256` nie zgadza się z bieżącym stanem, jest nieaktualny — konsument odmawia, `status` pokazuje, co przeliczyć. Miękkie unieważnienie (tylko format): hash liczony po `dotnet format`.
