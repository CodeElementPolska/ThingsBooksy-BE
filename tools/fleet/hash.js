// Shared hashing for provenance and the acceptance-test hash (hash_version 2).
// Text files are hashed with CRLF normalised to LF: on Windows `core.autocrlf=true` rewrites line endings on
// checkout/rebase, and the 015 run showed every input hash changing on a clean tree for that reason alone.
// Binary files (no NUL-free UTF-8 text) are hashed as-is.
import fs from 'node:fs';
import crypto from 'node:crypto';

export const HASH_VERSION = 2;

export function sha256Bytes(buf) { return crypto.createHash('sha256').update(buf).digest('hex'); }

export function normalisedBuffer(file) {
  const buf = fs.readFileSync(file);
  if (buf.includes(0)) return buf; // binary
  return Buffer.from(buf.toString('utf8').replace(/\r\n/g, '\n'), 'utf8');
}

export function sha256File(file) { return sha256Bytes(normalisedBuffer(file)); }

/** Hash of an ordered set of files: relative path + normalised content, like red-first-prover/gate do. */
export function sha256Files(repo, files, relOf) {
  const h = crypto.createHash('sha256');
  for (const f of files) { h.update(relOf(f)); h.update(normalisedBuffer(f)); }
  return h.digest('hex');
}
