# ThingsBooksy

> **Business description coming soon.**

---

## Architecture

ThingsBooksy is a **Modular Monolith** — a single deployable artifact composed of isolated, independently-developed modules. Each module owns its domain, its database schema, and its HTTP surface. Modules never reference each other directly; all cross-module communication goes through events (fire-and-forget) or queries (request/response) via shared infrastructure abstractions.

```
┌───────────────────────────────────────────────────────────────┐
│                   ThingsBooksy.Bootstrapper                    │
│      Discovers and composes all modules at startup via         │
│      reflection (IModule interface)                            │
└───────────────────────────────┬───────────────────────────────┘
                                │
          ┌─────────────────────┼─────────────────────┐
          ▼                     ▼                     ▼
┌───────────────────┐ ┌───────────────────┐ ┌───────────────────┐
│       Users       │ │  ManagementGroups │ │     Resources     │
│       .Api        │ │       .Api        │ │       .Api        │
│       .Core       │ │       .Core       │ │       .Core       │
│    .Migrations    │ │    .Migrations    │ │    .Migrations    │
│ .IntegrationTests │ │ .IntegrationTests │ │ .IntegrationTests │
└─────────┬─────────┘ └─────────┬─────────┘ └─────────┬─────────┘
          │                     │                     │
          └─────────────────────┼─────────────────────┘
                                │
                   ┌────────────▼────────────┐
                   │   Shared.Abstractions   │
                   │   Events / Contracts    │
                   │   IDispatcher           │
                   │   IMessageBroker        │
                   └─────────────────────────┘
```

Each module follows a consistent project structure:

```
src/Modules/{ModuleName}/
├── {ModuleName}.Api               # Minimal API endpoints, DTOs, module config
├── {ModuleName}.Core              # Domain entities, EF DbContext, handlers
└── {ModuleName}.Migrations        # EF Core migrations (optional)

tests/
└── {ModuleName}.IntegrationTests  # Integration tests
```

---

## Modules

| Module | Description |
|---|---|
| **Users** | Authentication and user account management |
| **ManagementGroups** | Creation and management of groups and their memberships |
| **Resources** | EAV-based resource schema — resource types with property definitions, and resource instances with typed attribute values |

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 |
| Language | C# 13 |
| Database | PostgreSQL 17 (Docker) |
| ORM | Entity Framework Core 10 |
| Auth | JWT Bearer + AES-256 |
| API docs | Swashbuckle / Swagger UI (`/swagger`) |
| Logging | Serilog |
| Containerization | Docker / docker-compose |

---

## Rules & Conventions

All conventions live in `.claude/conventions/`. Every agent that writes or reviews code must follow them exactly.

| Convention | Summary |
|---|---|
| **domain-entity-design** | All entity properties use `private set`; entities are created via a static `Create()` factory (max 4 params) and mutated through named domain methods only. Read-models use `Upsert()` instead of `Create()`. |
| **naming-commands-queries-handlers-results** | Commands, queries, and handlers use full module-scoped PascalCase with their respective suffixes (`Command`, `CommandHandler`, `Query`, `QueryHandler`). Result types are derived mechanically from the handler name by stripping `Handler`. |
| **data-provider-pattern** | Handlers never inject `DbContext` directly — each handler depends on a dedicated `IXxxDataProvider` interface co-located in its feature folder. Providers are registered automatically via `AddDataProviders` called once per module. |
| **data-provider-query-syntax** | DataProvider methods use parenthesized LINQ query syntax with materialization chained outside — `(from ... select ...).ToListAsync(ct)` — for joins and group-by; method syntax is allowed only for simple single-table queries. |
| **command-construction-in-endpoints** | Commands are never bound directly from the HTTP request body; a request DTO (`{Module}.Api/Requests/`) holds only client-permitted fields, and the endpoint constructs the command explicitly. |
| **minimal-api-endpoints** | All HTTP endpoints use Minimal API (no MVC controllers), registered in `Expose()` with a `/{module-name}/...` route prefix. `AddEndpointsApiExplorer()` must be called in `Register()`. |
| **dispatcher-usage** | All commands go through `IDispatcher.SendAsync`, all queries through `IDispatcher.QueryAsync`. Direct handler calls and MediatR are forbidden. |
| **ef-schema-isolation** | Every module `DbContext` must call `modelBuilder.HasDefaultSchema(...)` with the module's lowercase snake_case name. Using `"public"` or omitting the call is forbidden. |
| **internals-visible-to** | Every `.Core` project must declare four `InternalsVisibleTo` attributes: for `.Api`, `.Migrations`, `.IntegrationTests`, and `DynamicProxyGenAssembly2`. |
| **integration-test-naming** | Test methods follow `{Action}{Entity}_{Condition}_{Result}` — the result segment is an HTTP status code for simple status assertions or a behavioral description when the test also asserts side effects or DB state. |
| **integration-test-infrastructure** | Each module defines a `TestClient` (HTTP + DB methods), entity `Factory` classes (direct DB insertion), and a module-scoped `IntegrationTestCollection`. Each test creates its own user to avoid shared state. |

---

## AI — the agent fleet (v4)

ThingsBooksy is built by a **heavy agentic workflow**: a fleet of purpose-built Claude Code agents, glued together by deterministic Node scripts, that takes a story from a product idea to a reviewed, tested, closed pull request. The human in the loop is the **owner** (product owner and the only person who decides). Everything else is an agent or a script.

This section is the entry point for someone who has never seen a workflow like this. It explains the ideas first, then every agent, every script and every file the fleet produces. Authoritative detail lives in `docs/agent-fleet-v4/` and `tools/fleet/README.md`; this is the map.

### Design principles

The fleet is built on seven rules (`docs/agent-fleet-v4/workflow.md` §0). Everything below follows from them.

| # | Principle | What it means in practice |
|---|---|---|
| 1 | **Source of truth outside the LLM** | Correctness is decided by a script or a test, never by a second model. An LLM judges only what cannot be expressed mechanically. |
| 2 | **Frame = mechanism, not prompt** | Isolation and limits are enforced by tool allowlists, a path ACL hook, `omitClaudeMd`, no shell for blind agents, output schemas, `maxTurns` and prompts assembled by a script. Not by asking nicely. |
| 3 | **Owner gate = phase boundary** | Subagents cannot talk to a human. The owner is asked only at gates: G1, G2, G3 and the exceptional G2b for a risky migration. |
| 4 | **One author per artifact, every artifact has a schema and provenance** | Nobody rewrites someone else's document. Each artifact records who produced it, in which run, from which inputs (sha256). |
| 5 | **Three decision buckets with a hard list** | Every doubt an agent has is triaged: ask the owner, assume with a scored default, or do not ask because the code already answers. Authorization, data loss and new UI are always asked. |
| 6 | **Hooks enforce, never steer** | Flow lives in scripts and a journal; hooks only block forbidden actions and record owner answers. |
| 7 | **One story at a time** | Parallelism only inside a story (backend ∥ frontend, three reviewers ∥). No migration or test-database conflicts. |

