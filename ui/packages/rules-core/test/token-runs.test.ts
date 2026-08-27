import { describe, it, expect } from 'vitest';
import { tokenSpans } from '../src/dsl/tokenRuns.js';

describe('tokenSpans', () => {
  it('splits DSL text into token runs with the whitespace gaps re-inserted verbatim', () => {
    const spans = tokenSpans('a  && b');
    expect(spans.map((span) => span.value).join('')).toBe('a  && b');
    expect(spans.map((span) => span.kind)).toEqual(['spec', 'gap', 'operator', 'gap', 'spec']);
  });

  it('keeps leading and trailing whitespace as gaps', () => {
    const spans = tokenSpans('  a ');
    expect(spans.map((span) => span.value).join('')).toBe('  a ');
    expect(spans[0]!.kind).toBe('gap');
    expect(spans[spans.length - 1]!.kind).toBe('gap');
  });

  it('gives every run a key unique within the text', () => {
    const spans = tokenSpans('a && a && a');
    const keys = spans.map((span) => span.key);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it('returns nothing for empty text', () => {
    expect(tokenSpans('')).toEqual([]);
  });
});
