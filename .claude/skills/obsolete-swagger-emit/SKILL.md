---
name: "obsolete-swagger-emit"
description: "Deterministic OpenAPI 3.0 emission from a FINALIZED `specs/{feature}/api-contract.md` into `specs/{feature}/swagger.json` (consumed by `fe-api-client-writer` and `contract-drift-check`)."
argument-hint: "Optional: absolute path to api-contract.md (default: locate from current feature branch)"
compatibility: "Requires spec-kit project structure with .specify/ directory and a FINALIZED `specs/{feature}/api-contract.md`. Run after `contract-definer` reports `CONTRACT-DEFINER COMPLETE`."
metadata:
  author: "thingsbooksy"
  source: "agent-fleet-redesign Etap 2 item 4b"
user-invocable: true
disable-model-invocation: false
tools: Glob, Read, Write, Bash
---


## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

Supported arguments:
- *(no argument)* — locate `api-contract.md` from the active feature branch via `setup-plan.ps1 -Json`.
- *absolute path to `api-contract.md`* — use the provided path directly. Useful when emitting for a non-current branch.

Any other value is treated as ignored noise.

## Purpose

`contract-definer` finalizes `specs/{feature}/api-contract.md` (DRAFT → FINALIZED) through interactive negotiation with the developer. It does **not** emit OpenAPI JSON, because LLM-generated JSON is structurally fragile and benefits from a deterministic post-step with explicit validation.

This skill is that post-step. It is the canonical writer of `specs/{feature}/swagger.json`. It is **not** auto-invoked via `extensions.yml` — the orchestrator (main session) calls it after `CONTRACT-DEFINER COMPLETE`, following the `Next step:` line in the agent's final report.

The emitted `swagger.json` becomes the input for:
- `fe-api-client-writer` (regenerates the TypeScript HTTP client at `frontend/src/app/api/`),
- `contract-drift-check` (compares static contract vs runtime swagger),
- any developer who needs a machine-readable view of the FE↔BE surface.

## Pre-Execution Checks

**Resolve target paths**:
- If `$ARGUMENTS` is empty:
  - Run `.specify/scripts/powershell/setup-plan.ps1 -Json` from repo root and parse `SPECS_DIR`, `BRANCH`.
  - If the script fails or `BRANCH` does not look like a feature branch (no `NNN-` prefix), abort with: `ERROR: cannot determine active feature — pass an absolute path to api-contract.md as argument`.
  - Target input: `{SPECS_DIR}/api-contract.md`.
  - Target output: `{SPECS_DIR}/swagger.json`.
- If `$ARGUMENTS` is a non-empty string:
  - Treat it as the absolute path to `api-contract.md`.
  - Output path is `swagger.json` in the same directory.

**Verify input contract**:
- The input `api-contract.md` must exist. Abort if missing: `ERROR: api-contract.md not found at {path} — run /contract-definer first`.
- Read the frontmatter / preamble. Locate the `Status:` field.
  - `Status: FINALIZED` → proceed.
  - `Status: DRAFT` → abort with: `ERROR: Cannot emit swagger.json from DRAFT contract — run /contract-definer first to finalize.`
  - Any other value (missing, malformed) → abort with: `ERROR: api-contract.md has invalid Status field. Inspect manually.`

**Output overwrite policy**:
- `swagger.json` is fully regenerable from a FINALIZED contract. Overwrite is always allowed; no `--force` flag exists.
- If the output path already exists, log `Mode: overwrite` in the final report; otherwise log `Mode: fresh`.

## Outline

### Phase 1 — Load and parse

1. Read `api-contract.md` in full.
2. Parse:
   - The endpoint table in `## 1. Endpoints` (method, route, module, auth, request DTO, response DTO).
   - All DTO shapes from `## 2. DTO shapes (draft)` (despite the heading retained from the draft template, after finalization the shapes are authoritative). Each DTO becomes a `components/schemas` entry.
   - Cross-module dependencies from `## 3. Cross-module dependencies` (informational — does not produce OpenAPI operations).
   - The `## 6. Negotiation log` is informational; ignore for emission.
3. Build an in-memory model: `{ endpoints: [...], schemas: {...} }`.

### Phase 2 — Render OpenAPI 3.0

Render an OpenAPI 3.0.x document with the following structure:

