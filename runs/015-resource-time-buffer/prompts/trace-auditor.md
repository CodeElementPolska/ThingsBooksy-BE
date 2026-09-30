Phase **C6 — trace audit** for story `015-resource-time-buffer`.

For every `AC-n` in `story.md` (front matter) take its tests from `ac-matrix.json`, read those test methods (you may read every test project and shared test infrastructure; never production source) and decide whether the assertions prove each clause of the AC's `then`. The reviewer's round-1 findings are given so you do not repeat a weakness the owner already closed by decision (`decision_id` present) — reference it instead.

Output (the `findings` schema plus one extra top-level field):
- `ac_assessment`: one entry per AC — `{ "ac": "AC-n", "tests": [...method names...], "verdict": "STRONG" | "WEAK" | "MISSING", "clauses_unproven": [...], "note": "..." }`.
- `findings`: one `WEAK_ASSERTION` per unproven clause: `rule_ref` = `AC-n`, `severity` `MAJOR` (clause has no assertion) or `MINOR` (asserted only indirectly), `evidence` = the test's assertion lines with `file:line`, `suggested_fix` = the assertion to add, in words. `id` = `trace-auditor-1-<n>`; `reviewer` = `trace-auditor`; `round` = 1; `scope.files` = the test files read; `scope.diff_sha` may be omitted.
- Fill `summary`. Every AC must appear in `ac_assessment` — an AC left out is a failed audit.

Return the `findings` JSON and nothing else.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/story.md`  (sha256 9212e4b66dae…)
- `specs/015-resource-time-buffer/spec.md`  (sha256 dbdfe2408791…)
- `runs/015-resource-time-buffer/ac-matrix.json`  (sha256 f048c9fe2958…)
- `runs/015-resource-time-buffer/review/round-1/review-spec-conformance.findings.json`  (sha256 b9dbba7f5cb1…)

## Conventions that apply to your output
- `.claude/conventions/integration-test-infrastructure.md`
- `.claude/conventions/integration-test-naming.md`

## Your output MUST validate against this JSON Schema
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "fleet-v4/findings",
  "title": "findings.json — output of every reviewer and guard (spec-conformance, security-authz, maintainability, plan-guard, architecture-guard, trace-auditor)",
  "type": "object",
  "required": [
    "provenance",
    "reviewer",
    "round",
    "scope",
    "findings"
  ],
  "properties": {
    "provenance": {
      "$ref": "fleet-v4/provenance"
    },
    "reviewer": {
      "type": "string"
    },
    "round": {
      "type": "integer",
      "minimum": 1,
      "description": "round ≥ 2 ⇒ scope must be the fix diff + previous findings only"
    },
    "scope": {
      "type": "object",
      "properties": {
        "diff_sha": {
          "type": "string"
        },
        "files": {
          "type": "array",
          "items": {
            "type": "string"
          }
        }
      }
    },
    "previous_findings_status": {
      "type": "array",
      "description": "round ≥ 2: verdict on each finding from the previous round",
      "items": {
        "type": "object",
        "required": [
          "id",
          "status"
        ],
        "properties": {
          "id": {
            "type": "string"
          },
          "status": {
            "type": "string",
            "enum": [
              "RESOLVED",
              "STILL_OPEN",
              "REGRESSED"
            ]
          }
        }
      }
    },
    "findings": {
      "type": "array",
      "items": {
        "type": "object",
        "required": [
          "id",
          "file",
          "line",
          "severity",
          "type",
          "message",
          "evidence"
        ],
        "properties": {
          "id": {
            "type": "string",
            "description": "<reviewer>-<round>-<n>"
          },
          "file": {
            "type": "string"
          },
          "line": {
            "type": "integer",
            "minimum": 1
          },
          "rule_ref": {
            "type": "string",
            "description": "AC-<n> | conventions/<file>#<section> | constitution#<article> | CWE-<n> | contract:<path> — REQUIRED for any severity above OPINION"
          },
          "severity": {
            "type": "string",
            "enum": [
              "BLOCKER",
              "MAJOR",
              "MINOR",
              "OPINION"
            ],
            "description": "BLOCKER: violates an AC, a constitution article, the contract, or a D-1 category (authz/data/UI) — blocks the gate. MAJOR: violates a convention with rule_ref, or UNSPECIFIED_BEHAVIOR — must be fixed or disputed before close. MINOR: rule_ref present, cosmetic/maintainability — fixed if cheap, else tech-debt issue. OPINION: no rule_ref — never blocks; shown to the owner in one batch."
          },
          "type": {
            "type": "string",
            "enum": [
              "SPEC_VIOLATION",
              "UNSPECIFIED_BEHAVIOR",
              "CONVENTION",
              "SECURITY",
              "AUTHZ",
              "DATA_SAFETY",
              "ARCHITECTURE",
              "CONTRACT_DRIFT",
              "WEAK_ASSERTION",
              "MAINTAINABILITY",
              "PERFORMANCE"
            ],
            "description": "UNSPECIFIED_BEHAVIOR = behaviour in code with no AC (mandatory type for spec-conformance reviewer); WEAK_ASSERTION = trace-auditor"
          },
          "message": {
            "type": "string"
          },
          "evidence": {
            "type": "string",
            "description": "quoted code / observed behaviour; opinions are not evidence"
          },
          "suggested_fix": {
            "type": "string"
          },
          "rule_candidate": {
            "type": "object",
            "description": "if this finding can be generalised into a deterministic check",
            "required": [
              "kind",
              "description"
            ],
            "properties": {
              "kind": {
                "type": "string",
                "enum": [
                  "roslyn-analyzer",
                  "arch-test",
                  "eslint",
                  "script"
                ]
              },
              "description": {
                "type": "string"
              }
            }
          },
          "out_of_diff": {
            "type": "boolean",
            "description": "round ≥ 2: finding outside the fix diff — allowed only as BLOCKER (constitution/security), otherwise goes to tech-debt"
          }
        },
        "allOf": [
          {
            "if": {
              "properties": {
                "severity": {
                  "enum": [
                    "BLOCKER",
                    "MAJOR",
                    "MINOR"
                  ]
                }
              }
            },
            "then": {
              "required": [
                "rule_ref"
              ]
            }
          }
        ]
      }
    },
    "summary": {
      "type": "object",
      "properties": {
        "blockers": {
          "type": "integer"
        },
        "majors": {
          "type": "integer"
        },
        "minors": {
          "type": "integer"
        },
        "opinions": {
          "type": "integer"
        }
      }
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
  "author_agent": "trace-auditor",
  "run_id": "015-resource-time-buffer-trace-auditor-mulhqbob",
  "story": "015-resource-time-buffer",
  "phase": "C6",
  "inputs": [
    {
      "path": "runs/015-resource-time-buffer/story.md",
      "sha256": "9212e4b66dae0a890f38c3653c2c4a06771aa7d4c12c3c14a06da84604f56490"
    },
    {
      "path": "specs/015-resource-time-buffer/spec.md",
      "sha256": "dbdfe2408791cff4604b73dee20dba490b69a674be806a0ed09ff7cba9f21a66"
    },
    {
      "path": "runs/015-resource-time-buffer/ac-matrix.json",
      "sha256": "f048c9fe2958727d2163c8a16b591e5a92f269c83c73680eb87b52c9fa3971ab"
    },
    {
      "path": "runs/015-resource-time-buffer/review/round-1/review-spec-conformance.findings.json",
      "sha256": "b9dbba7f5cb181ed048a6163105f9c1ac2d3fd924042fa2b986c76ebfb50f99a"
    }
  ],
  "schema_version": "1"
}
```
