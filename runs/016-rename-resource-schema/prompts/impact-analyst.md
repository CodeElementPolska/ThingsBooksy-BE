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

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/016-rename-resource-schema/story.md`  (sha256 13204bfba604…)
- `generated/capability-map.json`  (sha256 01040357aff3…)
- `generated/core-surface.json`  (sha256 72e6a5cc2c4c…)
- `generated/swagger.base.json`  (sha256 d6c98f972241…)

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
                "maxLength": 500
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
  "run_id": "016-rename-resource-schema-impact-analyst-muqu98d8",
  "story": "016-rename-resource-schema",
  "phase": "B",
  "inputs": [
    {
      "path": "runs/016-rename-resource-schema/story.md",
      "sha256": "13204bfba604995bd8379042db89e1a61c595aa8982745bc7925781ef7fc5b50"
    },
    {
      "path": "generated/capability-map.json",
      "sha256": "01040357aff3a256ea13ee13ac2d7dc16e6e41b283ac5a2827c136803735e18d"
    },
    {
      "path": "generated/core-surface.json",
      "sha256": "72e6a5cc2c4c208beeed3f341bc57fe2d0d5908f7083891b5f7e689370f5476b"
    },
    {
      "path": "generated/swagger.base.json",
      "sha256": "d6c98f9722415d4b8ba5e4b6d8dd07681d60b8b09a4eb066e0516790cfc16ebd"
    }
  ],
  "schema_version": "1"
}
```
