import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';

describe('parse — leaves and grouping', () => {
  it('parses a bare spec into a spec node at the root path', () => {
    const result = parse('is-active');
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual({ rule: { spec: 'is-active' } });
  });

  it('records a span for the root node covering the spec token', () => {
    expect(parse('is-active').spans).toEqual([{ path: '$.rule', from: 0, to: 9 }]);
  });

  it('parses a backtick expression into an expression node, stripping the backticks', () => {
    expect(parse('`n > 0`').document).toEqual({ rule: { expression: 'n > 0' } });
  });

  it('parses negation into a not node', () => {
    expect(parse('!is-flagged').document).toEqual({ rule: { not: { spec: 'is-flagged' } } });
  });

  it('parses double negation', () => {
    expect(parse('!!is-flagged').document).toEqual({
      rule: { not: { not: { spec: 'is-flagged' } } },
    });
  });

  it('spans a not node and its child at distinct paths', () => {
    expect(parse('!is-flagged').spans).toEqual([
      { path: '$.rule', from: 0, to: 11 },
      { path: '$.rule.not', from: 1, to: 11 },
    ]);
  });

  it('parses a parenthesised expression as the inner node, without a wrapper', () => {
    expect(parse('(is-active)').document).toEqual({ rule: { spec: 'is-active' } });
  });

  it('attaches a name from a trailing as-clause', () => {
    expect(parse('is-active as "activity"').document).toEqual({
      rule: { spec: 'is-active', name: 'activity' },
    });
  });

  it('binds as to the group when applied to a parenthesised expression', () => {
    expect(parse('(is-active) as "activity"').document).toEqual({
      rule: { spec: 'is-active', name: 'activity' },
    });
  });
});

describe('parse — binary operators', () => {
  it('maps each operator to its node kind', () => {
    expect(parse('a & b').document).toEqual({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    expect(parse('a | b').document).toEqual({ rule: { or: [{ spec: 'a' }, { spec: 'b' }] } });
    expect(parse('a ^ b').document).toEqual({ rule: { xor: [{ spec: 'a' }, { spec: 'b' }] } });
    expect(parse('a && b').document).toEqual({ rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }] } });
    expect(parse('a || b').document).toEqual({ rule: { orElse: [{ spec: 'a' }, { spec: 'b' }] } });
  });

  it('flattens a run of the same operator into one n-ary node', () => {
    expect(parse('a && b && c').document).toEqual({
      rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] },
    });
  });

  it('binds & tighter than &&', () => {
    expect(parse('a & b && c').document).toEqual({
      rule: { andAlso: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] },
    });
  });

  it('binds | tighter than || and &&', () => {
    expect(parse('a | b && c').document).toEqual({
      rule: { andAlso: [{ or: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] },
    });
  });

  it('binds ^ tighter than | and looser than &', () => {
    expect(parse('a & b ^ c | d').document).toEqual({
      rule: { or: [{ xor: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }, { spec: 'd' }] },
    });
  });

  it('binds ! tighter than any binary operator', () => {
    expect(parse('!a && b').document).toEqual({
      rule: { andAlso: [{ not: { spec: 'a' } }, { spec: 'b' }] },
    });
  });

  it('lets parentheses override precedence', () => {
    expect(parse('a && (b | c)').document).toEqual({
      rule: { andAlso: [{ spec: 'a' }, { or: [{ spec: 'b' }, { spec: 'c' }] }] },
    });
  });

  it('names a compound node when the group carries the as-clause', () => {
    expect(parse('(a && b) as "pair"').document).toEqual({
      rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' },
    });
  });

  it('paths operands by operator and index', () => {
    const spans = parse('a && b').spans;
    expect(spans.map((s) => s.path)).toEqual([
      '$.rule', '$.rule.andAlso[0]', '$.rule.andAlso[1]',
    ]);
  });
});
