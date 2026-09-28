Phase **C3a — skeleton** for story `015-resource-time-buffer`, module `Resources`.

Implement ONLY the skeleton tasks of `tasks.md` (the "Foundational"/skeleton phase) that are within the skeleton scope:
- new or changed **entities and read models** (properties with private setters, `Create` factory taking the command record, no domain methods with business logic),
- **EF configuration** and `DbSet`s,
- **command/query/result records** (data only, no handlers),
- **event / IModuleClient contract records** in `Shared.Abstractions` when the plan defines them.

Explicitly NOT in this phase, even if `tasks.md` lists them under the same heading: handlers, endpoints, event handlers, domain methods that change state, anything in test projects (factories, test clients, recording brokers — those belong to the test designer in the next phase), and migrations (`dotnet ef migrations add` is run by the developer; list the migration task under `tasks_remaining` with note `developer-only`).

Steps:
1. Read the inputs; list the task ids you will do and the ones you will leave (with the reason) — this list goes into the result.
2. Implement; keep each file in the module's existing style.
3. `dotnet build backend/ThingsBooksy.slnx` must be green.
4. `node tools/fleet/skeleton-check.js --story 015-resource-time-buffer` must report 0 violations — if it reports one, fix it or move the task to `tasks_remaining` with the reason.
5. Return the `result` JSON (`schema_changes`: `ADDITIVE` if you added/changed columns or indexes, `DESTRUCTIVE` if a column/table is dropped or narrowed, else `NONE`).

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `specs/015-resource-time-buffer/spec.md`  (sha256 3128d40f0e49…)
- `specs/015-resource-time-buffer/plan.md`  (sha256 f1a8fda23bfd…)
- `specs/015-resource-time-buffer/tasks.md`  (sha256 0f84641ef1f4…)
- `specs/015-resource-time-buffer/data-model.md`  (sha256 96ef74e8e68e…)
- `specs/015-resource-time-buffer/contracts/events.md`  (sha256 423a3078c745…)
- `runs/015-resource-time-buffer/story.md`  (sha256 f483a23dd3d1…)
- `runs/015-resource-time-buffer/contract-next.json`  (sha256 523298c62382…)
- `runs/015-resource-time-buffer/discovery/decisions.jsonl`  (sha256 23901ee7f561…)
- `runs/015-resource-time-buffer/discovery/assumptions.jsonl`  (sha256 7027b12d714b…)

## Conventions that apply to your output
- `.specify/memory/constitution.md`
- `.claude/conventions/domain-entity-design.md`
- `.claude/conventions/ef-schema-isolation.md`
- `.claude/conventions/naming-commands-queries-handlers-results.md`

