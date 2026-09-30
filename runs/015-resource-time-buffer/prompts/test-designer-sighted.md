Phase **C4b — second test pass (sighted)** for story `015-resource-time-buffer`, module `Resources`.

`coverage-gaps.json` lists lines and branches of the story's NEW production code that the acceptance tests do not exercise. For each gap: read the code, decide whether it belongs to an AC (`[Trait("AC","AC-n")]`), is unspecified behaviour (`[Trait("AC","UNSPECIFIED")]` + an `assumptions` entry starting with `UNSPECIFIED:`), or is unreachable (explain in `notes`). Write the tests in the story's test files (new methods or new files) — never modify the blind-pass tests (`red-first.json` carries their hash).

Line coverage cannot see behaviour that flows through UNCHANGED branches (e.g. "deleting an already-deleted row must publish nothing"). So also take the test tasks of this phase from `tasks.md` (regardless of the phase heading they sit under) and write every test they name, with the tag they prescribe; if `coverage-gaps.json` has no gaps, those tasks are your whole scope.

Run `dotnet test backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests` before returning; all tests green. Return the `result` JSON.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/story.md`  (sha256 c9591ad203cf…)
- `specs/015-resource-time-buffer/spec.md`  (sha256 ad2fb57f8010…)
- `specs/015-resource-time-buffer/tasks.md`  (sha256 d79bef94c03e…)
- `runs/015-resource-time-buffer/coverage-gaps.json`  (sha256 bf66c6e2b15e…)
- `runs/015-resource-time-buffer/ac-matrix.json`  (sha256 3d76f7d1225f…)
- `runs/015-resource-time-buffer/tests/red-first.json`  (sha256 d01984252970…)

## Conventions that apply to your output
- `.claude/conventions/integration-test-infrastructure.md`
- `.claude/conventions/integration-test-naming.md`

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
  "author_agent": "test-designer-sighted",
  "run_id": "015-resource-time-buffer-test-designer-sighted-muldwued",
  "story": "015-resource-time-buffer",
  "phase": "C4b",
  "inputs": [
    {
      "path": "runs/015-resource-time-buffer/story.md",
      "sha256": "c9591ad203cf439589ed0abf7a2578d5cc3336eb89a91a38e8a67f82a2970d19"
    },
    {
      "path": "specs/015-resource-time-buffer/spec.md",
      "sha256": "ad2fb57f801052932cf738cf37f52e5519a7dad05a7d32dee4a7f11b0337f93b"
    },
    {
      "path": "specs/015-resource-time-buffer/tasks.md",
      "sha256": "d79bef94c03ec11579fdad6cbecb2ab1224f4dc07c5c6ad3baea5cdde8b81484"
    },
    {
      "path": "runs/015-resource-time-buffer/coverage-gaps.json",
      "sha256": "bf66c6e2b15e8012b3e3662e56f859b85b371939d800aab4f6fbf4a5af5e3ceb"
    },
    {
      "path": "runs/015-resource-time-buffer/ac-matrix.json",
      "sha256": "3d76f7d1225fa4660ebc6f4b8e49ccfd4a4a573ecbca285222f624f5d3ac8155"
    },
    {
      "path": "runs/015-resource-time-buffer/tests/red-first.json",
      "sha256": "d019842529702d9c9d8c50f34a301855986dfd89484bad196a3415c66ae73d10"
    }
  ],
  "schema_version": "1"
}
```
