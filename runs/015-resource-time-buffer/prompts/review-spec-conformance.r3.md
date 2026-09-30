Phase **C5 — review round 3, spec conformance** for story `015-resource-time-buffer`.

You review the code diff in `diff.patch` (scope in `scope.json`) against the story's acceptance criteria (`story.md` front matter and the `**AC-n**` scenarios in `spec.md`), the owner's decisions (`decisions.jsonl`) and the HTTP contract (`contract-next.json`, when present). One question only: **does the code do exactly what the AC say — no less, no more?**

What to report (each as a finding with `evidence` = quoted code with `file:line`):
- `SPEC_VIOLATION` — an AC, decision or contract clause is not met, or met differently (wrong status code, wrong payload, wrong message, wrong order of persist/publish). `rule_ref` = `AC-n`, `DEC-n` or `contract:<path>`. Severity `BLOCKER`.
- `UNSPECIFIED_BEHAVIOR` — behaviour in the diff that no AC or decision asks for (extra branch, extra side effect, defaulted rule). This type is **mandatory**: every such branch is a finding, even if it looks harmless. `rule_ref` = `story:acceptance_criteria` (the AC list is the rule violated by omission) or the nearest `AC-n`. Severity `MAJOR`. A test tagged `UNSPECIFIED` covering it does not close the finding — say which test covers it in `message`.
- `CONTRACT_DRIFT` — a route, request or response shape differs from `contract-next.json`. `rule_ref` = `contract:<path>`. Severity `BLOCKER`.
- `WEAK_ASSERTION` only when an acceptance test in the diff would pass while its AC is violated (state exactly how). `rule_ref` = `AC-n`. Severity `MAJOR`.

Rules:
- Verify every claim by reading the code (you may read the whole backend and frontend source; the diff is your scope, the code is your evidence). Never report from the diff hunk alone if the surrounding code changes the meaning.
- Not your job: naming, style, conventions, security — other reviewers own those. Do not mention them.
- No `rule_ref` ⇒ `severity: OPINION`. Opinions are allowed but never block; keep them few.
- Round ≥ 2: review only the fix diff; fill `previous_findings_status` for **each** of your previous findings (`RESOLVED` / `STILL_OPEN` / `REGRESSED`); a finding outside the fix diff is allowed only as `BLOCKER` with `out_of_diff: true`.
- `id` = `review-spec-conformance-3-<n>`; `reviewer` = `review-spec-conformance`; `round` = 3; `scope.diff_sha` = the `tree` value from `scope.json`; `scope.files` = the files you actually examined.
- Fill `summary` counts. If there is nothing to report, return an empty `findings` array — that is a valid, welcome result.

Return the `findings` JSON and nothing else.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/review/round-3/diff.patch`  (sha256 d477262bd13a…)
- `runs/015-resource-time-buffer/review/round-3/scope.json`  (sha256 c3d93acc67d2…)
- `runs/015-resource-time-buffer/story.md`  (sha256 9212e4b66dae…)
- `specs/015-resource-time-buffer/spec.md`  (sha256 dbdfe2408791…)
- `runs/015-resource-time-buffer/contract-next.json`  (sha256 433a70127610…)
- `runs/015-resource-time-buffer/ac-matrix.json`  (sha256 f048c9fe2958…)
- `runs/015-resource-time-buffer/discovery/decisions.jsonl`  (sha256 b1185a95ddba…)
- `runs/015-resource-time-buffer/review/round-2/review-spec-conformance.findings.json`  (sha256 8660a998e166…)

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
  "author_agent": "review-spec-conformance",
  "run_id": "015-resource-time-buffer-review-spec-conformance-r3-mulhg9hc",
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
      "path": "runs/015-resource-time-buffer/ac-matrix.json",
      "sha256": "f048c9fe2958727d2163c8a16b591e5a92f269c83c73680eb87b52c9fa3971ab"
    },
    {
      "path": "runs/015-resource-time-buffer/discovery/decisions.jsonl",
      "sha256": "b1185a95ddbae5292e15a6cd300130f5f77ea799a1fbc79ef5a6dd9ca05fbb22"
    },
    {
      "path": "runs/015-resource-time-buffer/review/round-2/review-spec-conformance.findings.json",
      "sha256": "8660a998e1668b8613cb579b911a739d6eddc112608e13ce9706b77adc9bfa35"
    }
  ],
  "schema_version": "1"
}
```
