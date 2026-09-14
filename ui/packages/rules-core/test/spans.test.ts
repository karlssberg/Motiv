import { describe, it, expect } from 'vitest';
import { rangeOfPath } from '../src/dsl/spans.js';
import { parse } from '../src/dsl/parser.js';
import { printInline } from '../src/dsl/printer.js';

describe('rangeOfPath', () => {
  const spansOf = (text: string) => parse(text).spans;

  it('finds the range recorded for an exact path', () => {
    const text = 'a & b';
    const range = rangeOfPath('$.rule.and[1]', spansOf(text), text.length);
    expect(text.slice(range.from, range.to)).toBe('b');
  });

  it('includes the parentheses of a grouped subtree', () => {
    const text = 'a & (b | c)';
    const range = rangeOfPath('$.rule.and[1]', spansOf(text), text.length);
    expect(text.slice(range.from, range.to)).toBe('(b | c)');
  });

  it('falls back to the nearest ancestor for a sub-field path', () => {
    const text = 'a & b';
    const exact = rangeOfPath('$.rule.and[1]', spansOf(text), text.length);
    expect(rangeOfPath('$.rule.and[1].whenTrue', spansOf(text), text.length)).toEqual(exact);
  });

  it('falls back to the whole document for an unknown path', () => {
    const text = 'a & b';
    expect(rangeOfPath('$.rule.or[9]', spansOf(text), text.length)).toEqual({ from: 0, to: text.length });
  });

  it('anchors a definition sub-field path on the `let` declaration name', () => {
    const text = 'let a = x\n\na';
    const range = rangeOfPath('$.definitions.a.whenTrue', spansOf(text), text.length);
    expect(range).toEqual({ from: 4, to: 5 });
    expect(text.slice(range.from, range.to)).toBe('a');
  });

  it('anchors the definition path itself on the declaration, not the whole document', () => {
    const text = 'let a = x\n\na';
    expect(rangeOfPath('$.definitions.a', spansOf(text), text.length)).toEqual({ from: 4, to: 5 });
  });

  it('keeps the definition body addressable separately from the declaration', () => {
    const text = 'let a = x\n\na';
    const range = rangeOfPath('$.definitions.a.rule', spansOf(text), text.length);
    expect(text.slice(range.from, range.to)).toBe('x');
  });

  it('round-trips a printed node so a builder document can be addressed by path', () => {
    const rule = { and: [{ spec: 'a' }, { or: [{ spec: 'b' }, { spec: 'c' }] }] };
    const text = printInline(rule);
    const range = rangeOfPath('$.rule.and[1]', spansOf(text), text.length);
    expect(text.slice(range.from, range.to)).toBe('(b | c)');
  });
});