```json
{
  "openapi": "3.0.3",
  "info": {
    "title": "ThingsBooksy — {feature-slug}",
    "version": "1.0.0",
    "description": "Static FE↔BE contract derived from specs/{feature}/api-contract.md (FINALIZED)."
  },
  "paths": {
    "/{route}": {
      "{method}": {
        "operationId": "{moduleKebab}_{actionCamel}",
        "tags": ["{Module}"],
        "security": [...],
        "requestBody": { ... },
        "responses": { ... }
      }
    }
  },
  "components": {
    "schemas": { ... },
    "securitySchemes": {
      "bearerAuth": { "type": "http", "scheme": "bearer", "bearerFormat": "JWT" }
    }
  }
}
```

Rules:
- Emit OpenAPI **3.0.x** (currently `3.0.3`). Never emit Swagger 2.0 or OpenAPI 3.1.
- Every DTO from `## 2.` lands in `components/schemas` as a top-level entry. Never inline shared DTOs — always use `{"$ref": "#/components/schemas/{Name}"}`.
- For an endpoint with `Auth: Bearer`, attach `"security": [{ "bearerAuth": [] }]`. For `Auth: —` or `Anonymous`, omit `security`.
- For an endpoint with `Request DTO: —` (em dash), omit `requestBody`.
- Map markdown field types to OpenAPI:
  - `Guid` → `{ "type": "string", "format": "uuid" }`
  - `string` → `{ "type": "string" }`
  - `int` / `long` → `{ "type": "integer", "format": "int32" | "int64" }`
  - `decimal` → `{ "type": "number", "format": "decimal" }`
  - `bool` → `{ "type": "boolean" }`
  - `DateTimeOffset` / `DateTime` → `{ "type": "string", "format": "date-time" }`
  - `T[]` or `List<T>` → `{ "type": "array", "items": <T-mapped> }`
- Default response code is `200`. If the endpoint description references created/no-content/error semantics in the contract, include the corresponding 201/204/4xx response with empty schema unless an explicit error DTO is named.
- All routes are written verbatim. Path parameters (`{id}`) become OpenAPI `parameters` entries of `in: path`, `required: true`, with the type derived from the matching DTO field or defaulted to `string`/`uuid` when the table lacks the information.

### Phase 3 — Write the file

Write the rendered JSON to the output path using the `Write` tool. Use 2-space indentation, UTF-8, LF line endings (the Write tool handles encoding; do not append a BOM).

### Phase 4 — Validate

Validation is split into three checks. Each failure triggers a retry (cap 2 retries — i.e. 3 total attempts). 4a and 4c run unconditionally; 4b runs when `npx` is available, otherwise it is skipped with a WARNING in the final report.

**4a — Structural JSON validity (deterministic, via PowerShell)**

Run:
```powershell
pwsh -Command "Get-Content -Raw -Path '{absolute-output-path}' | ConvertFrom-Json | Out-Null"
```

Expected: exit code 0 and no stderr. On failure:
- Capture stderr.
- Increment retry counter.
- If retries remain → return to Phase 2, re-render with the captured error as feedback (e.g. "trailing comma at offset X"), re-write, re-validate.
- If retries exhausted → ABORT (see Failure mode).

**4b — OpenAPI 3.0 schema validation (deterministic, via npx swagger-cli)**

Probe for `npx` availability first:
```bash
npx --version
```

If the probe fails (non-zero exit or "command not found"), skip 4b entirely — log `Validation 4b: SKIPPED (npx unavailable — install Node.js to enable schema validation)` and proceed to 4c. Do not retry, do not abort.

If `npx` is available, run:
```bash
npx --yes @apidevtools/swagger-cli validate "{absolute-output-path}"
```

Expected: exit code 0 and a `... is valid` line on stdout. On failure (non-zero exit):
- Capture stdout + stderr verbatim (swagger-cli outputs precise pointers, e.g. `#/paths/~1users~1{id}/get/responses/200/content/application~1json/schema — Schema is missing`).
- Increment retry counter.
- If retries remain → return to Phase 2, re-render with the captured error as feedback (verbatim, including the JSONPointer), re-write, re-run 4a then 4b.
- If retries exhausted → ABORT (see Failure mode).

