// @vitest-environment node
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { sourceFiles, SRC_ROOT } from './sources.js';

const ADOPTION_DOC = fileURLToPath(new URL('../../../../docs/adoption/index.md', import.meta.url));
const REACT_SRC = fileURLToPath(new URL('../../../packages/rules-react/src/', import.meta.url));

/**
 * The three parts each adapter is priced by, in the order the tables list them: the document
 * bindings, the workflow entry point you take only if you want the session logic, and the one
 * component. The same split for both, so the two columns compare like with like.
 *
 * The first arm is deliberately the catch-all: every source file lands in exactly one row, so a
 * file added anywhere moves a published number and the gate below fires. A part-of-the-tree
 * predicate that could miss a file would let a new one be priced at nothing.
 */
const PARTS = [
  { part: 'bindings', holds: (file: string) => !file.includes('workflow') && !file.startsWith('JustificationTree.') },
  { part: 'workflow', holds: (file: string) => file.includes('workflow') },
  { part: 'component', holds: (file: string) => file.startsWith('JustificationTree.') },
] as const;

/** Lines of code: neither blank nor comment. Block comments are tracked, not guessed at. */
function codeLines(source: string): number {
  let inBlock = false;
  let count = 0;
  for (const raw of source.split('\n')) {
    const line = raw.trim();
    if (!line) continue;
    if (inBlock) {
      if (line.includes('*/')) inBlock = false;
      continue;
    }
    if (line.startsWith('/*')) {
      if (!line.includes('*/')) inBlock = true;
      continue;
    }
    if (line.startsWith('//')) continue;
    count++;
  }
  return count;
}

/** Every number the table for `adapter` publishes, row by row, `lines` then `code lines`. */
function measure(root: string): number[] {
  const files = sourceFiles(root).map((path) => {
    const source = readFileSync(path, 'utf8');
    return { name: path.slice(root.length), total: source.split('\n').length - 1, code: codeLines(source) };
  });
  return PARTS.flatMap(({ holds }) => {
    const part = files.filter((file) => holds(file.name));
    return [
      part.reduce((sum, file) => sum + file.total, 0),
      part.reduce((sum, file) => sum + file.code, 0),
    ];
  });
}

/**
 * The published numbers, read out of the table `docs/adoption/index.md` marks for the purpose. The
 * markers exist so that which table is being read is a decision recorded in the document, rather
 * than a guess made here about which one came first.
 */
function published(marker: string): number[] {
  const doc = readFileSync(ADOPTION_DOC, 'utf8');
  const marked = doc.split(`<!-- ${marker} -->`)[1];
  if (marked === undefined) throw new Error(`docs/adoption/index.md has no <!-- ${marker} --> table.`);
  const table = marked.slice(0, marked.indexOf('\n\n'));
  // Lookahead on the closing pipe: two numeric cells are adjacent, and consuming the delimiter
  // between them would read only the first of every pair.
  return [...table.matchAll(/\|\s*(\d+)\s*(?=\|)/g)].map((match) => Number(match[1]));
}

/**
 * A number in a document that nothing recomputes is a claim with a shelf life, and this one has a
 * buyer's decision resting on it — *what does it cost us to use this from Vue?* So it is checked
 * against the thing it describes, the way the conformance report is checked against its record
 * rather than maintained beside it.
 *
 * Both tables are gated, not just the new one. The page's argument is a *comparison*, and half a
 * comparison held to the source is the same defect as none of it: the React figures are what the
 * Vue figures mean anything against.
 *
 * When this fails, an adapter changed and the page did not. Edit the page.
 */
describe('the prices the tier table publishes', () => {
  it.each([
    ['the React adapter', 'react-adapter-price', REACT_SRC],
    ['the Vue adapter', 'vue-adapter-price', SRC_ROOT],
  ])('are what %s actually costs', (_name, marker, root) => {
    expect(published(marker)).toEqual(measure(root));
  });
});
