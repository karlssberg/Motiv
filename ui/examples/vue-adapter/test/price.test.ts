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

/**
 * Lines, as a reader would count them: a trailing newline terminates the last line rather than
 * starting an empty one, and a file that ends without one still has its last line counted.
 *
 * The naive `split('\n').length - 1` agrees with this for every file in the repository today and
 * disagrees by one for any file saved without a final newline — undercounting exactly the file an
 * editor is most likely to produce, and only after it is committed.
 */
export function countLines(source: string): number {
  if (source === '') return 0;
  return source.replace(/\n$/, '').split('\n').length;
}

/** Lines of code: neither blank nor comment. Block comments are tracked, not guessed at. */
export function codeLines(source: string): number {
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

/** Every number the table for an adapter publishes, row by row, `lines` then `code lines`. */
function measure(root: string): number[] {
  const files = sourceFiles(root).map((path) => {
    const source = readFileSync(path, 'utf8');
    return { name: path.slice(root.length), total: countLines(source), code: codeLines(source) };
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
 *
 * Both the marker and the blank line that ends the table are required rather than assumed. A gate
 * that reads a malformed document as a shorter one still passes, on numbers that came from
 * somewhere else — the failure mode this whole file exists to prevent.
 */
export function published(doc: string, marker: string): number[] {
  const marked = doc.split(`<!-- ${marker} -->`)[1];
  if (marked === undefined) throw new Error(`docs/adoption/index.md has no <!-- ${marker} --> table.`);
  const end = marked.indexOf('\n\n');
  if (end === -1) {
    throw new Error(`The <!-- ${marker} --> table is not followed by a blank line, so its end cannot be found.`);
  }
  // Lookahead on the closing pipe: two numeric cells are adjacent, and consuming the delimiter
  // between them would read only the first of every pair.
  return [...marked.slice(0, end).matchAll(/\|\s*(\d+)\s*(?=\|)/g)].map((match) => Number(match[1]));
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
  const doc = readFileSync(ADOPTION_DOC, 'utf8');

  it.each([
    ['the React adapter', 'react-adapter-price', REACT_SRC],
    ['the Vue adapter', 'vue-adapter-price', SRC_ROOT],
  ])('are what %s actually costs', (_name, marker, root) => {
    expect(published(doc, marker)).toEqual(measure(root));
  });
});

/** The gate's own reading, which decides what every number above means. */
describe('reading a source file', () => {
  it('counts the last line whether or not the file ends with a newline', () => {
    expect(countLines('a\nb\nc\n')).toBe(3);
    expect(countLines('a\nb\nc')).toBe(3);
    expect(countLines('')).toBe(0);
    expect(countLines('\n')).toBe(1);
  });

  it('counts neither blank lines nor comments as code', () => {
    expect(codeLines('const a = 1;\n\n// note\n/* block\n   more */\nconst b = 2;\n')).toBe(2);
    expect(codeLines('/** one-line doc */\nconst a = 1;\n')).toBe(1);
  });
});

describe('reading the published table', () => {
  const table = [
    '<!-- a-price -->',
    '| Part | Lines | Code lines | What |',
    '|---|---:|---:|---|',
    '| bindings | 12 | 7 | 409 recovery and other prose with digits in it |',
    '',
    'Text after the table.',
  ].join('\n');

  it('reads the numeric cells of the marked table, in order, and nothing else', () => {
    expect(published(table, 'a-price')).toEqual([12, 7]);
  });

  it('refuses a document with no such marker', () => {
    expect(() => published(table, 'b-price')).toThrow(/has no <!-- b-price --> table/);
  });

  it('refuses a table that never ends, rather than reading part of the document', () => {
    const unterminated = '<!-- a-price -->\n| bindings | 12 | 7 |';
    expect(() => published(unterminated, 'a-price')).toThrow(/not followed by a blank line/);
  });
});
