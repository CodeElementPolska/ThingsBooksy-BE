#!/usr/bin/env node
// Owner-answer hook (PostToolUse, matcher "AskUserQuestion") — registered project-wide in .claude/settings.json,
// so it fires for the personas (dev-analyst, scrum) AND for the main session acting as conductor.
// Only an AskUserQuestion answer reaches the journal — an answer typed in the chat does not. Copies the owner's
// answer verbatim into runs/<story>/journal.jsonl
// as an OWNER_ANSWER event (schemas/journal-event.schema.json). This is the ONLY source a script may
// use to mark a decision as `decided_by: owner` (D-2 provenance). Never blocks (exit 0).
// Story resolution: env FLEET_STORY, else the current git branch when it matches NNN-slug, else
// runs/_unassigned/journal.jsonl (the conductor re-files it when the story is known).
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
let raw = ''; try { raw = fs.readFileSync(0, 'utf8'); } catch { process.exit(0); }
let input; try { input = JSON.parse(raw); } catch { process.exit(0); }
if (input.tool_name !== 'AskUserQuestion') process.exit(0);

let story = process.env.FLEET_STORY;
if (!story) { const b = (spawnSync('git', ['rev-parse', '--abbrev-ref', 'HEAD'], { cwd: REPO, encoding: 'utf8' }).stdout || '').trim(); if (/^\d{3}-[a-z0-9-]+$/.test(b)) story = b; }
const dir = path.join(REPO, 'runs', story || '_unassigned');
fs.mkdirSync(dir, { recursive: true });

const resp = input.tool_response || {};
const event = {
  at: new Date().toISOString(),
  event: 'OWNER_ANSWER',
  agent_type: input.agent_type || 'main',
  owner_answer: {
    source: 'AskUserQuestion',
    tool_use_id: input.tool_use_id,
    prompt_id: input.prompt_id,
    questions: (input.tool_input?.questions || []).map(q => ({ header: q.header, question: q.question, options: (q.options || []).map(o => o.label) })),
    answers: resp.answers || {},
    annotations: resp.annotations || {},
  },
};
fs.appendFileSync(path.join(dir, 'journal.jsonl'), JSON.stringify(event) + '\n');
process.exit(0);