This check catches issues that 4a (pure JSON parse) and 4c (LLM self-check vs api-contract.md) both miss: invalid `$ref` targets, missing required OpenAPI keywords, wrong field shapes per the OpenAPI 3.0 meta-schema, malformed `parameters` arrays, illegal nullable/required combinations. It is the strongest gate in the pipeline and the reason a successful 4b is recorded explicitly in the final report.

**4c — Semantic self-check vs api-contract.md (LLM, deterministic checklist)**

After 4a passes, perform a deterministic self-check by re-reading both files and comparing. Run **every** check on the list — do not stop at the first match. Record each check as `PASS` or `FAIL` with a short reason.

1. **Endpoint count**: number of `(method, route)` pairs in `paths` equals the number of rows in `## 1. Endpoints`.
2. **Endpoint identity**: every `(method, route)` from `## 1.` appears in `paths`, and vice versa.
3. **Auth coverage**: every endpoint with `Auth: Bearer` in the table has `security: [{ bearerAuth: [] }]` in JSON.
4. **Schema count**: every DTO named in `## 2.` (or referenced from `Request DTO` / `Response DTO` columns) exists in `components/schemas`.
5. **Schema field count**: for each DTO, the number of properties in JSON equals the number of bullets in the markdown DTO definition.
6. **Schema field names**: for each DTO, the property name set in JSON equals the bullet name set in markdown (case-sensitive).
7. **Schema field types**: for each property, the OpenAPI type/format pair matches the mapping rules in Phase 2.
8. **Path parameters**: every `{x}` placeholder in a route has a matching `parameters` entry with `in: path`, `required: true`.
9. **Request body presence**: an endpoint has `requestBody` iff its `Request DTO` column is not `—`.
10. **`$ref` discipline**: no DTO from `components/schemas` is inlined anywhere in `paths` — only referenced via `$ref`.

If any check returns `FAIL`:
- Increment retry counter.
- If retries remain → return to Phase 2, re-render with the failing checks as feedback (verbatim, e.g. "Check 6 FAIL: UserSummary.displayName missing in JSON schema"), re-write, re-run Phase 4a, 4b, then 4c.
- If retries exhausted → ABORT (see Failure mode).

If every check passes, proceed to Phase 5.

### Phase 5 — Report

Emit a single final report:

```
## SWAGGER-EMIT COMPLETE

File: {absolute path to swagger.json}
Mode: {fresh|overwrite}
Endpoints: {N}
Schemas: {M}
Validation:
  4a structural (pwsh ConvertFrom-Json): PASS
  4b OpenAPI 3.0 schema (npx swagger-cli validate): PASS | SKIPPED (npx unavailable)
  4c semantic self-check (10/10): PASS
Retries used: {0|1|2}
```

If 4b was skipped because `npx` is unavailable, append a single advisory line at the end of the report:

```
Advisory: install Node.js to enable Phase 4b — npx @apidevtools/swagger-cli validate.
```

## Failure mode

After 2 failed retries (i.e. 3 total attempts with the same root cause), abort with:

```
## SWAGGER-EMIT ABORTED

File: {absolute path to swagger.json}
Failed phase: {4a|4b|4c}
Retries used: 2
Last error:
  {verbatim error message or list of failed checks}

Manual diagnosis:
  npx @apidevtools/swagger-cli validate {absolute-output-path}

Action: inspect both api-contract.md and swagger.json side-by-side, then re-invoke /swagger-emit after the discrepancy is resolved.
```

The orchestrator must not proceed to `fe-api-client-writer` while `swagger.json` is in an aborted state.

## Key rules

- Always emit OpenAPI **3.0.x** (currently `3.0.3`). Not Swagger 2.0, not OpenAPI 3.1.
- Every shared DTO lives under `components/schemas`. Use `$ref` everywhere it is referenced; never inline.
- Validate after every write — both Phase 4a (structural) and Phase 4b (semantic) must pass before reporting COMPLETE.
- Never modify `api-contract.md` — it is read-only input to this skill. Finalization is `contract-definer`'s job; emission is yours.
- Never set or change `Status:` anywhere. The status of `api-contract.md` must remain `FINALIZED` after this skill runs.
- Retry cap is **2** retries (3 total attempts). After that, abort with the manual fallback suggestion.
- This skill performs no git operations and no further agent calls. It is a pure file emitter with built-in validation.
- Use absolute paths for filesystem operations (`Read`, `Write`, `Bash`). Use project-relative paths only inside human-readable report text.
