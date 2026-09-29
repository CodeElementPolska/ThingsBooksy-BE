You answer exactly ONE question about what the application can do today, from the generated artifacts only.

Rules:
- Read the question file listed below. Answer that question and nothing else, in plain language for a product owner (routes and screen names are fine, C# names are not).
- Use `"id": "F-1"` (assigned by the caller) and `"by": "capability-analyst <run_id from the provenance block>"`.
- Evidence items: `{ "generated": "generated/capability-map.json" }` / `{ "captured_response": "<the swagger fragment, verbatim, ≤ 500 chars>", "source": "generated/swagger.base.json" }`. A claim without evidence is not a fact — omit it.
- `confidence: "unknown"` when the artifacts do not show it (business rules inside handlers are invisible here); say what IS visible (the route exists, the response codes) and what is not.
- Facts only — no proposals, no judgement. Return ONLY the JSON object required by the schema below.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/016-rename-resource-schema/questions/1.md`  (sha256 2a8ce24aabef…)
- `generated/capability-map.json`  (sha256 461ba2aa78c0…)
- `generated/swagger.base.json`  (sha256 d6c98f972241…)
- `generated/modules.json`  (sha256 3dfa51a9e785…)

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
  "author_agent": "capability-analyst",
  "run_id": "016-rename-resource-schema-capability-analyst-1-mumofnrr",
  "story": "016-rename-resource-schema",
  "phase": "A",
  "inputs": [
    {
      "path": "runs/016-rename-resource-schema/questions/1.md",
      "sha256": "2a8ce24aabeffe467c47fef7492b43fdfbab833a669e957fc85dc0bf492cb9cb"
    },
    {
      "path": "generated/capability-map.json",
      "sha256": "461ba2aa78c0c9a87c1770a052ad83697165a8f78b8421d2cab7675ddfc9d6d4"
    },
    {
      "path": "generated/swagger.base.json",
      "sha256": "d6c98f9722415d4b8ba5e4b6d8dd07681d60b8b09a4eb066e0516790cfc16ebd"
    },
    {
      "path": "generated/modules.json",
      "sha256": "3dfa51a9e7853e4f1c9bbdde366b9a41caa7ba2358b56263594d5a254addd7a4"
    }
  ],
  "schema_version": "1"
}
```
