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

## Input files (read these; nothing else was given to you)
- `runs/015-resource-time-buffer/story.md`  (sha256 dd2624433313…)
- `generated/capability-map.json`  (sha256 ef9341f9bd76…)
- `generated/core-surface.json`  (sha256 1c5ca522929d…)
- `generated/swagger.base.json`  (sha256 4ad8b820ca57…)

## Conventions that apply to your output
- `.specify/memory/constitution.md`

## Your output MUST validate against this JSON Schema
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "fleet-v4/fact",
  "title": "One entry of facts.jsonl — output of code-researcher / capability-analyst; every claim about existing behaviour needs evidence",
  "type": "object",
  "required": [
    "id",
    "claim",
    "evidence",
    "confidence",
    "by"
  ],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^F-[0-9]+$"
    },
    "question": {
      "type": "string",
      "description": "the single question the researcher was asked"
    },
    "claim": {
      "type": "string"
    },
    "evidence": {
      "type": "array",
      "minItems": 1,
      "items": {
        "oneOf": [
          {
            "type": "object",
            "required": [
              "file",
              "line"
            ],
            "properties": {
              "file": {
                "type": "string"
              },
              "line": {
                "type": "integer",
                "minimum": 1
              },
              "excerpt": {
                "type": "string",
                "maxLength": 300
              }
            }
          },
          {
            "type": "object",
            "required": [
              "captured_response"
            ],
            "properties": {
              "captured_response": {
                "type": "string",
                "description": "verbatim HTTP response / swagger fragment"
              },
              "source": {
                "type": "string"
              }
            }
          },
          {
            "type": "object",
            "required": [
              "generated"
            ],
            "properties": {
              "generated": {
                "type": "string",
                "description": "path in generated/ (capability-map, swagger.base, core-surface)"
              }
            }
          }
        ]
      }
    },
    "confidence": {
      "type": "string",
      "enum": [
        "certain",
        "likely",
        "unknown"
      ],
      "description": "unknown = 'I don't know' is a valid, expected answer"
    },
    "by": {
      "type": "string",
      "description": "agent_type + run_id"
    }
  }
}
```

## Provenance block — copy VERBATIM into the `provenance` field of your output
```json
{
  "author_agent": "impact-analyst",
  "run_id": "015-resource-time-buffer-impact-analyst-mufkjxac",
  "story": "015-resource-time-buffer",
  "phase": "B",
  "inputs": [
    {
      "path": "runs/015-resource-time-buffer/story.md",
      "sha256": "dd26244333136c55a59586883ea9dab698bbf5b7ff06e60a243b57df71b5b3b6"
    },
    {
      "path": "generated/capability-map.json",
      "sha256": "ef9341f9bd76df83af914e1bda2ba6c4d20f4cad1698fe971aae1bccb854048c"
    },
    {
      "path": "generated/core-surface.json",
      "sha256": "1c5ca522929db7f829ba60c35cb9066077118eff86bbd968b36798f8631f2945"
    },
    {
      "path": "generated/swagger.base.json",
      "sha256": "4ad8b820ca57f87cd41e331883f7a9951105a22d272fa9b1d805aef6423aa6eb"
    }
  ],
  "schema_version": "1"
}
```
