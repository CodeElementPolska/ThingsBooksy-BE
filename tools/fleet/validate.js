#!/usr/bin/env node
// validate — JSON Schema validation of fleet artifacts (docs/agent-fleet-v4/schemas), ajv 2020-12.
// Usage: node tools/fleet/validate.js --schema fact --file runs/x/discovery/facts.jsonl [--jsonl]
//        echo '{...}' | node tools/fleet/validate.js --schema result
// Exit 0 valid · 3 invalid (errors on stderr) · 1 usage
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import Ajv2020 from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const SCHEMAS = path.join(REPO, 'docs', 'agent-fleet-v4', 'schemas');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
if (!args.schema) { console.error('usage: validate --schema <name> [--file path] [--jsonl]'); process.exit(1); }

const ajv = new Ajv2020({ allErrors: true, strict: false });
addFormats(ajv);
for (const f of fs.readdirSync(SCHEMAS).filter(f => f.endsWith('.schema.json'))) ajv.addSchema(JSON.parse(fs.readFileSync(path.join(SCHEMAS, f), 'utf8')));
const validate = ajv.getSchema(`fleet-v4/${args.schema}`);
if (!validate) { console.error(`validate: unknown schema "${args.schema}"`); process.exit(1); }

const text = args.file ? fs.readFileSync(path.resolve(REPO, args.file), 'utf8') : fs.readFileSync(0, 'utf8');
const docs = args.jsonl || (args.file && args.file.endsWith('.jsonl')) ? text.split('\n').filter(Boolean).map(l => JSON.parse(l)) : [JSON.parse(text)];
let bad = 0;
docs.forEach((d, i) => { if (!validate(d)) { bad++; console.error(`#${i + 1} invalid: ` + ajv.errorsText(validate.errors, { separator: '; ' })); } });
console.log(`validate: ${docs.length - bad}/${docs.length} valid against ${args.schema}`);
process.exit(bad ? 3 : 0);
