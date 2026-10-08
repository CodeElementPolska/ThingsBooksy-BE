#!/usr/bin/env node
// rehash-acceptance — prints the current acceptance_tests_hash over the file list recorded in
// runs/<story>/tests/red-first.json, with the same algorithm the gate uses (hash.js, hash_version 2).
// Use after an owner-authorised REBASELINE edit of blind-pass test files: the conductor puts the
// printed hash into the journal REBASELINE entry; the gate's test-hash step accepts a journal
// REBASELINE that contains the current hash. red-first-prover itself is not rerun, because after
// the behaviour phase it would report NOT_RED and overwrite the red proof.
// Usage: node tools/fleet/rehash-acceptance.mjs --story NNN-slug
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { sha256Files, HASH_VERSION } from './hash.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, arr) => a.startsWith('--') ? [a.slice(2), arr[i + 1] && !arr[i + 1].startsWith('--') ? arr[i + 1] : true] : []).filter(Boolean));
if (!args.story) { console.error('usage: rehash-acceptance --story NNN-slug'); process.exit(1); }
const rf = path.join(REPO, 'runs', args.story, 'tests', 'red-first.json');
const redFirst = JSON.parse(fs.readFileSync(rf, 'utf8'));
if (!Array.isArray(redFirst.acceptance_test_files)) { console.error('rehash-acceptance: red-first.json has no acceptance_test_files'); process.exit(3); }
if ((redFirst.hash_version || 1) !== HASH_VERSION) { console.error(`rehash-acceptance: red-first.json hash_version ${redFirst.hash_version} != ${HASH_VERSION}`); process.exit(3); }
const files = redFirst.acceptance_test_files.map(rel => path.join(REPO, rel));
const missing = files.filter(f => !fs.existsSync(f));
if (missing.length) { console.error('rehash-acceptance: blind-pass test file(s) removed: ' + missing.map(f => path.relative(REPO, f)).join(', ')); process.exit(3); }
const actual = sha256Files(REPO, files, f => path.relative(REPO, f).replace(/\\/g, '/'));
console.log(`rehash-acceptance: ${files.length} files, recorded ${redFirst.acceptance_tests_hash} → current ${actual}${actual === redFirst.acceptance_tests_hash ? ' (unchanged)' : ''}`);
process.stdout.write(actual + '\n');