### Glossary

| Term | Meaning |
|---|---|
| **Story** | One unit of work with stable id `NNN-slug`. The id is at the same time the git branch, the folder `specs/NNN-slug/` (SpecKit spec, plan, tasks) and the folder `runs/NNN-slug/` (every artifact the fleet produced while working on it). |
| **Owner** | The human. Talks only to the two personas and answers gate questions through `AskUserQuestion`. An answer typed in chat does not exist for the scripts. |
| **Conductor** | The plain Claude session that drives delivery (session C). It builds prompts with a script, spawns agents, saves their results and runs the scripts between phases. It never writes code and never fixes a failure itself. |
| **Persona** | An agent started as the main session with `claude --agent <name>`. It can talk to the owner. There are two: `scrum` and `dev-analyst`. |
| **Subagent / worker** | A one-shot agent spawned by a persona or the conductor. Gets one prompt file, returns one JSON object, cannot ask anyone anything, cannot be continued. |
| **Blind agent** | A worker whose read scope deliberately excludes something: the critics never see the code or the conversation, the blind test designer never sees production source, the trace auditor never sees production code. Blindness is enforced by the ACL hook, not by the prompt. |
| **Gate** | A decision point for the owner. **G1** story accepted · **G2** discovery package accepted · **G2b** a migration classified REVIEW or DESTRUCTIVE accepted · **G3** story closed, commit allowed. Only `decide.js --gate` can close a gate. |
| **Hard list (D-1)** | Authorization and security · data and migrations (data loss, destructive migrations) · new UI outside the story. Never defaulted; an assumption on this list without an owner decision fails the Definition of Done. |
| **Triage buckets (D-2)** | **ASK** (hard list, irreversible, or visibly different options without a clear recommendation) · **ASSUME** (cheaply reversible, recommended default, logged with a score, silence = acceptance) · **DO NOT ASK** (the answer is in the code, cite `file:line`). |
| **AC-n** | Acceptance criterion id, assigned once in the story and never renumbered. Backend tests carry `[Trait("AC", "AC-n")]`, frontend tests `it("[AC-n] …")`. Scripts build the AC → test matrix from these tags. |
| **Provenance** | Header on every agent artifact: `author_agent`, `run_id`, `story`, `inputs[{path, sha256}]`. A consumer refuses an artifact whose inputs changed since it was produced. |
| **Finding severities (D-9)** | **BLOCKER** breaks an AC, a constitution article, the API contract or a hard-list category; blocks the gate. **MAJOR** breaks a convention with a `rule_ref` or is `UNSPECIFIED_BEHAVIOR`; must be fixed or disputed before close. **MINOR** cosmetic with a `rule_ref`. **OPINION** no `rule_ref`; never blocks, shown to the owner in bulk. |
| **Writer statuses** | `DONE` · `BLOCKED_ON_DECISION` (owner must decide, question in D-2 format) · `DISPUTE` (writer contests a review finding) · `DISPUTE_TEST` (writer claims an acceptance test contradicts the spec) · `FAILED`. The conductor branches on this field, never on prose. |

### The three sessions

A story passes through three sessions. Each one starts from a clean context and reads only the files the previous one left behind.

```
SESSION A  business    claude --agent scrum         on the base branch (main)   → runs/<story>/story.md, GitHub issue   [G1]
SESSION B  discovery   claude --agent dev-analyst   on the story branch         → specs/<story>/, runs/<story>/discovery/ [G2]
SESSION C  delivery    claude  (plain session = conductor, runbook-driven)       → code, tests, review, close report     [G2b?, G3]
```

All three are started **from PowerShell in the repository root**, never from inside a running Claude session (a persona started from inside another session is not injected). The personas open the conversation themselves thanks to `initialPrompt` in their frontmatter; they talk to the owner in Polish and write every artifact in English.

#### Session A — business (`scrum`)

```
scrum  ⇄  owner
  ├─ capability-analyst ×N  [subagent, read-only on generated/]   one question → one fact with evidence
  ├─ scope-critic           [blind, Read only, 6 turns]            value · cheapest version · scope creep · dependency
  └─ premortem-critic       [blind, Read only, 6 turns]            data loss · migration · authz · ops · timing · concurrency
critic reports → owner UNFILTERED (scrum does not summarise)
→ story.md → backlog-writer.js (validate + render issue body) → G1 → GitHub Story issue
```

- **Input:** the owner's idea. **Output:** `runs/<story>/story.md` with front matter per `story.schema.json`: title, why, acceptance criteria with ids (given/when/then, role, exact user-visible message), out of scope, rejected alternatives with reasons, blast radius (modules + `touches_*` flags), dependencies, capability-map version, critique refs. No tasks (D-6).
- `scrum` never reads production source. Its only picture of the application is `generated/capability-map.json` and what `capability-analyst` finds in the generated artifacts. When the owner drifts into implementation, the wish is written under `notes_for_discovery` and the conversation is steered back.
- Right after the title is settled, `scrum` allocates the next `NNN`, creates the branch `NNN-slug` and the folder `runs/NNN-slug/`, so that every later owner answer lands in the story's journal.
- Critics get one round plus one reply, never a third. `NO_OBJECTIONS` is a legitimate verdict. Every objection ends as accepted (proposal changes), rejected by the owner (goes to `rejected_alternatives`) or deferred (out of scope).
- **G1:** one `AskUserQuestion` (“Do you accept the story?”), closed with `decide.js --gate G1`, then the GitHub issue is created through the GitHub MCP and its number written into `story.md`. The persona commits `runs/<story>/` only after an explicit yes.

#### Session B — discovery (`dev-analyst`)

```
dev-analyst  ⇄  owner        (loop without a round limit; stop = the ASK bucket is empty)
  ├─ impact-analyst          [subagent, read-only]   story + code → modules, files, entities, endpoints, migration likely?, BLOCKED_BY
  └─ code-researcher ×N      [subagent, read-only, parallel]   ONE factual question → fact + file:line + confidence
after EVERY round: runs/<story>/discovery/round-N.md
→ facts.jsonl · decisions.jsonl · assumptions.jsonl · ui-sketch.md · spec.md
→ /speckit-plan → /speckit-tasks → /speckit-clarify + /speckit-analyze as LINT only
→ contract-delta.overlay.json → contract-compose.js → contract-next.json
→ G2 package
```

