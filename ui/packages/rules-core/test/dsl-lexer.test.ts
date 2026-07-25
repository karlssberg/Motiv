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
});
