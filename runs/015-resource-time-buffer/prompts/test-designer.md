Phase **C2 — acceptance tests (blind pass)** for story `015-resource-time-buffer`, module `Resources`.

The skeleton exists (entities, EF configuration, contract records) but NO behaviour: the tests you write must compile and must FAIL. Write the acceptance tests for every `AC-n` of the story (see `story.md` front matter and the `**AC-n**` scenarios in `spec.md`), plus the test infrastructure they need.

Scope of this phase (take the matching tasks from `tasks.md` regardless of the phase heading they sit under):
- test infrastructure: factories for new entities (use `Create(...)` signatures from `generated/core-surface.json`), test-client methods for the endpoints in the contract, shared helpers such as a recording `IMessageBroker` decorator registered in `ThingsBooksyWebAppFactory`;
- one or more test classes per user story; each test tagged `[Trait("AC", "AC-n")]`; existing tests whose expectations the story changes are rewritten (with the AC tag) — say which in `notes`.

Rules:
- Arrange via EF + factories, Act via HTTP, Assert via EF re-read (`IgnoreQueryFilters()`) and recorded events. Never seed through the API.
- Assert exact user-visible messages when the AC spells them out; assert status codes; assert DB state after the act.
- Every test must fail for the right reason. Build the test projects: `dotnet build backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests` (and `backend/src/Shared/ThingsBooksy.Shared.IntegrationTests` if you changed it). Then run `dotnet test backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests --filter "AC=AC-3|…"` for your AC ids and confirm they FAIL (report counts in `notes`).
- If an AC cannot be encoded as a test (ambiguous, contradicts the contract or the type surface), return `status: BLOCKED_ON_DECISION` with a D-2 question; do not invent semantics.

Return the `result` JSON: `tasks_completed` (test tasks), `files_changed`, `assumptions` (anything you had to choose — e.g. a factory default), `local_checks.build`.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/story.md`  (sha256 f483a23dd3d1…)
- `specs/015-resource-time-buffer/spec.md`  (sha256 3128d40f0e49…)
- `specs/015-resource-time-buffer/tasks.md`  (sha256 0f84641ef1f4…)
- `runs/015-resource-time-buffer/contract-next.json`  (sha256 523298c62382…)
- `runs/015-resource-time-buffer/discovery/decisions.jsonl`  (sha256 23901ee7f561…)
- `generated/core-surface.json`  (sha256 1c5ca522929d…)
- `generated/capability-map.json`  (sha256 ef9341f9bd76…)

## Conventions that apply to your output
- `.specify/memory/constitution.md`
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
  "author_agent": "test-designer",
  "run_id": "015-resource-time-buffer-test-designer-mul37sn2",
  "story": "015-resource-time-buffer",
  "phase": "C2",
  "inputs": [
    {
      "path": "runs/015-resource-time-buffer/story.md",
      "sha256": "f483a23dd3d1ab3df94ed1fa975058a1f33c6f1d059eda79e755f9587695c486"
    },
    {
      "path": "specs/015-resource-time-buffer/spec.md",
      "sha256": "3128d40f0e49961d583fe4245b45373c3abdbec496d78be3f0a4aea926b5d929"
    },
    {
      "path": "specs/015-resource-time-buffer/tasks.md",
      "sha256": "0f84641ef1f48f5889f7899dbc711749b143646847bb41242da72a548d6371f8"
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
      "path": "generated/core-surface.json",
      "sha256": "1c5ca522929db7f829ba60c35cb9066077118eff86bbd968b36798f8631f2945"
    },
    {
      "path": "generated/capability-map.json",
      "sha256": "ef9341f9bd76df83af914e1bda2ba6c4d20f4cad1698fe971aae1bccb854048c"
    }
  ],
  "schema_version": "1"
}
```
