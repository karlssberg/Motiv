import { describe, it, expect } from 'vitest';
import { tokenize } from '../src/dsl/lexer.js';

describe('tokenize', () => {
  it('lexes a spec name as a single token with exact offsets', () => {
    expect(tokenize('is-active')).toEqual([
      { kind: 'spec', value: 'is-active', from: 0, to: 9 },
    ]);
  });

  it('skips whitespace but keeps offsets absolute', () => {
    expect(tokenize('  is-active')).toEqual([
      { kind: 'spec', value: 'is-active', from: 2, to: 11 },
    ]);
  });

  it('lexes two-character operators before one-character ones', () => {
    expect(tokenize('a && b || c').map((t) => [t.kind, t.value])).toEqual([
      ['spec', 'a'], ['operator', '&&'], ['spec', 'b'], ['operator', '||'], ['spec', 'c'],
    ]);
  });

  it('lexes single-character logical operators', () => {
    expect(tokenize('a & b | c ^ !d').map((t) => t.value)).toEqual([
      'a', '&', 'b', '|', 'c', '^', '!', 'd',
    ]);
  });

  it('classifies keywords, types and quantifiers', () => {
    expect(tokenize('param x: integer atLeast in as').map((t) => t.kind)).toEqual([
      'keyword', 'spec', 'colon', 'type', 'quantifier', 'keyword', 'keyword',
    ]);
  });

  it('lexes a quoted string including its quotes', () => {
    expect(tokenize('as "quota"')[1]).toEqual({
      kind: 'string', value: '"quota"', from: 3, to: 10,
    });
  });

  it('lexes a backtick expression', () => {
    expect(tokenize('`n > 0`')).toEqual([
      { kind: 'expression', value: '`n > 0`', from: 0, to: 7 },
    ]);
  });

  it('lexes a param reference and a number', () => {
    expect(tokenize('@minOrders 3').map((t) => [t.kind, t.value])).toEqual([
      ['paramRef', '@minOrders'], ['number', '3'],
    ]);
  });

  it('lexes braces, parens, colon and equals', () => {
    expect(tokenize('(){}:=').map((t) => t.kind)).toEqual([
      'paren', 'paren', 'brace', 'brace', 'colon', 'equals',
    ]);
  });

  it('emits an error token for an unrecognised character', () => {
    expect(tokenize('#')).toEqual([{ kind: 'error', value: '#', from: 0, to: 1 }]);
  });

  it('lexes an unterminated string up to end of input', () => {
    expect(tokenize('"oops')).toEqual([
      { kind: 'string', value: '"oops', from: 0, to: 5 },
    ]);
  });
  it('lexes a negative number as one token', () => {
    expect(tokenize('-1')).toEqual([{ kind: 'number', value: '-1', from: 0, to: 2 }]);
  });

  it('keeps hyphens inside spec words out of numbers', () => {
    expect(tokenize('is-active a-1').map((t) => [t.kind, t.value])).toEqual([
      ['spec', 'is-active'], ['spec', 'a-1'],
    ]);
  });

  it('emits an error token for a hyphen not followed by a digit', () => {
    expect(tokenize('- 1').map((t) => [t.kind, t.value])).toEqual([
      ['error', '-'], ['number', '1'],
    ]);
  });

  it('lexes a decimal number as one token', () => {
    expect(tokenize('2.5')).toEqual([{ kind: 'number', value: '2.5', from: 0, to: 3 }]);
  });

  it('lexes a negative decimal number as one token', () => {
    expect(tokenize('-1.5')).toEqual([{ kind: 'number', value: '-1.5', from: 0, to: 4 }]);
  });

  it('stops a number at a decimal point with no digit after it', () => {
    expect(tokenize('2.').map((t) => [t.kind, t.value])).toEqual([
      ['number', '2'], ['error', '.'],
    ]);
  });

  it('takes only the first decimal point into a number', () => {
    expect(tokenize('1.2.3').map((t) => [t.kind, t.value])).toEqual([
      ['number', '1.2'], ['error', '.'], ['number', '3'],
    ]);
  });

  it('lexes a dotted spec name as one token', () => {
    const tokens = tokenize('customer.eligibility.is-active');

    expect(tokens).toHaveLength(1);
    expect(tokens[0]!.kind).toBe('spec');
    expect(tokens[0]!.value).toBe('customer.eligibility.is-active');
  });

  it('lexes dotted names either side of an operator', () => {
    const tokens = tokenize('customer.is-active & customer.is-adult');

    expect(tokens.map((token) => token.value)).toEqual([
      'customer.is-active',
      '&',
      'customer.is-adult',
    ]);
  });

  it('still lexes a decimal number rather than a dotted word', () => {
    const tokens = tokenize('2.5');

    expect(tokens).toHaveLength(1);
    expect(tokens[0]!.kind).toBe('number');
    expect(tokens[0]!.value).toBe('2.5');
  });

  it('does not let a dotted word swallow a following number', () => {
    const tokens = tokenize('customer.is-active 2');

    expect(tokens.map((token) => [token.kind, token.value])).toEqual([
      ['spec', 'customer.is-active'],
      ['number', '2'],
    ]);
  });

  it('keeps a quantifier a quantifier and a dotted word a spec', () => {
    const tokens = tokenize('all all.things');

    expect(tokens.map((token) => token.kind)).toEqual(['quantifier', 'spec']);
  });

  it('does not let a parameter reference admit a dot', () => {
    // Params aren't namespaced, so a dot after one is a syntax error, not a continuation —
    // admitting it here would be an accidental widening riding along on the shared word
    // character class rather than a deliberate grammar decision.
    const tokens = tokenize('@minOrders.foo');

    expect(tokens.map((token) => [token.kind, token.value])).toEqual([
      ['paramRef', '@minOrders'],
      ['error', '.'],
      ['spec', 'foo'],
    ]);
  });
});
