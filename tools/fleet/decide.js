#!/usr/bin/env node
// decide — the ONLY writer of `decided_by: owner` (schemas/decision.schema.json, D-2).
// Marks a decision in runs/<story>/discovery/decisions.jsonl as DECIDED by pointing at an OWNER_ANSWER
// event in journal.jsonl (tool_use_id from the AskUserQuestion hook, or prompt_id for a typed answer).
// The chosen option is taken from the journal, not from the caller — an agent cannot fabricate it.
// Usage: node tools/fleet/decide.js --story NNN-slug --decision DEC-3 --answer-ref <tool_use_id|prompt_id> [--question "<exact question text>"]
//        node tools/fleet/decide.js --story NNN-slug --veto ASM-2 --answer-ref <id> --replacement DEC-9
// Exit 0 · 3 not found / mismatch · 1 usage
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
const { story } = args;
if (!story || !args['answer-ref'] || !(args.decision || args.veto || args.gate)) { console.error('usage: decide --story NNN-slug (--decision DEC-n | --veto ASM-n --replacement DEC-m | --gate G1|G2|G2b|G3 --status PASSED|REJECTED) --answer-ref <id> [--question text]'); process.exit(1); }
const runDir = path.join(REPO, 'runs', story);
const journalPath = path.join(runDir, 'journal.jsonl');
const journal = fs.existsSync(journalPath) ? fs.readFileSync(journalPath, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) : [];
// --answer-ref latest : the most recent OWNER_ANSWER (the persona does not see its own tool_use_id;
// the hook wrote it seconds ago). Any other value must match tool_use_id or prompt_id exactly.
const ans = args['answer-ref'] === 'latest'
  ? [...journal].reverse().find(e => e.event === 'OWNER_ANSWER')
  : journal.find(e => e.event === 'OWNER_ANSWER' && (e.owner_answer?.tool_use_id === args['answer-ref'] || e.owner_answer?.prompt_id === args['answer-ref']));
if (!ans) { console.error(`decide: no OWNER_ANSWER with ref ${args['answer-ref']} in ${path.relative(REPO, journalPath)}`); process.exit(3); }
args['answer-ref'] = ans.owner_answer.tool_use_id || ans.owner_answer.prompt_id;

const rw = (file, fn) => { const p = path.join(runDir, 'discovery', file); const rows = fs.existsSync(p) ? fs.readFileSync(p, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) : []; const out = fn(rows); fs.mkdirSync(path.dirname(p), { recursive: true }); fs.writeFileSync(p, out.map(r => JSON.stringify(r)).join('\n') + '\n'); };

if (args.gate) {
  // close a gate with the owner's answer as provenance; the status is explicit (no guessing from words)
  if (!['G1', 'G2', 'G2b', 'G3'].includes(args.gate) || !['PASSED', 'REJECTED'].includes(args.status)) { console.error('decide: --gate needs G1|G2|G2b|G3 and --status PASSED|REJECTED'); process.exit(1); }
  fs.appendFileSync(journalPath, JSON.stringify({ at: new Date().toISOString(), event: 'GATE_ANSWER', phase: args.gate, status: args.status, owner_answer: { tool_use_id: ans.owner_answer.tool_use_id, answers: ans.owner_answer.answers } }) + '\n');
  console.log(`decide: gate ${args.gate} ${args.status} (ref ${args['answer-ref']})`);
} else if (args.decision) {
  let found = false;
  rw('decisions.jsonl', rows => rows.map(d => {
    if (d.id !== args.decision) return d;
    found = true;
    const answers = ans.owner_answer.answers || {};
    const key = args.question || Object.keys(answers).find(q => q === d.question) || Object.keys(answers)[0];
    const chosen = answers[key] ?? ans.owner_answer.text;
    if (chosen === undefined) { console.error(`decide: answer ${args['answer-ref']} has no value for question "${key}"`); process.exit(3); }
    if (d.options?.length && !d.options.some(o => o.label === chosen)) { console.error(`decide: chosen "${chosen}" is not one of the decision's options (${d.options.map(o => o.label).join(' | ')}) — use --question to disambiguate`); process.exit(3); }
    return { ...d, status: 'DECIDED', chosen, decided_by: 'owner', answer_ref: args['answer-ref'], decided_at: ans.at };
  }));
  if (!found) { console.error(`decide: ${args.decision} not found in decisions.jsonl`); process.exit(3); }
  // a decision is NOT a gate: gates are closed only by `decide --gate G2 --status PASSED|REJECTED`
  fs.appendFileSync(journalPath, JSON.stringify({ at: new Date().toISOString(), event: 'DECISION', status: 'DECIDED', reason: args.decision, owner_answer: { tool_use_id: ans.owner_answer.tool_use_id } }) + '\n');
  console.log(`decide: ${args.decision} DECIDED by owner (ref ${args['answer-ref']})`);
} else {
  if (!args.replacement) { console.error('decide: --veto requires --replacement DEC-n'); process.exit(1); }
  let found = false;
  rw('assumptions.jsonl', rows => rows.map(a => a.id === args.veto ? (found = true, { ...a, veto: { answer_ref: args['answer-ref'], replacement_decision_id: args.replacement } }) : a));
  if (!found) { console.error(`decide: ${args.veto} not found in assumptions.jsonl`); process.exit(3); }
  console.log(`decide: ${args.veto} vetoed by owner → ${args.replacement}`);
}
