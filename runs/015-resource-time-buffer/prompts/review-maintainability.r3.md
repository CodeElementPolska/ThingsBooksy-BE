Phase **C5 — review round 3, conventions and maintainability** for story `015-resource-time-buffer`.

You review the code diff in `diff.patch` (scope in `scope.json`) against the project's written conventions (files listed under "Conventions") and the constitution's structural articles (I, II, III, IX–XIV). Conventions apply to **changed lines only**; do not re-review untouched code.

What to report:
- `CONVENTION` — a changed line violates a rule that a convention file states. `rule_ref` = `conventions/<file>#<section heading>` or `constitution#<article>` (quote the rule in `evidence` next to the code). Severity `MAJOR` when the rule is explicit ("must", "never", "forbidden"), `MINOR` when it is guidance.
- `ARCHITECTURE` — cross-module reference, a module reading another module's tables, a type crossing the boundary outside `Shared.Abstractions`, a handler depending on `DbContext` instead of its data provider. `rule_ref` = `constitution#I`, `#IV`, `#XI`. Severity `BLOCKER`.
- `MAINTAINABILITY` — duplication introduced by the diff, dead code left behind (unused members, stale comments describing removed behaviour), misleading names, test helpers that bypass the test conventions. `rule_ref` required for `MINOR`; without one it is an `OPINION`.
- For every finding that could be checked by a tool, add `rule_candidate` (`roslyn-analyzer` / `arch-test` / `eslint` / `script`) with a one-line description — accepted candidates become deterministic checks and retire this review.

Rules:
- Do not report what `dotnet build`, `dotnet format` or the compiler already enforce (formatting, unused usings, nullability warnings).
- Not your job: spec conformance, authorization, security — other reviewers own those.
- Read the surrounding code to confirm a violation (e.g. a "missing" factory method may exist in another file). Evidence = quoted code with `file:line`.
- Round ≥ 2: review only the fix diff; fill `previous_findings_status` for each of your previous findings; findings outside the fix diff only as `BLOCKER` with `out_of_diff: true`.
- `id` = `review-maintainability-3-<n>`; `reviewer` = `review-maintainability`; `round` = 3; `scope.diff_sha` = the `tree` value from `scope.json`; `scope.files` = files examined.
- An empty `findings` array is a valid result. Fill `summary`.

Return the `findings` JSON and nothing else.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/review/round-3/diff.patch`  (sha256 d477262bd13a…)
- `runs/015-resource-time-buffer/review/round-3/scope.json`  (sha256 c3d93acc67d2…)
- `runs/015-resource-time-buffer/review/round-2/review-maintainability.findings.json`  (sha256 d783630180e2…)

## Conventions that apply to your output
- `.specify/memory/constitution.md`
- `.claude/conventions/domain-entity-design.md`
- `.claude/conventions/data-provider-pattern.md`
- `.claude/conventions/data-provider-query-syntax.md`
- `.claude/conventions/command-construction-in-endpoints.md`
- `.claude/conventions/minimal-api-endpoints.md`
- `.claude/conventions/dispatcher-usage.md`
- `.claude/conventions/naming-commands-queries-handlers-results.md`
- `.claude/conventions/ef-schema-isolation.md`
- `.claude/conventions/internals-visible-to.md`
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
  "author_agent": "review-maintainability",
  "run_id": "015-resource-time-buffer-review-maintainability-r3-mulhg9mi",
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
      "path": "runs/015-resource-time-buffer/review/round-2/review-maintainability.findings.json",
      "sha256": "d783630180e289545238e42e48187d5e39b7a52b01cc5d4d961b14b533afda54"
    }
  ],
  "schema_version": "1"
}
```
