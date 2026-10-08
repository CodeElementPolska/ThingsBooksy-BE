Phase **C3b — behaviour** for story `016-rename-resource-schema`, module `Resources`.

The acceptance tests listed in `runs/016-rename-resource-schema/tests/red-first.json` are RED. Make them GREEN by implementing the behaviour tasks of `tasks.md` (user-story phases): handlers, endpoints, domain methods, event publishing, plus unit tests for domain logic where a `Tests.Unit` project exists.

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
- `specs/016-rename-resource-schema/spec.md`  (sha256 3add460ce38f…)
- `specs/016-rename-resource-schema/plan.md`  (sha256 45c7f2e96ed1…)
- `specs/016-rename-resource-schema/tasks.md`  (sha256 6a364140c211…)
- `specs/016-rename-resource-schema/data-model.md`  (sha256 f48f8fe3b624…)
- `specs/016-rename-resource-schema/contracts/README.md`  (sha256 fae28fc69338…)
- `runs/016-rename-resource-schema/story.md`  (sha256 cfc1c3644793…)
- `runs/016-rename-resource-schema/contract-next.json`  (sha256 d3585a905c51…)
- `runs/016-rename-resource-schema/discovery/decisions.jsonl`  (sha256 8f6666a679b8…)
- `runs/016-rename-resource-schema/discovery/assumptions.jsonl`  (sha256 dc6c49d9c739…)
- `runs/016-rename-resource-schema/tests/red-first.json`  (sha256 c3e4dccf4eb2…)
- `runs/016-rename-resource-schema/ac-matrix.json`  (sha256 27cc5fca5878…)

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

