---
agent: impact-analyst
phase: B
output_schema: fact
max_turns: 40
inputs:
  - runs/{story}/story.md
  - generated/capability-map.json
  - generated/core-surface.json
  - generated/swagger.base.json?
conventions:
  - .specify/memory/constitution.md
---
Map the impact of the story described in the input files on the current codebase.

Produce ONE JSON object of the `fact` schema whose `claim` is a structured summary, and put every individual finding into `evidence` entries. Use this ordering inside `claim` (plain text, one line per item, prefix each with its tag):
- `MODULE:` module name — touched / crossed-boundary
- `ENTITY:` existing entity or read model affected (from core-surface)
- `ENDPOINT:` existing endpoint affected (method + route from capability-map)
- `SCREEN:` existing Angular route/component affected
- `EVENT:` existing event / IModuleClient contract involved, or "new contract needed" (fact: none exists)
- `SCHEMA:` "likely change" / "no change" with the reason
- `BLOCKED_BY:` missing prerequisite (story id, feature, contract) or "none"
- `RISK:` factual risk (data loss path, authorization gap, timing) with evidence

Set `confidence` to the lowest confidence among your claims. If the story cannot be mapped (too vague, contradicts the capability map), say so in `claim` with `confidence: "unknown"` — that is a valid outcome and triggers a return to the business session.
