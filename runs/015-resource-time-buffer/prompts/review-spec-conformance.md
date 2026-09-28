Phase **C5 — review round 2, spec conformance** for story `015-resource-time-buffer`.

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
- `id` = `review-spec-conformance-2-<n>`; `reviewer` = `review-spec-conformance`; `round` = 2; `scope.diff_sha` = the `tree` value from `scope.json`; `scope.files` = the files you actually examined.
- Fill `summary` counts. If there is nothing to report, return an empty `findings` array — that is a valid, welcome result.

Return the `findings` JSON and nothing else.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/review/round-2/diff.patch`  (sha256 647c2ac8ad52…)
- `runs/015-resource-time-buffer/review/round-2/scope.json`  (sha256 85769aba578c…)
- `runs/015-resource-time-buffer/story.md`  (sha256 9212e4b66dae…)
- `specs/015-resource-time-buffer/spec.md`  (sha256 dbdfe2408791…)
- `runs/015-resource-time-buffer/contract-next.json`  (sha256 433a70127610…)
- `runs/015-resource-time-buffer/ac-matrix.json`  (sha256 1cc409585f0c…)
- `runs/015-resource-time-buffer/discovery/decisions.jsonl`  (sha256 b1185a95ddba…)
- `runs/015-resource-time-buffer/review/round-1/review-spec-conformance.findings.json`  (sha256 b9dbba7f5cb1…)

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

## Provenance block — copy VERBATIM into the `provenance` field of your output
```json
{
  "author_agent": "review-spec-conformance",
  "run_id": "015-resource-time-buffer-review-spec-conformance-mulh5qvk",
  "story": "015-resource-time-buffer",
  "phase": "C5",
  "inputs": [
    {
      "path": "runs/015-resource-time-buffer/review/round-2/diff.patch",
      "sha256": "647c2ac8ad5217d237646a7c21dbabd790026f87e1ff254de6ce4883436ea293"
    },
    {
      "path": "runs/015-resource-time-buffer/review/round-2/scope.json",
      "sha256": "85769aba578c3dae6a354f13961f5195078b92a05f6c30f13cdfce0af01cdb02"
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
      "sha256": "1cc409585f0cfcc19347172a5d14de6ebdc267ac79b769dc85de0fbfa39ea01d"
    },
    {
      "path": "runs/015-resource-time-buffer/discovery/decisions.jsonl",
      "sha256": "b1185a95ddbae5292e15a6cd300130f5f77ea799a1fbc79ef5a6dd9ca05fbb22"
    },
    {
      "path": "runs/015-resource-time-buffer/review/round-1/review-spec-conformance.findings.json",
      "sha256": "b9dbba7f5cb181ed048a6163105f9c1ac2d3fd924042fa2b986c76ebfb50f99a"
    }
  ],
  "schema_version": "1"
}
```
