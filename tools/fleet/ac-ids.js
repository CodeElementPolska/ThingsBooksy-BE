// Shared helper — the story's expected acceptance-criterion ids.
// Source of truth: the `acceptance_criteria[].id` entries in the front matter of runs/<story>/story.md.
// A whole-file regex is NOT enough: story.md and spec.md also mention ids that were REMOVED during
// discovery ("removed AC-1 and AC-2", DEC-3 on 015), which showed up as "uncovered AC" in ac-matrix.
// Fallbacks (no story.md front matter): `**AC-n**` scenario headings in specs/<story>/spec.md, then
// any `AC-n` token in the first file that exists (legacy behaviour).
import fs from 'node:fs';
import path from 'node:path';

const uniq = a => [...new Set(a)];
const read = f => fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n');

export function readAcIds(repo, story, storyFile) {
  const storyMd = storyFile || path.join(repo, 'runs', story, 'story.md');
  const specMd = path.join(repo, 'specs', story, 'spec.md');

  if (fs.existsSync(storyMd)) {
    const fm = read(storyMd).match(/^---\n([\s\S]*?)\n---/);
    if (fm) {
      const ids = uniq([...fm[1].matchAll(/^\s*-\s+id:\s*"?(AC-\d+)"?\s*$/gm)].map(m => m[1]));
      if (ids.length) return ids;
    }
  }
  if (fs.existsSync(specMd)) {
    const ids = uniq([...read(specMd).matchAll(/\*\*(AC-\d+)\*\*/g)].map(m => m[1]));
    if (ids.length) return ids;
  }
  const src = [storyMd, specMd].find(f => fs.existsSync(f));
  return src ? uniq([...read(src).matchAll(/\bAC-\d+\b/g)].map(m => m[0])) : [];
}