## Your output MUST validate against this JSON Schema
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "fleet-v4/result",
  "title": "result.json — output of every writer (be-writer, fe-writer, test-designer). The conductor script branches on `status`.",
  "type": "object",
  "required": [
    "provenance",
    "status",
    "tasks_completed",
    "files_changed",
    "assumptions",
    "decisions_needed"
  ],
  "properties": {
    "provenance": {
      "$ref": "fleet-v4/provenance"
    },
    "status": {
      "type": "string",
      "enum": [
        "DONE",
        "BLOCKED_ON_DECISION",
        "DISPUTE",
        "DISPUTE_TEST",
        "FAILED"
      ],
      "description": "DONE = all assigned tasks done and local checks green. BLOCKED_ON_DECISION = cannot proceed without an owner decision (decisions_needed non-empty). DISPUTE = disagrees with a review finding (dispute filled). DISPUTE_TEST = an acceptance test contradicts the spec (dispute filled, test_id set). FAILED = could not complete (error)."
    },
    "tasks_completed": {
      "type": "array",
      "items": {
        "type": "string",
        "pattern": "^T[0-9]+$"
      },
      "description": "script ticks these in tasks.md after the gate is green"
    },
    "tasks_remaining": {
      "type": "array",
      "items": {
        "type": "string",
        "pattern": "^T[0-9]+$"
      }
    },
    "files_changed": {
      "type": "array",
      "items": {
        "type": "string"
      },
      "description": "must all be inside the agent's write-allow roots — verified by script against git diff"
    },
    "assumptions": {
      "type": "array",
      "items": {
        "$ref": "fleet-v4/assumption"
      },
      "description": "every default the writer chose; hard_list=true here fails DoD"
    },
    "decisions_needed": {
      "type": "array",
      "items": {
        "$ref": "fleet-v4/decision"
      },
      "description": "non-empty ⇒ status must be BLOCKED_ON_DECISION"
    },
    "dispute": {
      "type": "object",
      "required": [
        "target_id",
        "argument",
        "rule_ref"
      ],
      "properties": {
        "target_id": {
          "type": "string",
          "description": "finding id (DISPUTE) or test id (DISPUTE_TEST)"
        },
        "argument": {
          "type": "string"
        },
        "rule_ref": {
          "type": "string",
          "description": "AC-id / convention file#section / constitution article the writer relies on"
        },
        "evidence": {
          "type": "string"
        }
      }
    },
    "local_checks": {
      "type": "object",
      "properties": {
        "build": {
          "type": "string",
          "enum": [
            "PASSED",
            "FAILED",
            "SKIPPED"
          ]
        },
        "unit_tests": {
          "type": "string",
          "enum": [
            "PASSED",
            "FAILED",
            "SKIPPED"
          ]
        },
        "format": {
          "type": "string",
          "enum": [
            "PASSED",
            "FAILED",
            "SKIPPED"
          ]
        }
      }
    },
    "schema_changes": {
      "type": "string",
      "enum": [
        "NONE",
        "ADDITIVE",
        "DESTRUCTIVE"
      ],
      "description": "be-writer in C3a; DESTRUCTIVE ⇒ conductor opens G2b"
    },
    "error": {
      "type": "string"
    }
  },
  "allOf": [
    {
      "if": {
        "properties": {
          "status": {
            "const": "BLOCKED_ON_DECISION"
          }
        }
      },
      "then": {
        "properties": {
          "decisions_needed": {
            "minItems": 1
          }
        }
      }
    },
    {
      "if": {
        "properties": {
          "status": {
            "enum": [
              "DISPUTE",
              "DISPUTE_TEST"
            ]
          }
        }
      },
      "then": {
        "required": [
          "dispute"
        ]
      }
    },
    {
      "if": {
        "properties": {
          "status": {
            "const": "FAILED"
          }
        }
      },
      "then": {
        "required": [
          "error"
        ]
      }
    }
  ]
}
```

## Provenance block — copy VERBATIM into the `provenance` field of your output
```json
{
  "author_agent": "be-writer",
  "run_id": "015-resource-time-buffer-be-writer-mul37skr",
  "story": "015-resource-time-buffer",
  "phase": "C3a",
  "inputs": [
    {
      "path": "specs/015-resource-time-buffer/spec.md",
      "sha256": "3128d40f0e49961d583fe4245b45373c3abdbec496d78be3f0a4aea926b5d929"
    },
    {
      "path": "specs/015-resource-time-buffer/plan.md",
      "sha256": "f1a8fda23bfda2f7bb3cb70126817a699134f7528f4ae1b0bb32b47f7a889eb1"
    },
    {
      "path": "specs/015-resource-time-buffer/tasks.md",
      "sha256": "0f84641ef1f48f5889f7899dbc711749b143646847bb41242da72a548d6371f8"
    },
    {
      "path": "specs/015-resource-time-buffer/data-model.md",
      "sha256": "96ef74e8e68ee64f1195f957466f0d369f5d6b1624b221ac5b87efcf066a1d4e"
    },
    {
      "path": "specs/015-resource-time-buffer/contracts/events.md",
      "sha256": "423a3078c745db5b614b081a436241c6330e583e69528a11ea827cec39fe8aa9"
    },
    {
      "path": "runs/015-resource-time-buffer/story.md",
      "sha256": "f483a23dd3d1ab3df94ed1fa975058a1f33c6f1d059eda79e755f9587695c486"
    },
    {
      "path": "runs/015-resource-time-buffer/contract-next.json",
      "sha256": "523298c62382bc0d61e92ddbb99110e77564ee5a180e6809f85715f9ab9410d2"
    },
    {
      "path": "runs/015-resource-time-buffer/discovery/decisions.jsonl",
      "sha256": "23901ee7f561c5816a602c49343bdcfdc99e2f3df54566ccb54b5fd12eb8235c"
    },
    {
      "path": "runs/015-resource-time-buffer/discovery/assumptions.jsonl",
      "sha256": "7027b12d714b98019a997653e2222e4e65aa913a7676c2e17ef62f6e6514b8dc"
    }
  ],
  "schema_version": "1"
}
```