- `dev-analyst` **never reads production code itself**. A question becomes a file in `runs/<story>/discovery/questions/<n>.md`, `prompt-builder.js` turns it into a prompt, a `code-researcher` instance answers it; the fact is appended verbatim to `facts.jsonl` as `F-<n>`. A follow-up question is a new file and a new instance; workers cannot be continued.
- Every doubt is triaged into the D-2 buckets and the bucket is written down: `assumptions.jsonl` for ASSUME and DO NOT ASK (with evidence), `decisions.jsonl` for ASK. Each owner question follows a fixed shape: plain-language context → what the code already establishes (fact ids) → options with consequences → recommendation with argument → **the strongest argument against the recommendation** → what changes downstream. Max four questions per round.
- `decided_by: owner` is written only by `decide.js` from the journal, right after the owner answers through `AskUserQuestion`. The persona never writes that field.
- The persona writes `spec.md` itself in the SpecKit template (D-5): every acceptance scenario starts with its id. `/speckit-plan` and `/speckit-tasks` generate plan and tasks; `/speckit-clarify` and `/speckit-analyze` only lint (open clarify questions mean discovery is not finished); `/speckit-specify` and `/speckit-implement` are never used. `tasks.md` is shaped for the delivery phases: skeleton = production data shapes only; test infrastructure and tests = acceptance-test phase; migration = developer-only task.
- API changes are described as an **OpenAPI Overlay 1.0** over `generated/swagger.base.json` (D-10); the owner is shown the resulting endpoint diff, never the raw overlay. Stories touching UI get `ui-sketch.md`: screens → elements → states → actions → endpoints (D-7).
- Early verdicts end the session: `RETURN` (contradicts the capability map), `SPLIT` (more than one user journey), `TOO_BIG` (schema changes in more than one module), `BLOCKED_BY` (a prerequisite is missing).
- **G2:** one screen for the owner: open decisions (should be none), the ASSUME list (one line + consequence each), endpoint diff, UI sketch, links to spec and plan. Closed with `decide.js --gate G2 --status PASSED`.

#### Session C — delivery (conductor)

The conductor is a plain Claude session following `docs/agent-fleet-v4/runbook-delivery.md`. Between phases **only scripts** decide; a failed script sends its output back to the agent as a new instance of the same template. The owner is asked only at G2b and G3 and only through `AskUserQuestion`.

```
 0   status.js                       branch = story, no stray .specify/feature.json
 C1  contract-compose.js             overlay composes (plan guard does not exist yet — skipped)
     baseline.js                     records the commit the story's code starts from
 C3a be-writer (skeleton template)   entities, EF config, DbSets, command/query/result records, event records
     skeleton-check.js               nothing but data shapes changed since baseline
     ── developer ──                 dotnet ef migrations add …  (never an agent)
     Tooling tests                   re-export generated/core-surface.json
     migration-check.js              SAFE / REVIEW / DESTRUCTIVE per statement → REVIEW or DESTRUCTIVE opens G2b
     [G2b]                           AskUserQuestion naming the migration sha → decide.js --gate G2b
 C2  test-designer (blind)           acceptance tests + factories from AC + contract + core-surface, no production source
     red-first-prover.js             tests compile and FAIL for the right reason; hash of acceptance tests saved
     ac-matrix.js                    every AC has at least one test
 C3b be-writer (behaviour) ∥ fe-writer (behaviour)      handlers, endpoints, domain methods, unit tests / Angular code
     conductor regenerates frontend/src/app/api/ from swagger BEFORE fe-writer starts
     gate.js                         migration → build → format → tests → fe-build → fe-test → swagger re-export
                                     → contract-diff → ac-matrix → acceptance-test hash      (RED → writer, max 3 rounds)
 C4b coverage-gaps.js                untested lines / branches of the story's NEW code
     test-designer-sighted           tests for the gaps; each tagged AC-n or UNSPECIFIED → gate.js again
 C5  review-diff.js                  round N diff + scope
     review-spec-conformance ∥ review-security-authz ∥ review-maintainability   (read-only, parallel)
     dedup-findings.js               CLEAN | FIX_REQUIRED → be-writer/fe-writer fix | DECISIONS_REQUIRED → owner / tester
                                     | ESCALATE (blockers do not shrink, or round 3)
     review-arbiter                  one disputed finding → UPHOLD | OVERTURN | ESCALATE
 C6  architecture-guard ∥ trace-auditor   whole-solution structure · strength of assertions per AC
     dedup-findings.js --dir closing → fixes / decisions as in C5
     tick-tasks.js · dod.js · closer.js   tasks ticked, Definition of Done, metrics + close report
 [G3] owner: OPINIONs, UNSPECIFIED tests, escalations → decide.js --gate G3 → commit only after an explicit yes
```

Why the odd order (skeleton → blind tests → behaviour, D-4a/D-4b): the blind test designer needs the entity types to seed data through EF, but must not see any logic, so the skeleton phase produces only data shapes and a reflection dump (`generated/core-surface.json`) of them. Tests are then written from requirements alone and are proven red before behaviour exists. After behaviour is green, a second, sighted pass adds tests for branches the blind pass could not know about; every such test is tagged with an AC or with `UNSPECIFIED`, which turns a rule that exists only in code into an owner decision instead of silently ratifying it with a green test.

How the conductor reacts to agent statuses: `DONE` → next script · `BLOCKED_ON_DECISION` → the D-2 question goes to the owner via `AskUserQuestion`, `decide.js`, agent re-run · `DISPUTE_TEST` → the owner settles it; a changed test means the tester edits it and a `REBASELINE` entry in the journal · `FAILED` → the `error` text goes to the owner, no blind retry.

### Agent reference

