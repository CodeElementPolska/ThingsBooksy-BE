Phase **C5 — review round 3, security and authorization** for story `015-resource-time-buffer`.

You review the code diff in `diff.patch` (scope in `scope.json`) for the categories the owner always decides personally (D-1: authorization and security, data loss and destructive changes) plus classic security defects. Read the surrounding code to verify — the diff is your scope, the code is your evidence.

Checklist (report only what you can prove with quoted code):
- **Authorization** (`AUTHZ`): every command/query touching a group's data checks that the requester is allowed (owner / member as the AC says); the requester identity comes from the token, never from the request body; IDs from the URL are checked against the requester's scope (no IDOR across groups, schemas, instances); role clauses in AC (`role: owner`) are enforced. `rule_ref` = `AC-n` when the AC states the role, otherwise `CWE-862` (missing authz), `CWE-639` (IDOR), `CWE-285`. Severity `BLOCKER`.
- **Data safety** (`DATA_SAFETY`): soft-delete semantics kept (no hard delete of rows the spec keeps), bulk updates (`ExecuteUpdate/DeleteAsync`) scoped by the right predicates, global query filters not bypassed on write paths, cascades that lose data, events published before the row is persisted, migrations that drop or narrow columns. `rule_ref` = `AC-n`, `constitution#VI` or `CWE-n`. Severity `BLOCKER` when data can be lost or leaked, `MAJOR` otherwise.
- **Input handling** (`SECURITY`): raw SQL / interpolated queries (`CWE-89`), unbounded collections or strings, missing validation on new request fields, secrets or PII in events, logs or error messages (`CWE-532`, `CWE-209`). Severity by impact.
- **Event payloads** (`SECURITY`/`DATA_SAFETY`): cross-module events carry only identifiers and non-sensitive data; consumers cannot be tricked into acting on another group's data.

Rules:
- Every finding needs `rule_ref` (`CWE-n`, `constitution#<article>`, `AC-n`) and `evidence` with `file:line`. No `rule_ref` ⇒ `OPINION`.
- Not your job: style, naming, spec completeness beyond authorization/data clauses — other reviewers own those.
- Round ≥ 2: review only the fix diff; fill `previous_findings_status` for each of your previous findings; findings outside the fix diff only as `BLOCKER` with `out_of_diff: true`.
- `id` = `review-security-authz-3-<n>`; `reviewer` = `review-security-authz`; `round` = 3; `scope.diff_sha` = the `tree` value from `scope.json`; `scope.files` = files examined.
- An empty `findings` array is a valid result. Fill `summary`.

Return the `findings` JSON and nothing else.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/review/round-3/diff.patch`  (sha256 d477262bd13a…)
- `runs/015-resource-time-buffer/review/round-3/scope.json`  (sha256 c3d93acc67d2…)
- `runs/015-resource-time-buffer/story.md`  (sha256 9212e4b66dae…)
- `specs/015-resource-time-buffer/spec.md`  (sha256 dbdfe2408791…)
- `runs/015-resource-time-buffer/contract-next.json`  (sha256 433a70127610…)
- `runs/015-resource-time-buffer/review/round-2/review-security-authz.findings.json`  (sha256 0f8933804ced…)

## Conventions that apply to your output
- `.specify/memory/constitution.md`

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
  "author_agent": "review-security-authz",
  "run_id": "015-resource-time-buffer-review-security-authz-r3-mulhg9k7",
  "story": "015-resource-time-buffer",
  "phase": "C5",
  "inputs": [
    {
      "path": "runs/015-resource-time-buffer/review/round-3/diff.patch",
      "sha256": "d477262bd13a4aafa8fb3db5691ff13c4973cffd57c0f8e0ee51890a4551c94b"
    },
    {
      "path": "runs/015-resource-time-buffer/review/round-3/scope.json",
      "sha256": "c3d93acc67d287ea3282722c5a1601d0d5ec3cbb0516a3915eb67b471787c5ae"
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
      "path": "runs/015-resource-time-buffer/contract-next.json",
      "sha256": "433a701276108c541bac93297e953a39cbd80181bfb17675616e2211325b5414"
    },
    {
      "path": "runs/015-resource-time-buffer/review/round-2/review-security-authz.findings.json",
      "sha256": "0f8933804ced8bc3b7a96d79ab341666d816049948261bad61f0903e3b673a83"
    }
  ],
  "schema_version": "1"
}
```
