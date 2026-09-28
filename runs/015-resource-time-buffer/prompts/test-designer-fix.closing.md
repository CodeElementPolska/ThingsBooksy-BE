Phase **C5 — test fixes after review round 4** for story `015-resource-time-buffer`, module `Resources`.

`dedup.json` is the merged review. Work only on the findings with `route: "tester"` (they concern test code): fix each `BLOCKER` and `MAJOR`; fix a `MINOR` when the change is local and cheap, otherwise say why not in `notes`; ignore the rest.

Rules:
- The blind-pass acceptance tests are read-only: the files listed in `red-first.json` → `acceptance_test_files` must not change (their hash is checked by the gate). A finding that could only be satisfied by editing one of them is a `DISPUTE_TEST` with the finding id as `dispute.target_id`, or, if you agree with the finding, a `status: BLOCKED_ON_DECISION` asking the owner to authorise a REBASELINE.
- A fix changes only what the finding names; it must not weaken any assertion or drop an AC tag. Shared infrastructure (factories, test client) may be extended in the convention's shape (`Clients/{Entity}Factory.cs`, DB reads on the TestClient with `IgnoreQueryFilters()`).
- To dispute a finding return `status: DISPUTE` with `dispute.target_id`, your `argument`, the `rule_ref` you rely on and `evidence` with `file:line`. Fix everything else first.

Then `dotnet build` and `dotnet test backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests` — all green, no regression. Report in `local_checks`.

Return the `result` JSON with `files_changed`, `assumptions`, and `notes.fixed[]` = the finding ids you fixed.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/closing/dedup.json`  (sha256 02261bab7b2b…)
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
  "author_agent": "test-designer-sighted",
  "run_id": "015-resource-time-buffer-test-designer-sighted-closing-mulirhor",
  "story": "015-resource-time-buffer",
  "phase": "C5",
  "inputs": [
    {
      "path": "runs/015-resource-time-buffer/closing/dedup.json",
      "sha256": "02261bab7b2bb5e9e04e7009e04fa7997cf61be9cd461a1ecd5d3bea8a77e852"
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
