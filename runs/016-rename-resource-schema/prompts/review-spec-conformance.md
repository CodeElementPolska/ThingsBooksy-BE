Phase **C5 — review round 1, spec conformance** for story `016-rename-resource-schema`.

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
- `id` = `review-spec-conformance-1-<n>`; `reviewer` = `review-spec-conformance`; `round` = 1; `scope.diff_sha` = the `tree` value from `scope.json`; `scope.files` = the files you actually examined.
- Fill `summary` counts. If there is nothing to report, return an empty `findings` array — that is a valid, welcome result.

Return the `findings` JSON and nothing else.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/016-rename-resource-schema/review/round-1/diff.patch`  (sha256 28a27a184754…)
- `runs/016-rename-resource-schema/review/round-1/scope.json`  (sha256 ea8aeb05df0e…)
- `runs/016-rename-resource-schema/story.md`  (sha256 cfc1c3644793…)
- `specs/016-rename-resource-schema/spec.md`  (sha256 3add460ce38f…)
- `runs/016-rename-resource-schema/contract-next.json`  (sha256 d3585a905c51…)
- `runs/016-rename-resource-schema/ac-matrix.json`  (sha256 f8a00e98a2e6…)
- `runs/016-rename-resource-schema/discovery/decisions.jsonl`  (sha256 8f6666a679b8…)

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
  "run_id": "016-rename-resource-schema-review-spec-conformance-muzbqhmm",
  "story": "016-rename-resource-schema",
  "phase": "C5",
  "inputs": [
    {
      "path": "runs/016-rename-resource-schema/review/round-1/diff.patch",
      "sha256": "28a27a18475407108ad4e0d3cf7164b1ae315175ae40dd945f33cc3ced213dab"
    },
    {
      "path": "runs/016-rename-resource-schema/review/round-1/scope.json",
      "sha256": "ea8aeb05df0e8391209038fdc00ebe14274bc491a679f38abc7ab91f556282c2"
    },
    {
      "path": "runs/016-rename-resource-schema/story.md",
      "sha256": "cfc1c3644793b103ee92fbc9f05d679ed32aa6c642a90d4491654819d2199acb"
    },
    {
      "path": "specs/016-rename-resource-schema/spec.md",
      "sha256": "3add460ce38f1e325a5874c4638caa9aa5e2603f6d11d3bf70681c82d163a45a"
    },
    {
      "path": "runs/016-rename-resource-schema/contract-next.json",
      "sha256": "d3585a905c51474546c9a8f8835a92ab1e3d98fef003845c2d66df14102bfac4"
    },
    {
      "path": "runs/016-rename-resource-schema/ac-matrix.json",
      "sha256": "f8a00e98a2e6c49deeab7a26aabde89d4cb1cb7f572ec8864a043b97d9fb3a2f"
    },
    {
      "path": "runs/016-rename-resource-schema/discovery/decisions.jsonl",
      "sha256": "8f6666a679b8572d4816d6d3577bbea33ee175888065245fb8f1159f334a8d54"
    }
  ],
  "schema_version": "1"
}
```
