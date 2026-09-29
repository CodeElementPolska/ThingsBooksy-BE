Blind critique of the story proposal `016-rename-resource-schema` — **value and scope**.

Read `proposal.md` (the story front matter, the narrative and the `rejected_alternatives`), then the capability report (facts about what the application does today, with ids `F-n`) and the capability map. Judge the proposal on the four rubrics `value`, `cheapest-version`, `scope-creep`, `dependency` and nothing else.

Output rules:
- `critic` = `scope-critic`; `verdict` = `NO_OBJECTIONS` or `OBJECTIONS`; `reply_round` = 1 (or 2 when `## Owner reply` is present in the proposal — then judge only whether the reply answers your earlier items, one last time).
- Each item: `id` = `scope-<n>`, `rubric`, `weight` 1–5 (5 = the story would be wrong or pointless as written), `objection` in two or three plain sentences, `evidence` (`F-n` or a quote from the proposal; without it weight ≤ 2), `alternative` (concrete: the smaller story, the AC to drop, the dependency to name), `addresses_rejected_alternative` when you repeat a rejected option.
- At most six items. Do not restate the proposal. Do not design.

Return the `critique` JSON and nothing else.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/016-rename-resource-schema/proposal.md`  (sha256 5c7630570217…)
- `runs/016-rename-resource-schema/capability-report.jsonl`  (sha256 fadb09623558…)
- `generated/capability-map.json`  (sha256 461ba2aa78c0…)

## Your output MUST validate against this JSON Schema
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "fleet-v4/critique",
  "title": "critique.json — output of scope-critic / premortem-critic (session A); goes to the owner unfiltered",
  "type": "object",
  "required": [
    "provenance",
    "critic",
    "verdict",
    "items"
  ],
  "properties": {
    "provenance": {
      "$ref": "fleet-v4/provenance"
    },
    "critic": {
      "type": "string",
      "enum": [
        "scope-critic",
        "premortem-critic"
      ]
    },
    "verdict": {
      "type": "string",
      "enum": [
        "NO_OBJECTIONS",
        "OBJECTIONS"
      ]
    },
    "items": {
      "type": "array",
      "items": {
        "type": "object",
        "required": [
          "id",
          "rubric",
          "weight",
          "objection",
          "alternative"
        ],
        "properties": {
          "id": {
            "type": "string"
          },
          "rubric": {
            "type": "string",
            "description": "scope-critic: value | cheapest-version | scope-creep | dependency. premortem-critic: data-loss | migration | authz | ops | timing | concurrency",
            "enum": [
              "value",
              "cheapest-version",
              "scope-creep",
              "dependency",
              "data-loss",
              "migration",
              "authz",
              "ops",
              "timing",
              "concurrency"
            ]
          },
          "weight": {
            "type": "integer",
            "minimum": 1,
            "maximum": 5,
            "description": "5 = would make the story wrong or harmful; 1 = nice to know"
          },
          "objection": {
            "type": "string"
          },
          "evidence": {
            "type": "string",
            "description": "reference to capability-report / story text; opinions without evidence get weight ≤ 2"
          },
          "alternative": {
            "type": "string",
            "description": "mandatory: what to do instead"
          },
          "addresses_rejected_alternative": {
            "type": "string",
            "description": "if the objection repeats a rejected alternative, name it — the owner already decided"
          }
        }
      }
    },
    "reply_round": {
      "type": "integer",
      "minimum": 1,
      "maximum": 2,
      "description": "1 = initial critique, 2 = single reply after the owner's response; no further rounds"
    }
  }
}
```

### Referenced schema `fleet-v4/provenance` (items of the fields that `$ref` it must have this shape)
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "fleet-v4/provenance",
  "title": "Provenance header — common to every agent-produced artifact",
  "type": "object",
  "required": [
    "author_agent",
    "run_id",
    "story",
    "inputs"
  ],
  "properties": {
    "author_agent": {
      "type": "string",
      "description": "agent_type that produced the artifact (as seen by hooks)"
    },
    "agent_id": {
      "type": "string"
    },
    "run_id": {
      "type": "string",
      "description": "workflow run id or session id"
    },
    "story": {
      "type": "string",
      "pattern": "^[0-9]{3}-[a-z0-9-]+$",
      "description": "NNN-slug (SpecKit branch/dir name)"
    },
    "phase": {
      "type": "string",
      "enum": [
        "A",
        "B",
        "C1",
        "C2",
        "C3a",
        "C3b",
        "C4",
        "C4b",
        "C5",
        "C6"
      ]
    },
    "inputs": {
      "type": "array",
      "description": "Every file the agent was given. A consumer script rejects the artifact if any input hash changed since.",
      "items": {
        "type": "object",
        "required": [
          "path",
          "sha256"
        ],
        "properties": {
          "path": {
            "type": "string"
          },
          "sha256": {
            "type": "string",
            "pattern": "^[a-f0-9]{64}$"
          }
        }
      }
    },
    "schema_version": {
      "type": "string",
      "default": "1"
    }
  }
}
```

## Provenance block — copy VERBATIM into the `provenance` field of your output
```json
{
  "author_agent": "scope-critic",
  "run_id": "016-rename-resource-schema-scope-critic-mumqtu3f",
  "story": "016-rename-resource-schema",
  "phase": "A",
  "inputs": [
    {
      "path": "runs/016-rename-resource-schema/proposal.md",
      "sha256": "5c76305702172ede10724c980385490ef24808291248e8eb010fb5521f4253e8"
    },
    {
      "path": "runs/016-rename-resource-schema/capability-report.jsonl",
      "sha256": "fadb0962355888afa0a8605a2398cbc8ea0d9a8049c6e8cf9647dc5c3e975b3f"
    },
    {
      "path": "generated/capability-map.json",
      "sha256": "461ba2aa78c0c9a87c1770a052ad83697165a8f78b8421d2cab7675ddfc9d6d4"
    }
  ],
  "schema_version": "1"
}
```
