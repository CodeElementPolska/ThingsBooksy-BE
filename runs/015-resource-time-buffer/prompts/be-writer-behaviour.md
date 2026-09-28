Phase **C3b — behaviour** for story `015-resource-time-buffer`, module `Resources`.

The acceptance tests listed in `runs/015-resource-time-buffer/tests/red-first.json` are RED. Make them GREEN by implementing the behaviour tasks of `tasks.md` (user-story phases): handlers, endpoints, domain methods, event publishing, plus unit tests for domain logic where a `Tests.Unit` project exists.

Rules of this phase:
- The acceptance tests are read-only for you (`backend/src/Modules/*/…IntegrationTests`, `frontend/**/*.spec.ts`). Read them to understand the expected behaviour; run them with `dotnet test <module IntegrationTests project>`; never edit them. A test you believe is wrong → `status: DISPUTE_TEST` with evidence, do not work around it.
- Do not add behaviour the spec does not ask for. If a branch is needed that no AC covers (e.g. an error path), implement the minimal safe version and record it in `assumptions` with `visible_effect: true` so the reviewer sees it.
- Constitution and conventions in your prompt are binding; keep `SaveChangesAsync` before `PublishAsync`; keep handler constructors ≤ 4 params; DataProvider per handler.
- Do not touch migrations. If the model must change again, stop and return `status: BLOCKED_ON_DECISION` (a second migration is not allowed; the developer regenerates the single one).

Steps:
1. Read the inputs, the red tests and the current module code for the touched features.
2. Implement task by task; after each task `dotnet build`.
3. `dotnet test <module IntegrationTests>` — all story tests (AC filter) green and no pre-existing test regressed; `dotnet format backend/ThingsBooksy.slnx` at the end.
4. Return the `result` JSON with `tasks_completed`, `files_changed`, `assumptions`, `local_checks`.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `specs/015-resource-time-buffer/spec.md`  (sha256 ad2fb57f8010…)
- `specs/015-resource-time-buffer/plan.md`  (sha256 a78fdf965a25…)
- `specs/015-resource-time-buffer/tasks.md`  (sha256 d79bef94c03e…)
- `specs/015-resource-time-buffer/data-model.md`  (sha256 7911cdc82b6d…)
- `specs/015-resource-time-buffer/contracts/events.md`  (sha256 749a5a0c703a…)
- `runs/015-resource-time-buffer/story.md`  (sha256 c9591ad203cf…)
- `runs/015-resource-time-buffer/contract-next.json`  (sha256 433a70127610…)
- `runs/015-resource-time-buffer/discovery/decisions.jsonl`  (sha256 c9072451caf0…)
- `runs/015-resource-time-buffer/discovery/assumptions.jsonl`  (sha256 9a218b8b2a8f…)
- `runs/015-resource-time-buffer/tests/red-first.json`  (sha256 d01984252970…)
- `runs/015-resource-time-buffer/ac-matrix.json`  (sha256 3d76f7d1225f…)

## Conventions that apply to your output
- `.specify/memory/constitution.md`
- `.claude/conventions/domain-entity-design.md`
- `.claude/conventions/data-provider-pattern.md`
- `.claude/conventions/data-provider-query-syntax.md`
- `.claude/conventions/command-construction-in-endpoints.md`
- `.claude/conventions/minimal-api-endpoints.md`
- `.claude/conventions/dispatcher-usage.md`
- `.claude/conventions/naming-commands-queries-handlers-results.md`
- `.claude/conventions/internals-visible-to.md`

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
  "run_id": "015-resource-time-buffer-be-writer-muld2tdk",
  "story": "015-resource-time-buffer",
  "phase": "C3b",
  "inputs": [
    {
      "path": "specs/015-resource-time-buffer/spec.md",
      "sha256": "ad2fb57f801052932cf738cf37f52e5519a7dad05a7d32dee4a7f11b0337f93b"
    },
    {
      "path": "specs/015-resource-time-buffer/plan.md",
      "sha256": "a78fdf965a2523c2b0efafe30efba2b1294db10eb117de100fefd3db1b3bd4ce"
    },
    {
      "path": "specs/015-resource-time-buffer/tasks.md",
      "sha256": "d79bef94c03ec11579fdad6cbecb2ab1224f4dc07c5c6ad3baea5cdde8b81484"
    },
    {
      "path": "specs/015-resource-time-buffer/data-model.md",
      "sha256": "7911cdc82b6d4b95bda4fc132a5eef4a32c63be5053ceae2479cbef7af4e246e"
    },
    {
      "path": "specs/015-resource-time-buffer/contracts/events.md",
      "sha256": "749a5a0c703a93c19c312ab04401dee39e4d05f86234fec26f5a0bd0c20a6ddc"
    },
    {
      "path": "runs/015-resource-time-buffer/story.md",
      "sha256": "c9591ad203cf439589ed0abf7a2578d5cc3336eb89a91a38e8a67f82a2970d19"
    },
    {
      "path": "runs/015-resource-time-buffer/contract-next.json",
      "sha256": "433a701276108c541bac93297e953a39cbd80181bfb17675616e2211325b5414"
    },
    {
      "path": "runs/015-resource-time-buffer/discovery/decisions.jsonl",
      "sha256": "c9072451caf0fefa64935f24b61c9bb1912fe3164623f480b5935bcc09d91a8c"
    },
    {
      "path": "runs/015-resource-time-buffer/discovery/assumptions.jsonl",
      "sha256": "9a218b8b2a8f9a081de0704515385ba75642904cb8b24f736ffd1273c21e4891"
    },
    {
      "path": "runs/015-resource-time-buffer/tests/red-first.json",
      "sha256": "d019842529702d9c9d8c50f34a301855986dfd89484bad196a3415c66ae73d10"
    },
    {
      "path": "runs/015-resource-time-buffer/ac-matrix.json",
      "sha256": "3d76f7d1225fa4660ebc6f4b8e49ccfd4a4a573ecbca285222f624f5d3ac8155"
    }
  ],
  "schema_version": "1"
}
```
