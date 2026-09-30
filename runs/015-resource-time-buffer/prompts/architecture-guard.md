Phase **C6 — architecture guard** for story `015-resource-time-buffer`.

`scope.json` lists the files the story changed (round 1 of review). Use them as the entry point, but check the rules across the WHOLE solution: the per-file reviewers cannot see wiring that spans modules.

Checklist (report only what you can prove with quoted code; empty result is fine):
- **Module boundaries** (`constitution#I`, `#IV`): no project reference or `using` from one module's `.Core`/`.Api` to another module's; cross-module data only via events (`IEvent` records in `Shared.Abstractions/Events/<Module>/`) or `IModuleClient`; no module queries another module's tables or schema.
- **Event contracts** (`constitution#IV`): every new event record in `Shared.Abstractions` is published by exactly the module that owns the data; list its publishers and handlers across the solution (a new event with no handler anywhere is reported as `MAJOR` with the note that the owner must confirm the consumer is a future module — do not resolve it yourself); payloads carry identifiers only.
- **Persistence** (`constitution#VI`, `conventions/ef-schema-isolation.md`): each module's `DbContext` uses its own schema (`HasDefaultSchema`), one migration for the story, model snapshot consistent with the configuration (e.g. a filtered unique index present in both).
- **Visibility and registration** (`constitution#XIV`, `conventions/internals-visible-to.md`): `InternalsVisibleTo` set for `.Api`, `.Migrations`, `.IntegrationTests`, `DynamicProxyGenAssembly2`; new handlers/data providers registered where the module registers them (`AddDataProviders`, `Extensions.cs`); the Bootstrapper loads the module.
- **Layering** (`constitution#XI`, `conventions/data-provider-pattern.md`): handlers depend on `I…DataProvider`, never `DbContext`; endpoints construct commands and call `IDispatcher` only.

Rules:
- `id` = `architecture-guard-1-<n>`; `reviewer` = `architecture-guard`; `round` = 1; `scope.diff_sha` = the `tree` value from `scope.json`; `scope.files` = the files you examined.
- Severity: a violated NON-NEGOTIABLE article ⇒ `BLOCKER`; other articles/conventions ⇒ `MAJOR`; guidance ⇒ `MINOR`; no rule ⇒ `OPINION`. Add `rule_candidate` (`arch-test`) to every finding a NetArchTest-style test could catch.
- Fill `summary`.

Return the `findings` JSON and nothing else.

## Input files handed to you (paths + hashes)
These are the files this task was built from. They are NOT a read boundary: your read scope is set by your access rules (allowed roots such as `backend/`, `frontend/src/`, `generated/`); read whatever you need inside it. If something you need is outside your scope, say so in the answer.
- `runs/015-resource-time-buffer/review/round-1/scope.json`  (sha256 490c202fbfbe…)
- `runs/015-resource-time-buffer/story.md`  (sha256 9212e4b66dae…)
- `specs/015-resource-time-buffer/spec.md`  (sha256 dbdfe2408791…)
- `runs/015-resource-time-buffer/contract-next.json`  (sha256 433a70127610…)

## Conventions that apply to your output
- `.specify/memory/constitution.md`
- `.claude/conventions/ef-schema-isolation.md`
- `.claude/conventions/internals-visible-to.md`
- `.claude/conventions/data-provider-pattern.md`

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
  "author_agent": "architecture-guard",
  "run_id": "015-resource-time-buffer-architecture-guard-mulhqble",
  "story": "015-resource-time-buffer",
  "phase": "C6",
  "inputs": [
    {
      "path": "runs/015-resource-time-buffer/review/round-1/scope.json",
      "sha256": "490c202fbfbe50612052cdb3cf83b854aa0f172646c901423fbc553cd492f069"
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
    }
  ],
  "schema_version": "1"
}
```
