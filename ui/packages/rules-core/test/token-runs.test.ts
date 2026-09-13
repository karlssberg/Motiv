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

  it('marks a declared local as a local run rather than a spec run', () => {
    const spans = tokenSpans('let a = is-active\na');
    const runs = spans.filter((span) => span.kind !== 'gap');
    // 'a' the declaration name, 'is-active' the spec, 'a' the reference.
    expect(runs.map((span) => span.kind)).toEqual([
      'keyword', 'local', 'equals', 'spec', 'local',
    ]);
  });

  it('accepts an explicit locals set instead of scraping the text', () => {
    const spans = tokenSpans('a', new Set(['a']));
    expect(spans.map((span) => span.kind)).toEqual(['local']);
  });

  it('does not mistake a `let` inside a quoted argument string for a declaration of the real spec token', () => {
    const spans = tokenSpans('x && s(label = "let x = 1")');
    const realX = spans.find((span) => span.value === 'x');
    expect(realX!.kind).toBe('spec');
  });

  it('does not mistake a `let` after the rule body for a declaration', () => {
    const spans = tokenSpans('is-active\n\nlet trap = y');
    const trap = spans.find((span) => span.value === 'trap');
    expect(trap!.kind).toBe('spec');
  });
});
