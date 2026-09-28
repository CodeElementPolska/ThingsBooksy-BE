Phase **C5 — test fixes after review round 2** for story `015-resource-time-buffer`, module `Resources`.

`dedup.json` is the merged review. Work only on the findings with `route: "tester"` (they concern test code): fix each `BLOCKER` and `MAJOR`; fix a `MINOR` when the change is local and cheap, otherwise say why not in `notes`; ignore the rest.

Rules:
- The blind-pass acceptance tests are read-only: the files listed in `red-first.json` → `acceptance_test_files` must not change (their hash is checked by the gate). A finding that could only be satisfied by editing one of them is a `DISPUTE_TEST` with the finding id as `dispute.target_id`, or, if you agree with the finding, a `status: BLOCKED_ON_DECISION` asking the owner to authorise a REBASELINE.
- A fix changes only what the finding names; it must not weaken any assertion or drop an AC tag. Shared infrastructure (factories, test client) may be extended in the convention's shape (`Clients/{Entity}Factory.cs`, DB reads on the TestClient with `IgnoreQueryFilters()`).
- To dispute a finding return `status: DISPUTE` with `dispute.target_id`, your `argument`, the `rule_ref` you rely on and `evidence` with `file:line`. Fix everything else first.

Then `dotnet build` and `dotnet test backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests` — all green, no regression. Report in `local_checks`.

Return the `result` JSON with `files_changed`, `assumptions`, and `notes.fixed[]` = the finding ids you fixed.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/review/round-2/dedup.json`  (sha256 fb96a22b39c9…)
- `runs/015-resource-time-buffer/story.md`  (sha256 9212e4b66dae…)
- `specs/015-resource-time-buffer/spec.md`  (sha256 dbdfe2408791…)
- `runs/015-resource-time-buffer/tests/red-first.json`  (sha256 e84a95c389f6…)

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
  "run_id": "015-resource-time-buffer-test-designer-sighted-r2-mulhaczg",
  "story": "015-resource-time-buffer",
  "phase": "C5",
  "inputs": [
    {
      "path": "runs/015-resource-time-buffer/review/round-2/dedup.json",
      "sha256": "fb96a22b39c9680eb40f15da91e6538d714fbe28835330d4d2eacfd63976fa9c"
    },
    {
      "path": "runs/015-resource-time-buffer/story.md",
      "sha256": "9212e4b66dae0a890f38c3653c2c4a06771aa7d4c12c3c14a06da84604f56490"
    },
    {
      "path": "specs/015-resource-time-buffer/spec.md",
      "sha256": "dbdfe2408791cff4604b73dee20dba490b69a674be806a0ed09ff7cba9f21a66"
    },
    {
      "path": "runs/015-resource-time-buffer/tests/red-first.json",
      "sha256": "e84a95c389f692497cff32b98f474d5ac67d4bb1e0a8b9cfb11a7b0e49ca0222"
    }
  ],
  "schema_version": "1"
}
```
