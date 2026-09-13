import { describe, it, expect } from 'vitest';
import { EditorState } from '@codemirror/state';
import { localMarks } from '../../src/dsl/localMarks.js';

/** The `[from, to)` ranges the field currently marks, in document order. */
function markedRanges(text: string): Array<[number, number]> {
  const state = EditorState.create({ doc: text, extensions: [localMarks] });
  const ranges: Array<[number, number]> = [];
  state.field(localMarks).between(0, text.length, (from, to) => {
    ranges.push([from, to]);
  });
  return ranges;
}

describe('localMarks', () => {
  it('marks a reference to a declared local, not its declaration and not an ordinary spec word', () => {
    const text = 'let a = x\n\na & b';
    // "let a = x\n\na & b"
    //  0123456789 0123456
    //             1111111
    const secondA = text.lastIndexOf('a');

    expect(markedRanges(text)).toEqual([[secondA, secondA + 1]]);
  });

  it('marks every reference, not only the first', () => {
    const text = 'let a = x\n\na & a';
    const ranges = markedRanges(text);
    expect(ranges).toHaveLength(2);
    expect(ranges.map(([from, to]) => text.slice(from, to))).toEqual(['a', 'a']);
  });

  it('marks nothing when the document declares no locals', () => {
    expect(markedRanges('is-a & is-b')).toEqual([]);
  });

  it('recomputes when the document changes', () => {
    const state = EditorState.create({ doc: 'is-a', extensions: [localMarks] });
    const next = state.update({ changes: { from: 0, to: state.doc.length, insert: 'let a = x\n\na' } }).state;
    const ranges: Array<[number, number]> = [];
    next.field(localMarks).between(0, next.doc.length, (from, to) => { ranges.push([from, to]); });
    expect(ranges).toEqual([[11, 12]]);
  });
});