Eighteen agent files live in `.claude/agents/`. Every worker has `omitClaudeMd: true` (it does not see this repository's `CLAUDE.md`), an explicit tool allowlist, a `maxTurns` cap and a path ACL entry in `tools/fleet/fleet-acl.json`. Workers return exactly one JSON object validated against a schema from `docs/agent-fleet-v4/schemas/`. Prompt templates are in `tools/fleet/templates/`.

#### Personas (talk to the owner)

| Agent | Model | Tools | Reads | Produces | Ends with |
|---|---|---|---|---|---|
| **scrum** | session default | Read, Glob, Grep, Agent, AskUserQuestion, Write, Edit, Bash, GitHub MCP (issue write/read/search/list) | `generated/capability-map.json`, fleet docs; never `backend/src` or `frontend/src` | `runs/<story>/{story.md, proposal.md, questions/, capability-report.jsonl, critique/, issue-body.md}`, the story branch, the GitHub issue | G1 |
| **dev-analyst** | session default | Read, Glob, Grep, Agent, AskUserQuestion, Skill, Write, Edit, Bash | story, facts from workers, SpecKit templates; never answers a code question itself | `specs/<story>/{spec,plan,tasks}.md`, `runs/<story>/discovery/*`, `contract-delta.overlay.json`, `ui-sketch.md` | G2 or RETURN / SPLIT / TOO_BIG / BLOCKED_BY |

#### Session A workers

| Agent | Model · turns | Tools | Can read (ACL) | Input → output | Rule that defines it |
|---|---|---|---|---|---|
| **capability-analyst** | sonnet · 15 | Read, Grep, Glob | `generated/`, its question, its prompt | one plain-language question → `fact` with evidence (`METHOD /route`, response codes, schema field, screen route) | Facts about what the app does **today**, from generated artifacts only; `unknown` is a valid answer. |
| **scope-critic** | sonnet · 6 | Read | `proposal.md`, `capability-report.jsonl`, `capability-map.json`, its prompt | proposal → `critique` (rubrics value / cheapest-version / scope-creep / dependency) | Blind to the conversation and the code; every item weighted 1–5 with evidence and a concrete alternative; max six items. |
| **premortem-critic** | opus · 6 | Read | as above + `decisions.md` | proposal → `critique` (rubrics data-loss / migration / authz / ops / timing / concurrency) | Assumes the story shipped and failed; hard-list silence gets weight ≥ 4. |

#### Session B workers

| Agent | Model · turns | Tools | Can read (ACL) | Input → output | Rule that defines it |
|---|---|---|---|---|---|
| **code-researcher** | sonnet · 20 | Read, Grep, Glob | `backend/`, `frontend/src/`, `specs/`, `generated/`, its question, constitution, conventions | one factual question → `fact` with `file:line` excerpts and confidence | Never proposes, never judges, never infers intent; spawned in parallel, one per question. |
| **impact-analyst** | sonnet · 40 | Read, Grep, Glob | as above + `story.md`, `discovery/` | story → blast radius: modules, files, entities, endpoints, events, likely schema change, `BLOCKED_BY`, risks as facts | Says a migration is likely; never designs it. |

#### Session C writers

| Agent | Model · turns | Tools | Can write (ACL) | Shell prefixes | Rule that defines it |
|---|---|---|---|---|---|
| **be-writer** | opus · 80 | Read, Grep, Glob, Edit, Write, Bash | its module's `.Core` and `.Api`, `Shared.Abstractions`, `Bootstrapper`, `.slnx`, `runs/*/impl` | `dotnet build`, `dotnet test backend/src/Modules/`, `dotnet format …`, `skeleton-check.js`, `validate.js`, `git diff`, `git status` | Two templates: skeleton (C3a, data shapes only) and behaviour (C3b). Reads acceptance tests, never edits them; never touches `*.Migrations` or `frontend/`. Returns `BLOCKED_ON_DECISION` instead of guessing on the hard list. |
| **fe-writer** (v0) | opus · 80 | Read, Grep, Glob, Edit, Write, Bash | `frontend/src/app/{features,shared,core}`, `runs/*/impl` | `npm --prefix frontend run build`, `npm --prefix frontend test`, `validate.js`, `git diff`, `git status` | Cannot read `backend/`; the API is defined by the contract and the generated client. Never edits `*.spec.ts` or `frontend/src/app/api/` (regenerated by the conductor; missing types → `FAILED`, no workarounds). |
| **test-designer** (blind pass) | opus · 80 | Read, Grep, Glob, Edit, Write, Bash | `*.IntegrationTests`, `Shared.IntegrationTests`, `frontend/**/*.spec.ts`, `runs/*/impl` | `dotnet build` / `dotnet test` of test projects only, `validate.js`, `ac-matrix.js` | Cannot read `.Core` / `.Api`. Writes factories, test clients and acceptance tests from AC + contract + `core-surface.json`. Arrange = EF seed, Act = HTTP, Assert = EF re-read. Tests must fail for the right reason. |
| **test-designer-sighted** (second pass) | opus · 60 | Read, Grep, Glob, Edit, Write, Bash | `*.IntegrationTests`, `frontend/**/*.spec.ts`, `runs/*/impl` | as above | May read production code. Every added test is tagged `AC-n` or `UNSPECIFIED` (+ an assumption for the owner). Never edits the blind pass's tests (hash-checked) or production code. Also runs fix rounds for tester-routed findings. |

#### Session C reviewers (phase C5, parallel, read-only)

| Agent | Model · turns | Sees | Does not see | Output |
|---|---|---|---|---|
| **review-spec-conformance** | opus · 40 | round diff, story, spec, decisions, contract, AC matrix, its own previous findings | writer notes, other reviews | `findings` — every deviation and every `UNSPECIFIED_BEHAVIOR`, `rule_ref` = `AC-n` / `DEC-n` / `contract:<path>` |
| **review-security-authz** | opus · 40 | round diff, story, contract, its own previous findings | writer notes, other reviews | `findings` — authorization, data loss, unsafe inputs, leaks; `rule_ref` = `CWE-n` / constitution / AC; hard-list gaps are BLOCKER |
| **review-maintainability** | sonnet · 40 | round diff, conventions, its own previous findings | writer notes, other reviews | `findings` — changed lines vs written conventions; `rule_candidate` whenever a tool could enforce the rule instead |
| **review-arbiter** | opus · 15 | one finding, one dispute, the disputed code | the rest of the review, the history | `arbiter-verdict` — `UPHOLD` / `OVERTURN` / `ESCALATE`, naming the one `rule_ref` that settled it |

From round 2 a reviewer sees only the fix diff and its own previous findings and must report the status of each. A finding without quoted `file:line` is not a finding; a finding without `rule_ref` is an OPINION.

#### Session C closers (phase C6, parallel, read-only)

| Agent | Model · turns | Sees | Does not see | Output |
|---|---|---|---|---|
| **architecture-guard** | opus · 40 | whole `backend/`, `frontend/src`, constitution, conventions, contract | writer notes | `findings` type ARCHITECTURE — module boundaries, schema isolation, event wiring, `InternalsVisibleTo`, Bootstrapper registration; what only the whole solution shows |
| **trace-auditor** | opus · 40 | AC list, AC → test matrix, test sources, `Shared.Abstractions` | any production code | `findings` type WEAK_ASSERTION + `ac_assessment[]` (STRONG / WEAK / MISSING per AC): does each test really assert status, exact message, DB state, events, role path? |

#### Not part of any story

| Agent | Purpose |
|---|---|
| **fleet-smoke-blind** (haiku · 30) | A probe that executes a list of steps and records which were allowed and which the ACL hook blocked. Used to verify the hook's isolation (substrate step S10). |

### Enforcement mechanisms

| Rule | Mechanism |
|---|---|
| A worker reads and writes only its allowed paths | `tools/fleet/acl-hook.js`, a `PreToolUse` hook registered in `.claude/settings.json` for Read / Grep / Glob / Edit / Write / Bash / PowerShell. Reads `agent_type` from the hook stdin and allowlists from `fleet-acl.json`. Allowlist model, fail-closed, paths canonicalised for Windows. A Grep or Glob without a path means the whole repo and is denied. |
| No agent can push, commit, reset or touch the database | `deny_always` in `fleet-acl.json`: `git push`, `git commit`, `git reset`, `git checkout --`, `dotnet ef database update`, `rm -rf`, `Remove-Item -Recurse`. |
| Blind agents cannot escape through a shell | No Bash / PowerShell in their `tools`; writers get only exact command prefixes (`shell` list per agent). |
| Workers do not see orchestration or this file | `omitClaudeMd: true` in every worker's frontmatter; the needed conventions are listed in the prompt template instead. |
| The conductor cannot "help" by pasting context | Prompts are built only by `prompt-builder.js` from a template plus a list of input files with hashes; the agent receives the **path** to the prompt file. Refuses inputs outside the agent's `read_allow`, agents without an ACL entry, and overwriting an existing prompt without a new `--instance`. |
| Results have a fixed shape | Every output is validated with `validate.js` against a JSON Schema; the referenced schemas are embedded in the prompt. |
| Loops end | `maxTurns` per agent, three repair rounds per loop, and the rule “blockers must shrink” in `dedup-findings.js`. |
| Owner decisions cannot be fabricated | `owner-answer-hook.js`, a `PostToolUse(AskUserQuestion)` hook, appends the raw answer to `runs/<story>/journal.jsonl` as `OWNER_ANSWER`. `decide.js` is the only writer of `decided_by: owner` and references that entry. |
| Hard-list assumptions do not slip through | `dod.js` fails on any assumption with `score.hard_list: true` that has no linked decision. |
| Acceptance tests are not bent to fit the code | `red-first-prover.js` stores a hash of the blind pass's test files; `gate.js` recomputes it on every run. A changed or deleted file is RED unless a `REBASELINE` journal entry explains it. |
| A risky migration cannot pass unnoticed | `migration-check.js` classifies every statement; `gate.js` runs the `migration` step first and refuses without a current report and an owner answer bound to the migration's `migration_sha`. |
| Stale artifacts are rejected | Provenance hashes (`hash.js`, CRLF-normalised) are compared by every consumer; `status.js` shows what needs recomputing. |

### Scripts — `tools/fleet/`

Deterministic Node ≥ 20 (ESM) scripts, no LLM inside. Install once with `npm install` in `tools/fleet/`; run from the repository root as `node tools/fleet/<script>.js --story <NNN-slug> …`. Exit code 0 is always "fine"; non-zero codes are listed per script. Statuses and the full defect journal: `tools/fleet/README.md`.

#### Hooks and guards

| Script | What it does | Input → output | Exit |
|---|---|---|---|
| `acl-hook.js` | The `PreToolUse` ACL hook described above. | hook stdin JSON → allow / deny | 0 allow · 2 deny |
| `acl-hook.test.js` | ~100 hook cases plus a static check that every template's agent has an ACL entry covering `runs/*/prompts`. `npm run acl:test`. | — | 0 / 1 |
| `fleet-acl.json` | The allowlists: `read_allow`, `write_allow`, `shell` prefixes per `agent_type`, global `deny_always`. Schema: `schemas/fleet-acl.schema.json`. | configuration | — |
| `owner-answer-hook.js` | The `PostToolUse(AskUserQuestion)` hook; picks the story from the current branch and appends `OWNER_ANSWER` to its journal (to `runs/_unassigned/` before a story branch exists). | hook stdin → `journal.jsonl` | 0 |

#### Prompts, decisions, journal, validation

| Script | What it does | Input → output | Exit |
|---|---|---|---|
| `prompt-builder.js` | Builds an agent prompt: template front matter (`agent`, `phase`, `inputs` with `?` for optional, `conventions`, `output_schema`, `max_turns`) + input file list with sha256 + embedded output schema (with `$ref`s resolved) + provenance block. `--template`, `--instance`, `--var k=v`. | `templates/<name>.md` → `runs/<story>/prompts/<name>[.<instance>].{md,json}` | 0 · 3 refused |
| `templates/*.md` | Nineteen templates: `capability-analyst`, `scope-critic`, `premortem-critic`, `code-researcher`, `impact-analyst`, `be-writer-skeleton`, `be-writer-behaviour`, `be-writer-fix`, `fe-writer-behaviour`, `fe-writer-fix`, `test-designer`, `test-designer-sighted`, `test-designer-fix`, `review-spec-conformance`, `review-security-authz`, `review-maintainability`, `review-arbiter`, `architecture-guard`, `trace-auditor`. | — | — |
| `decide.js` | The only writer of `decided_by: owner`. Takes the chosen option from the latest `OWNER_ANSWER` (`--answer-ref latest` or a `tool_use_id`), rejects options outside the list. `--decision DEC-n`, `--veto ASM-n --replacement DEC-m`, `--gate G1|G2|G2b|G3 --status PASSED|REJECTED` (G2b additionally requires an answer newer than the migration report that names the migration sha). | journal → `decisions.jsonl` / `assumptions.jsonl`, `GATE_ANSWER` / `DECISION` events | 0 · 3 |
| `journal.js` | The conductor's only way to write to the journal: `AGENT_START` / `AGENT_END` (tokens, duration from the Agent tool), `PHASE_START` / `PHASE_END`, `NOTE`, `REBASELINE`. Refuses owner and gate events. | → `runs/<story>/journal.jsonl` | 0 · 1 |
| `validate.js` | Validates a JSON or JSONL file (or stdin) against a schema in `docs/agent-fleet-v4/schemas/` (ajv, draft 2020-12). | `--schema <name> --file <path>` | 0 · 3 |
| `hash.js` | Shared hashing (`hash_version: 2`): text normalised CRLF → LF, binaries as-is. Used by `prompt-builder`, `dedup-findings`, `red-first-prover`, `gate`, `status`. | library | — |
| `ac-ids.js` | Shared helper that extracts the story's AC ids from `story.md` front matter (fallback: `**AC-n**` in `spec.md`). Used by `gate`, `red-first-prover`, `coverage-gaps`, `ac-matrix`. | library | — |

#### Session A / B

| Script | What it does | Input → output | Exit |
|---|---|---|---|
| `capability-map.js` | Builds the application's capability map: endpoints per module from the exported swagger, screens from `*.routes.ts`, modules from `/modules`. The only picture of the app the business session sees. `--refresh` runs the swagger export test (needs Docker). | `generated/swagger.base.json`, `generated/modules.json` → `generated/capability-map.json` | 0 · 1 |
| `backlog-writer.js` | Validates the `story.md` front matter against the `story` schema and renders `issue-body.md` (the dry run shown to the owner before the GitHub issue is created). | `runs/<story>/story.md` → `runs/<story>/issue-body.md` | 0 · 3 |
| `contract-compose.js` | Composes the story's OpenAPI Overlay 1.0 with the base swagger into a full OpenAPI document; requires `x-evidence: F-n` on actions touching existing routes. | `contract-delta.overlay.json` + `swagger.base.json` → `contract-next.json` | 0 · 3 |
| `status.js` | Computes `state.json` from artifacts and the journal: phase, gates, hashes, open decisions, hard-list assumptions without decisions, blockers. Refuses when the branch is not the story or a stray `.specify/feature.json` exists. Never written by an agent. | → `runs/<story>/state.json` | 0 · 1 |

#### Session C — phases C1 to C4b

| Script | Phase | What it does | Input → output | Exit |
|---|---|---|---|---|
| `baseline.js` | C1 | Records the commit the story's code starts from; `skeleton-check` and `coverage-gaps` diff against it (merge-base with `main` produced false positives from fleet commits on the story branch). Requires a clean `backend/` tree. | → `runs/<story>/baseline.json` | 0 · 3 |
| `skeleton-check.js` | C3a | Verifies the skeleton run changed only data shapes: Domain, DAL, ReadModels, Exceptions, Migrations, `Shared.Abstractions`, command/query/result records. Handlers, validators, Api, frontend or tests = violation. | git diff vs baseline → `runs/<story>/skeleton-check.json` | 0 · 3 |
| `migration-check.js` | after the migration | Classifies every `migrationBuilder` statement of migrations added since baseline into SAFE / REVIEW / DESTRUCTIVE with context (table created in the same `Up`, `Down` capped at REVIEW, raw `Sql(` in `Up` = DESTRUCTIVE, anything unparseable = REVIEW). Compares with the writer's `schema_changes` forecast. REVIEW or DESTRUCTIVE opens G2b (`GATE_OPEN` with `migration_sha`). | → `runs/<story>/migration-check.json` | 0 clear · 5 G2b required · 1 usage / model changed without migration |
| `migration-check.test.js` | — | 63 tests: scrubber, buckets, a golden set of the repo's own migrations, 016 fixtures, CLI paths. `npm run migration:test`. | — | 0 / 1 |
| `red-first-prover.js` | C2 | Builds the test projects, runs the story's acceptance tests (`--filter AC=…`); every one must fail. Saves the list and hash of acceptance-test files. `--ac` to exclude criteria satisfied by the skeleton alone. | → `runs/<story>/tests/red-first.json` | 0 RED · 3 NOT_RED / NO_TESTS / BUILD_FAILED |
| `ac-matrix.js` | C2, gate | AC → tests matrix from `[Trait("AC","AC-n")]` (backend) and `it("[AC-n] …")` (frontend), optionally with TRX results. | story AC ids + test sources → `runs/<story>/ac-matrix.json` | 0 · 3 uncovered AC |
| `gate.js` | C3b, C4b, C5 | The quality gate, in order: `migration` (current report + matching G2b answer) → `dotnet build` → `dotnet format --verify-no-changes` → arch-tests (SKIPPED until the analyzer epic) → story tests (or `--full`) → `fe-build` + `fe-test` (when `touches_ui` or anything under `frontend/` changed since baseline) → swagger re-export → `contract-diff` → `ac-matrix` → acceptance-test hash vs `red-first.json`. | → `runs/<story>/gate.json` | 0 GREEN · 3 RED |
| `contract-diff.js` | gate | Drift between the agreed contract and the implementation: missing or unrequested operations, response-code differences, breaking changes via `openapi-diff`. | `contract-next.json` vs re-exported `swagger.base.json` → `runs/<story>/contract-diff.json` | 0 CLEAR · 3 DRIFT |
| `coverage-gaps.js` | C4b | Changed production lines since baseline ∩ uncovered lines / partial branches from the Cobertura reports of all test projects. Input for the sighted test pass. | → `runs/<story>/coverage-gaps.json` | 0 · 3 gaps · 1 no coverage files |

#### Session C — review and close (C5, C6)

| Script | Phase | What it does | Input → output | Exit |
|---|---|---|---|---|
| `review-diff.js` | C5 | Produces the round's input: round 1 = baseline → working tree, round ≥ 2 = previous round's snapshot → current tree (via a temporary git index, no commit). Excludes `*.Designer.cs` and `*ModelSnapshot.cs`. | → `runs/<story>/review/round-N/{diff.patch, scope.json}` | 0 · 3 empty diff |
| `dedup-findings.js` | C5, C6 | Merges the round's `<reviewer>.findings.json`: schema validation, rejection of stale files (provenance) and of round ≥ 2 files without `previous_findings_status`, downgrade without `rule_ref` → OPINION, duplicate merging (same file, ±3 lines, same rule), a `route` (writer / tester / owner) for every BLOCKER and MAJOR, verdict. `--dir closing` for C6. | → `review/round-N/dedup.json` | 0 CLEAN · 3 FIX_REQUIRED · 4 ESCALATE · 5 DECISIONS_REQUIRED |
| `close-finding.js` | C5, C6 | Closes one finding with the provenance of an owner decision (`DEC-n`) or an arbiter `OVERTURN`; writes `decision_id` / `closed_by` into the reviewer file and `dedup.json`, logs `FINDING_CLOSED`. | → findings files, `dedup.json`, journal | 0 · 3 |
| `tick-tasks.js` | C6 | Ticks `tasks_completed` from every DONE `impl/*/result.json` in `tasks.md`, plus conductor / owner tasks via `--also`; only when the gate is GREEN. | → `specs/<story>/tasks.md` | 0 · 3 |
| `dod.js` | C6 | Definition of Done as a script: gate GREEN, AC coverage 100 %, no open decisions, no hard-list assumptions without a decision, 0 BLOCKER, no open disputes, every UNSPECIFIED test decided, contract CLEAR, tasks ticked, acceptance-test hash intact. | → `runs/<story>/dod.json` | 0 DONE · 3 NOT_DONE |
| `closer.js` | C6 | Writes `metrics.json` (interruptions, decisions, assumptions, repair rounds, tokens, wall clock, rule candidates) and `close-report.md` with the checklist for the GitHub issue; logs `PHASE_END C6`. Produces the package **for** G3, not after it. | → `runs/<story>/{metrics.json, close-report.md}` | 0 · 3 |

#### Generated inputs produced by xUnit "Tooling" tests

Two tests in `backend/src/Shared/ThingsBooksy.Shared.IntegrationTests` (category `Tooling`, run with `dotnet test … --filter Category=Tooling`, need Docker for Testcontainers) produce the files the fleet reads instead of source code:

| Test | Output | Used by |
|---|---|---|
| `SwaggerExportTests` | `generated/swagger.base.json` (the running app's OpenAPI), `generated/modules.json` | `capability-map`, `contract-compose`, `contract-diff`, `gate`, the TypeScript client generator |
| `CoreSurfaceExportTests` | `generated/core-surface.json` — reflection dump of every module's entities, read models, DbContexts, `DbSet`s, `Create` signatures, setters; no logic | the blind `test-designer` |

### Where everything lives

```
.claude/
  agents/*.md                  18 agent definitions (frontmatter: model, effort, tools, omitClaudeMd, maxTurns, initialPrompt)
  settings.json                project hooks: PreToolUse → acl-hook.js, PostToolUse(AskUserQuestion) → owner-answer-hook.js; enabled plugins
  conventions/*.md             the written rules reviewers cite (backend + angular-*); linked from prompt templates
  skills/speckit-*/            SpecKit skills; the fleet uses plan, tasks, clarify (lint), analyze (lint) — never specify or implement
  commands/git.md, run-tests.md   legacy slash helpers for a human session, not part of the fleet
CLAUDE.md                      an index for humans and the main session; workers never see it (omitClaudeMd)
.specify/
  memory/constitution.md       articles I–XIV, the rule_ref target "constitution#<article>" in findings
  templates/*.md               SpecKit templates for spec / plan / tasks
  scripts/powershell/          SpecKit helper scripts (PowerShell only; run via powershell.exe -File)
  extensions.yml, extensions/git, .github/agents + .github/prompts   SpecKit git extension (Copilot-format files); not used by the fleet
  decisions/ADR-*.md           architecture decision records from before v4
  agent-fleet-redesign.md      historical v3 proposal, superseded by docs/agent-fleet-v4/
tools/fleet/                   the substrate: scripts, templates, ACL, package.json (npm run acl:test | migration:test), README with status + defect journal
docs/agent-fleet-v4/
  HANDOFF.md                   current state for a new session (read first)
  NEXT-SESSION.md, next-session-fe-writer.md   hand-over notes of the last fleet-building sessions
  workflow.md                  the design: principles, sessions, agent table, artifacts, mechanisms, substrate list, platform findings
  decisions.md                 owner decisions D-1 … D-15 (below)
  runbook-delivery.md          the conductor's phase-by-phase procedure for session C
  schemas/*.schema.json        12 JSON Schemas + README with producer → consumer → validator
  prototypes/                  the 2026-09-24 platform prototypes (hook, stdin logger, example frontmatters)
  reviews/, handoff-2026-10-02/   independent critiques of the design and of later plans
docs/backlog/                  deferred topics: static analysis / SonarQube epic, OPINION items from story 015
generated/                     capability-map.json, swagger.base.json, modules.json, core-surface.json — never edited by hand, committed
specs/<story>/                 spec.md, plan.md, tasks.md (+ data-model, contracts) — immutable once the story is closed
runs/<story>/                  everything the fleet produced for the story — committed as provenance
.husky/                        pre-commit: dotnet format on staged .cs files (independent of the fleet)
```

#### `runs/<story>/` in detail

| Path | Written by | Content |
|---|---|---|
| `story.md` | scrum | front matter per `story` schema + narrative; the single source of AC ids |
| `proposal.md`, `questions/<n>.md`, `capability-report.jsonl`, `critique/<critic>.json`, `issue-body.md`, `epic.md` | scrum / its workers / `backlog-writer` | session A trail |
| `journal.jsonl` | hooks and scripts only | append-only event log: `OWNER_ANSWER`, `GATE_OPEN`, `GATE_ANSWER`, `DECISION`, `FINDING_CLOSED`, `AGENT_START/END`, `PHASE_START/END`, `NOTE`, `REBASELINE` |
| `state.json` | `status.js` | computed state, never hand-written |
| `discovery/questions/<n>.md`, `discovery/raw/*.json`, `discovery/facts.jsonl` | dev-analyst / code-researcher / impact-analyst | questions and verbatim facts (`F-n`) |
| `discovery/decisions.jsonl`, `discovery/assumptions.jsonl` | dev-analyst, writers (via the conductor), `decide.js` | `DEC-n` and `ASM-n` rows; `decided_by: owner` only from `decide.js` |
| `discovery/round-N.md`, `discovery/ui-sketch.md`, `discovery/verdict.json` | dev-analyst | round dumps (compaction-proof), UI sketch, early-exit verdict |
| `contract-delta.overlay.json` → `contract-next.json` → `contract-diff.json` | dev-analyst → `contract-compose` → `contract-diff` | API contract as overlay, composed OpenAPI, drift report |
| `prompts/<template>[.<instance>].{md,json}` | `prompt-builder` | every prompt ever given to an agent, with input hashes and `run_id` |
| `impl/<agent>.<phase>/result.json` | the conductor, from the agent's returned JSON | writer / tester results (`result` schema) |
| `baseline.json`, `skeleton-check.json`, `migration-check.json`, `tests/red-first.json`, `ac-matrix.json`, `gate.json`, `coverage-gaps.json` | the respective scripts | phase evidence |
| `review/round-N/{diff.patch, scope.json, <reviewer>.findings.json, dedup.json, disputes/<id>.json, arbiter.json}` | `review-diff`, the conductor, `dedup-findings`, arbiter | review rounds |
| `closing/<agent>.findings.json`, `closing/dedup.json` | the conductor, `dedup-findings` | C6 guard results |
| `dod.json`, `metrics.json`, `close-report.md` | `dod`, `closer` | Definition of Done and the closing package for G3 |

### Schemas — `docs/agent-fleet-v4/schemas/`

| Schema | Produced by | Consumed by | Rejected when |
|---|---|---|---|
| `provenance` | every agent (injected by `prompt-builder`) | every script | an input hash differs from the current file |
| `story` | scrum | `backlog-writer`, dev-analyst, `ac-ids` | required fields missing (checked before the GitHub issue) |
| `critique` | scope-critic, premortem-critic | owner (unfiltered), scrum | weight ≥ 3 without evidence is downgraded to 2 |
| `fact` | code-researcher, capability-analyst, impact-analyst | dev-analyst, scrum | — (`confidence: unknown` is allowed) |
| `decision` | dev-analyst, writers (`decisions_needed`), arbiter (`escalation`) | gates G2 / G3, `dod` | `decided_by` not backed by a journal `OWNER_ANSWER` |
| `assumption` | dev-analyst, writers | G2 / G3 package, `dod` | `score.hard_list: true` without a linked decision |
| `result` | be-writer, fe-writer, test-designer, test-designer-sighted | conductor, `gate`, `tick-tasks`, `closer` | schema error; `files_changed` outside the agent's `write_allow` |
| `findings` | reviewers, architecture-guard, trace-auditor | `dedup-findings` → writer / tester / owner / arbiter | severity above OPINION without `rule_ref`; round ≥ 2 without `previous_findings_status` |
| `arbiter-verdict` | review-arbiter | conductor, writer, G3 | `ESCALATE` without an `escalation` question |
| `fleet-acl` | humans | `acl-hook`, `prompt-builder`, `acl-hook.test` | — |
| `state` | `status.js` | conductor, owner | never written by an agent |
| `journal-event` | hooks and scripts | `status`, `closer`, resume | append-only |

### Owner decisions that shape the fleet (D-1 … D-15)

Full text and rationale: `docs/agent-fleet-v4/decisions.md`.

| # | Decision |
|---|---|
| D-1 | Hard list always asked of the owner: authorization & security · data & migrations · new UI outside the story. Enforced by `dod.js` and the `migration` gate step. |
| D-2 | Three triage buckets (ASK / ASSUME / DO NOT ASK); fixed question format ending with the strongest counter-argument; `decided_by` written only by a script. |
| D-3 | Build order: substrate scripts first, agent prompts second. Code analyzers / SonarQube deferred to a separate epic. |
| D-4 | Acceptance tests are written by a separate tester agent; the implementer reads them but cannot edit them (ACL + hash). Integration test style: Arrange = EF + factories, Act = HTTP, Assert = EF re-read. |
| D-4a | Delivery order skeleton → blind tester → behaviour; the migration is generated by the developer between skeleton and tests, never by an agent. |
| D-4b | Two tester passes: blind (requirements) and sighted (coverage gaps); every sighted test tagged `AC-n` or `UNSPECIFIED` (owner decision). |
| D-4c | The blind tester gets `dotnet build` limited to test projects, so it does not loop blindly against the red-first prover. |
| D-5 | `dev-analyst` writes `spec.md` itself; `/speckit-clarify` and `/speckit-analyze` are lint only; `/speckit-implement` never. |
| D-6 | Tasks stay in `tasks.md`; GitHub holds Epic + Story issues only. |
| D-7 | UI design = `ui-sketch.md` in discovery, no external design tool in the pipeline. |
| D-8 | One story at a time; parallelism only inside a story. |
| D-9 | Finding severities BLOCKER / MAJOR / MINOR / OPINION as defined in the glossary. |
| D-10 | API contract changes as OpenAPI Overlay 1.0 over the exported swagger; the owner sees the composed endpoint diff. |
| D-11 | Story content at G1: the full `story` schema (AC with ids, out of scope, rejected alternatives, blast radius, dependencies, capability-map version). |
| D-12 | Substrate = Node ESM scripts in `tools/fleet/` with their own `package.json`. |
| D-13 | One project-level `PreToolUse` hook in `.claude/settings.json` reading `agent_type`; agents without an ACL entry and the main session are unconstrained (fail-closed for listed agents only). |
| D-14 | AC ↔ test link via `[Trait("AC", "AC-n")]` (xUnit) and the `[AC-n]` prefix in `it(...)` names (Vitest). |
| D-15 | `swagger.base.json` is produced by an xUnit test over `ThingsBooksyWebAppFactory` (Testcontainers), no new tooling. |

### State of the fleet (2026-10-07)

Honest status, so nobody mistakes the design for the implementation. The living version is `docs/agent-fleet-v4/HANDOFF.md`; the defect journal with every real-run failure and its fix is in `tools/fleet/README.md`.

- **Proven on a real story:** session A (story 016), session B (story 015), session C phases C1–C6 (story 015, closed with DoD 10/10 and G3 PASSED).
- **Built, not yet proven on a real story:** `fe-writer` v0 (trial run on a toy task only; first real run is story 016), `review-arbiter` (no dispute has happened yet), `migration-check.js` (63 tests + a synthetic story; first real run on 016).
- **Does not exist yet:** a `/deliver` skill (the conductor is a human-driven main session following the runbook), `write_deny` in the ACL hook (`fe-writer` is kept away from `*.spec.ts` by its prompt and the gate's test hash), a `spec-critic`, the `{Module}.Tests.Unit` projects, a script around the TypeScript client generator (`swagger-typescript-api` is a devDependency, the command is run by hand), `npm run lint` in the frontend, CI.
- **Known gap:** an agent file in `.claude/agents/` without an ACL entry runs unconstrained in the hook; `prompt-builder` refuses to build a prompt for it, so the gap is real only for a manual spawn. Fail-closed is planned (change to D-13).
- **Dropped by the owner:** `plan-guard`, `migration-reviewer` (replaced by `migration-check.js`), `docs-delta` (the capability map is generated), `rule-harvester` (rule candidates go to `docs/backlog/` by hand). References to them in `workflow.md` are design history.

### Running it

Prerequisites: .NET 10 SDK, Node ≥ 20 (`npm install` in `tools/fleet/`), Docker reachable from PowerShell (Testcontainers for integration tests, swagger and core-surface export), the frontend installed (`npm install` in `frontend/`), Claude Code with the GitHub MCP for issue creation.

```powershell
# Session A — shape a story (from the branch the story should grow from, usually main; clean tree)
claude --agent scrum

# Session B — technical discovery (on the story branch created by scrum)
claude --agent dev-analyst

# Session C — delivery (plain session on the story branch; follow docs/agent-fleet-v4/runbook-delivery.md)
claude
node tools/fleet/status.js --story <NNN-slug>     # the first command of every delivery session
```

Reading order for a newcomer: this section → `docs/agent-fleet-v4/HANDOFF.md` → `workflow.md` → `decisions.md` → `runbook-delivery.md` → `tools/fleet/README.md`. A closed, fully journaled example is `runs/015-resource-time-buffer/`.
