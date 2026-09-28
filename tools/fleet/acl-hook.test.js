// Standalone tests for acl-hook.js: feeds synthetic hook JSON through the script and checks exit codes.
// Run: node tools/fleet/acl-hook.test.js   (no dependencies)
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const HOOK = path.join(HERE, 'acl-hook.js');
const T = path.join(REPO, 'backend', 'src', 'Modules', 'Resources', 'ThingsBooksy.Modules.Resources.IntegrationTests');
const CORE = path.join(REPO, 'backend', 'src', 'Modules', 'Resources', 'ThingsBooksy.Modules.Resources.Core', 'Module.cs');

const ev = (agent_type, tool_name, tool_input) => ({ cwd: REPO, agent_type, tool_name, tool_input });
const A = 'fleet-smoke-blind';

const cases = [
  // no agent / unknown agent → pass
  ['main session (no agent_type) reads Core', { cwd: REPO, tool_name: 'Read', tool_input: { file_path: CORE } }, 0],
  ['unknown agent reads Core', ev('general-purpose', 'Read', { file_path: CORE }), 0],
  // read allow/deny
  ['blind reads docs', ev(A, 'Read', { file_path: path.join(REPO, 'docs', 'agent-fleet-v4', 'decisions.md') }), 0],
  ['blind reads test project', ev(A, 'Read', { file_path: path.join(T, 'IntegrationTestCollection.cs') }), 0],
  ['blind reads Core (deny)', ev(A, 'Read', { file_path: CORE }), 2],
  ['blind reads UPPERCASE fwd (deny)', ev(A, 'Read', { file_path: 'D:/PROJECTS/AI/THINGSBOOKSY-BE/BACKEND/SRC/Shared/x.cs' }), 2],
  ['blind reads UPPERCASE allowed', ev(A, 'Read', { file_path: 'D:/PROJECTS/AI/THINGSBOOKSY-BE/DOCS/agent-fleet-v4/decisions.md' }), 0],
  ['blind relative deny', ev(A, 'Read', { file_path: 'backend/src/Shared/x.cs' }), 2],
  ['blind dotdot escape (deny)', ev(A, 'Read', { file_path: 'docs/../backend/src/Shared/x.cs' }), 2],
  ['blind \\\\?\\ prefix (deny)', ev(A, 'Read', { file_path: '\\\\?\\' + CORE }), 2],
  ['blind CLAUDE.md (deny)', ev(A, 'Read', { file_path: path.join(REPO, 'CLAUDE.md') }), 2],
  ['blind grep allowed path', ev(A, 'Grep', { pattern: 'class', path: T }), 0],
  ['blind grep Modules (deny)', ev(A, 'Grep', { pattern: 'class', path: path.join(REPO, 'backend', 'src', 'Modules') }), 2],
  ['blind grep no path (deny)', ev(A, 'Grep', { pattern: 'class' }), 2],
  ['blind glob no path (deny)', ev(A, 'Glob', { pattern: '**/*.csproj' }), 2],
  ['blind glob allowed path', ev(A, 'Glob', { pattern: '**/*.cs', path: T }), 0],
  ['blind glob absolute pattern (deny)', ev(A, 'Glob', { pattern: 'D:/Projects/AI/ThingsBooksy-BE/backend/src/**/*.cs' }), 2],
  // write
  ['blind writes runs/', ev(A, 'Write', { file_path: path.join(REPO, 'runs', '001-x', 'out.json') }), 0],
  ['blind writes docs (deny)', ev(A, 'Write', { file_path: path.join(REPO, 'docs', 'x.md') }), 2],
  ['blind edits Core (deny)', ev(A, 'Edit', { file_path: CORE }), 2],
  // shell
  ['blind bash (no allowlist → deny)', ev(A, 'Bash', { command: 'echo hi' }), 2],
  ['tester bash build allowed', ev('test-designer', 'Bash', { command: 'dotnet build backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests' }), 0],
  ['tester bash build Core (allowed: build output is not source)', ev('test-designer', 'Bash', { command: 'dotnet build backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Core' }), 0],
  ['tester chained evil (deny)', ev('test-designer', 'Bash', { command: 'dotnet build backend/src/Modules/X && cat backend/src/Modules/Resources/Core/Module.cs' }), 2],
  ['tester git push (deny)', ev('test-designer', 'Bash', { command: 'git push origin main' }), 2],
  ['tester ef database update (deny)', ev('test-designer', 'Bash', { command: 'dotnet ef database update' }), 2],
  // glob-root patterns
  ['tester reads module test project via * root', ev('test-designer', 'Read', { file_path: path.join(T, 'ResourceTypes', 'X.cs') }), 0],
  ['tester reads Core via * root (deny)', ev('test-designer', 'Read', { file_path: CORE }), 2],
  ['tester writes spec.ts via ** root', ev('test-designer', 'Write', { file_path: path.join(REPO, 'frontend', 'src', 'app', 'features', 'x', 'x.component.spec.ts') }), 0],
  ['tester writes component.ts via ** root (deny)', ev('test-designer', 'Write', { file_path: path.join(REPO, 'frontend', 'src', 'app', 'features', 'x', 'x.component.ts') }), 2],
  // delivery agents
  ["be-writer writes Core", ev("be-writer", "Write", { file_path: CORE }), 0],
  ["be-writer writes Migrations (deny)", ev("be-writer", "Write", { file_path: path.join(REPO, "backend", "src", "Modules", "Resources", "ThingsBooksy.Modules.Resources.Migrations", "Migrations", "x.cs") }), 2],
  ["be-writer edits IntegrationTests (deny)", ev("be-writer", "Edit", { file_path: path.join(T, "X.cs") }), 2],
  ["be-writer reads IntegrationTests (allowed)", ev("be-writer", "Read", { file_path: path.join(T, "IntegrationTestCollection.cs") }), 0],
  ["be-writer writes Shared.Abstractions", ev("be-writer", "Write", { file_path: path.join(REPO, "backend", "src", "Shared", "ThingsBooksy.Shared.Abstractions", "Events", "Resources", "E.cs") }), 0],
  ["be-writer dotnet test allowed", ev("be-writer", "Bash", { command: "dotnet test backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests" }), 0],
  ["be-writer git commit (deny)", ev("be-writer", "Bash", { command: "git commit -m x" }), 2],
  ["be-writer ef migrations add (deny: not in allowlist)", ev("be-writer", "Bash", { command: "dotnet ef migrations add X --project y" }), 2],
  ["test-designer reads Core (deny)", ev("test-designer", "Read", { file_path: CORE }), 2],
  ["test-designer reads Shared.Abstractions", ev("test-designer", "Read", { file_path: path.join(REPO, "backend", "src", "Shared", "ThingsBooksy.Shared.Abstractions", "Events", "IEvent.cs") }), 0],
  ["test-designer writes Shared.IntegrationTests", ev("test-designer", "Write", { file_path: path.join(REPO, "backend", "src", "Shared", "ThingsBooksy.Shared.IntegrationTests", "Messaging", "R.cs") }), 0],
  ["test-designer writes Core (deny)", ev("test-designer", "Write", { file_path: CORE }), 2],
  ["sighted reads Core", ev("test-designer-sighted", "Read", { file_path: CORE }), 0],
  ["sighted writes Core (deny)", ev("test-designer-sighted", "Write", { file_path: CORE }), 2],
  // shell: a `|` inside quotes is not a pipe (xunit trait filter), a bare `|` still is
  ["tester runs the AC filter with | in quotes", ev("test-designer", "Bash", { command: 'dotnet test backend/src/Modules/Resources/X.csproj --filter "AC=AC-3|AC=AC-4"' }), 0],
  ["tester pipes to an unallowed command (deny)", ev("test-designer", "Bash", { command: "dotnet test backend/src/Modules/Resources/X.csproj | tee out.txt" }), 2],
  // C6 guards: architecture-guard sees the whole solution; trace-auditor sees tests + contracts, never production source
  ["guard reads Core", ev("architecture-guard", "Read", { file_path: CORE }), 0],
  ["guard reads writer result (deny)", ev("architecture-guard", "Read", { file_path: path.join(REPO, "runs", "015-x", "impl", "be-writer.C3b", "result.json") }), 2],
  ["auditor reads test project", ev("trace-auditor", "Read", { file_path: path.join(T, "IntegrationTestCollection.cs") }), 0],
  ["auditor reads Shared.Abstractions event", ev("trace-auditor", "Read", { file_path: path.join(REPO, "backend", "src", "Shared", "ThingsBooksy.Shared.Abstractions", "Events", "Resources", "X.cs") }), 0],
  ["auditor reads Core (deny)", ev("trace-auditor", "Read", { file_path: CORE }), 2],
  ["auditor reads ac-matrix", ev("trace-auditor", "Read", { file_path: path.join(REPO, "runs", "015-x", "ac-matrix.json") }), 0],
  // C5 reviewers: code + own previous findings only; never the writer's notes, never another reviewer's findings
  ["reviewer reads Core", ev("review-spec-conformance", "Read", { file_path: CORE }), 0],
  ["reviewer reads its prompt", ev("review-security-authz", "Read", { file_path: path.join(REPO, "runs", "015-x", "prompts", "review-security-authz.md") }), 0],
  ["reviewer reads round diff", ev("review-spec-conformance", "Read", { file_path: path.join(REPO, "runs", "015-x", "review", "round-1", "diff.patch") }), 0],
  ["reviewer reads own previous findings", ev("review-spec-conformance", "Read", { file_path: path.join(REPO, "runs", "015-x", "review", "round-1", "review-spec-conformance.findings.json") }), 0],
  ["reviewer reads other reviewer's findings (deny)", ev("review-spec-conformance", "Read", { file_path: path.join(REPO, "runs", "015-x", "review", "round-1", "review-security-authz.findings.json") }), 2],
  ["reviewer reads writer result (deny)", ev("review-spec-conformance", "Read", { file_path: path.join(REPO, "runs", "015-x", "impl", "be-writer.C3b", "result.json") }), 2],
  ["reviewer reads dedup (deny)", ev("review-maintainability", "Read", { file_path: path.join(REPO, "runs", "015-x", "review", "round-1", "dedup.json") }), 2],
  ["reviewer writes findings (deny — conductor saves)", ev("review-security-authz", "Write", { file_path: path.join(REPO, "runs", "015-x", "review", "round-1", "review-security-authz.findings.json") }), 2],
  ["reviewer shell (deny)", ev("review-security-authz", "Bash", { command: "dotnet build" }), 2],
  ["arbiter reads dispute", ev("review-arbiter", "Read", { file_path: path.join(REPO, "runs", "015-x", "review", "round-1", "disputes", "review-spec-conformance-1-2.json") }), 0],
  ["arbiter reads other findings (deny)", ev("review-arbiter", "Read", { file_path: path.join(REPO, "runs", "015-x", "review", "round-1", "review-spec-conformance.findings.json") }), 2],
  // garbage
  ['garbage input (deny)', 'not json', 2],
];

let bad = 0;
for (const [name, input, expect] of cases) {
  const r = spawnSync(process.execPath, [HOOK], { input: typeof input === 'string' ? input : JSON.stringify(input), encoding: 'utf8' });
  const ok = r.status === expect;
  if (!ok) bad++;
  console.log(`${ok ? 'OK  ' : 'FAIL'} exit=${r.status} expect=${expect} :: ${name}${r.stderr ? ' :: ' + r.stderr.trim().slice(0, 80) : ''}`);
}
console.log(`\n${cases.length - bad}/${cases.length} passed`);
process.exit(bad ? 1 : 0);
