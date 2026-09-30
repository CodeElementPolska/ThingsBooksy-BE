#!/usr/bin/env node
// journal — append ONE event to runs/<story>/journal.jsonl (schemas/journal-event.schema.json) from the
// conductor: AGENT_START/AGENT_END with tokens and duration (the Agent tool's task notification carries
// them), PHASE_START/PHASE_END, NOTE (e.g. tasks.md "record UNSPECIFIED: … in the journal"), REBASELINE.
// Owner answers and gates are NOT written here — only the hook and decide.js may (D-2 provenance).
// Usage: node tools/fleet/journal.js --story NNN-slug --event AGENT_END --agent test-designer --run-id <id>
//          [--phase C2] [--status DONE] [--tokens 162445] [--duration-ms 538645] [--reason "…"] [--artifact path]
// Exit 0 · 1 usage/refused
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const { story, event } = args;
const ALLOWED = ['PHASE_START', 'PHASE_END', 'AGENT_START', 'AGENT_END', 'ARTIFACT_WRITTEN', 'ARTIFACT_REJECTED', 'REBASELINE', 'ESCALATION', 'BUDGET_STOP', 'ERROR', 'NOTE'];
if (!story || !event) { console.error('usage: journal --story NNN-slug --event <event> [--agent] [--run-id] [--phase] [--status] [--tokens] [--duration-ms] [--reason] [--artifact]'); process.exit(1); }
if (!ALLOWED.includes(event)) { console.error(`journal: event ${event} is not one the conductor may write (${ALLOWED.join(', ')}); OWNER_ANSWER/GATE_ANSWER/DECISION belong to the hook and decide.js`); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
if (!fs.existsSync(runDir)) { console.error(`journal: no ${path.relative(REPO, runDir)}`); process.exit(1); }
const e = { at: args.at || new Date().toISOString(), event };
if (args.phase) e.phase = args.phase;
if (args.agent) e.agent_type = args.agent;
if (args['run-id']) e.run_id = args['run-id'];
if (args.status) e.status = args.status;
if (args.artifact) e.artifact = args.artifact;
if (args.reason) e.reason = args.reason;
if (args.tokens) e.tokens = +args.tokens;
if (args['duration-ms']) e.duration_ms = +args['duration-ms'];
fs.appendFileSync(path.join(runDir, 'journal.jsonl'), JSON.stringify(e) + '\n');
console.log(`journal: ${event}${e.agent_type ? ' ' + e.agent_type : ''}${e.tokens ? ` (${e.tokens} tokens)` : ''} → runs/${story}/journal.jsonl`);