### Referenced schema `fleet-v4/assumption` (items of the fields that `$ref` it must have this shape)
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "fleet-v4/assumption",
  "title": "One entry of assumptions.jsonl — buckets ZAŁÓŻ and NIE PYTAJ (D-2). Everything an agent did NOT ask about.",
  "type": "object",
  "required": [
    "id",
    "made_by",
    "statement",
    "bucket",
    "score"
  ],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^ASM-[A-Z]*[0-9]+$",
      "description": "ASM-<n> in discovery; delivery agents prefix a letter to avoid collisions (ASM-W1 writer, ASM-T1 tester)"
    },
    "made_by": {
      "type": "string"
    },
    "phase": {
      "type": "string"
    },
    "statement": {
      "type": "string",
      "description": "one sentence: what was assumed"
    },
    "bucket": {
      "type": "string",
      "enum": [
        "ASSUME",
        "NO_ASK"
      ],
      "description": "ASSUME = sensible default chosen, owner may veto; NO_ASK = answer exists in code/docs"
    },
    "status": {
      "type": "string",
      "enum": [
        "ACTIVE",
        "WITHDRAWN"
      ],
      "default": "ACTIVE",
      "description": "WITHDRAWN when the story is rescoped and the assumption no longer applies (keep the row: it is history)"
    },
    "withdrawn_reason": {
      "type": "string"
    },
    "score": {
      "type": "object",
      "description": "the rubric answers, visible so the triage can be audited",
      "required": [
        "hard_list",
        "reversible_cheaply",
        "visible_effect",
        "has_recommendation"
      ],
      "properties": {
        "hard_list": {
          "type": "boolean",
          "description": "touches a D-1 category ⇒ MUST have been a decision; DoD grep fails the run if true here"
        },
        "hard_list_category": {
          "type": "string",
          "enum": [
            "authz-security",
            "data-migration",
            "new-ui"
          ]
        },
        "reversible_cheaply": {
          "type": "boolean",
          "description": "single-file change, no migration, no contract change"
        },
        "visible_effect": {
          "type": "boolean",
          "description": "options differ in something a user or another module can observe"
        },
        "has_recommendation": {
          "type": "boolean"
        }
      }
    },
    "default_chosen": {
      "type": "string"
    },
    "rationale": {
      "type": "string"
    },
    "evidence": {
      "type": "string",
      "description": "required when bucket = NO_ASK (file:line or captured response)"
    },
    "consequence_if_wrong": {
      "type": "string"
    },
    "veto": {
      "type": "object",
      "description": "written only by script when the owner overrides",
      "required": [
        "answer_ref",
        "replacement_decision_id"
      ],
      "properties": {
        "answer_ref": {
          "type": "string"
        },
        "replacement_decision_id": {
          "type": "string",
          "pattern": "^DEC-[0-9]+$"
        }
      }
    }
  },
  "allOf": [
    {
      "if": {
        "properties": {
          "bucket": {
            "const": "NO_ASK"
          }
        }
      },
      "then": {
        "required": [
          "evidence"
        ]
      }
    },
    {
      "if": {
        "properties": {
          "bucket": {
            "const": "ASSUME"
          }
        }
      },
      "then": {
        "required": [
          "default_chosen",
          "rationale",
          "consequence_if_wrong"
        ]
      }
    }
  ]
}
```

### Referenced schema `fleet-v4/decision` (items of the fields that `$ref` it must have this shape)
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "fleet-v4/decision",
  "title": "One entry of runs/<story>/discovery/decisions.jsonl — bucket ZAPYTAJ (D-2)",
  "type": "object",
  "required": [
    "id",
    "question",
    "context",
    "options",
    "recommendation",
    "argument_against",
    "consequences",
    "status"
  ],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^DEC-[0-9]+$"
    },
    "asked_by": {
      "type": "string",
      "description": "agent_type that raised it (dev-analyst, be-writer…)"
    },
    "phase": {
      "type": "string"
    },
    "hard_list_category": {
      "type": "string",
      "enum": [
        "authz-security",
        "data-migration",
        "new-ui"
      ],
      "description": "D-1 categories. Present ⇒ the decision can never be defaulted; DoD fails without decided_by=owner"
    },
    "question": {
      "type": "string",
      "description": "one sentence, plain language"
    },
    "context": {
      "type": "string",
      "description": "why this matters, for someone who has not read the code"
    },
    "code_says": {
      "type": "array",
      "description": "what the code/docs already establish, with evidence",
      "items": {
        "type": "object",
        "required": [
          "claim",
          "evidence"
        ],
        "properties": {
          "claim": {
            "type": "string"
          },
          "evidence": {
            "type": "string",
            "description": "file:line or captured response"
          }
        }
      }
    },
    "options": {
      "type": "array",
      "minItems": 2,
      "items": {
        "type": "object",
        "required": [
          "label",
          "consequences"
        ],
        "properties": {
          "label": {
            "type": "string"
          },
          "consequences": {
            "type": "string"
          }
        }
      }
    },
    "recommendation": {
      "type": "string",
      "description": "label of the recommended option + argument"
    },
    "argument_against": {
      "type": "string",
      "description": "the strongest argument AGAINST the recommendation (anti-anchoring, mandatory)"
    },
    "consequences": {
      "type": "string",
      "description": "what changes downstream once decided (spec, contract, tests)"
    },
    "status": {
      "type": "string",
      "enum": [
        "OPEN",
        "DECIDED",
        "WITHDRAWN"
      ]
    },
    "chosen": {
      "type": "string"
    },
    "decided_by": {
      "type": "string",
      "enum": [
        "owner"
      ],
      "description": "ONLY a script may write this field, from owner-answers.jsonl"
    },
    "answer_ref": {
      "type": "string",
      "description": "tool_use_id (AskUserQuestion) or prompt_id (typed answer) in owner-answers.jsonl"
    },
    "decided_at": {
      "type": "string",
      "format": "date-time"
    }
  },
  "allOf": [
    {
      "if": {
        "properties": {
          "status": {
            "const": "DECIDED"
          }
        }
      },
      "then": {
        "required": [
          "chosen",
          "decided_by",
          "answer_ref",
          "decided_at"
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
  "run_id": "016-rename-resource-schema-be-writer-muzawab2",
  "story": "016-rename-resource-schema",
  "phase": "C3b",
  "inputs": [
    {
      "path": "specs/016-rename-resource-schema/spec.md",
      "sha256": "3add460ce38f1e325a5874c4638caa9aa5e2603f6d11d3bf70681c82d163a45a"
    },
    {
      "path": "specs/016-rename-resource-schema/plan.md",
      "sha256": "45c7f2e96ed1f4a2dce05082f1a98a5df7dcd09fa54b8aac845d1b9c65c1bd24"
    },
    {
      "path": "specs/016-rename-resource-schema/tasks.md",
      "sha256": "6a364140c211bab1dd5980c35337bb13040d45bb297ec5e1839113a314a80128"
    },
    {
      "path": "specs/016-rename-resource-schema/data-model.md",
      "sha256": "f48f8fe3b6243ac42f4582609e582e1943178ba810f946468ce94e94c348b297"
    },
    {
      "path": "specs/016-rename-resource-schema/contracts/README.md",
      "sha256": "fae28fc69338ede86eb66190ff0b90192026a5329a985a232a8b6eac81c09b08"
    },
    {
      "path": "runs/016-rename-resource-schema/story.md",
      "sha256": "cfc1c3644793b103ee92fbc9f05d679ed32aa6c642a90d4491654819d2199acb"
    },
    {
      "path": "runs/016-rename-resource-schema/contract-next.json",
      "sha256": "d3585a905c51474546c9a8f8835a92ab1e3d98fef003845c2d66df14102bfac4"
    },
    {
      "path": "runs/016-rename-resource-schema/discovery/decisions.jsonl",
      "sha256": "8f6666a679b8572d4816d6d3577bbea33ee175888065245fb8f1159f334a8d54"
    },
    {
      "path": "runs/016-rename-resource-schema/discovery/assumptions.jsonl",
      "sha256": "dc6c49d9c7395a1f374eeb715b154b3e9c490b17f0155b2835bb11c47d418c57"
    },
    {
      "path": "runs/016-rename-resource-schema/tests/red-first.json",
      "sha256": "c3e4dccf4eb277f705c178c3965e1d21b9a669c73ccd31b9a7541c95669c9694"
    },
    {
      "path": "runs/016-rename-resource-schema/ac-matrix.json",
      "sha256": "27cc5fca58780ec77025b51a47067dab3c50c4feb3e10cdc4764a669e6244a8a"
    }
  ],
  "schema_version": "1"
}
```
