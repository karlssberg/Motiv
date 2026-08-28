import { describe, it, expect, vi } from 'vitest';
import { EditorState } from '@codemirror/state';
import { StringStream, ensureSyntaxTree } from '@codemirror/language';
import { motiv, motivStreamParser } from '../../src/dsl/motivLanguage.js';

/** Drives the stream parser over one line, returning each token's text and highlight tag. */
function classify(line: string): Array<{ text: string; tag: string }> {
  const stream = new StringStream(line, 4, 4, 0);
  const state = motivStreamParser.startState?.(4);
  const tokens: Array<{ text: string; tag: string }> = [];

  while (!stream.eol()) {
    const tag = motivStreamParser.token(stream, state);
    if (stream.pos === stream.start) throw new Error(`token() did not advance at ${stream.pos}`);
    if (tag !== null) tokens.push({ text: line.slice(stream.start, stream.pos), tag });
    stream.start = stream.pos;
  }

  return tokens;
}

/** The tag the parser assigns to the sole token of `line`. */
function tagOf(line: string): string {
  const tokens = classify(line);
  expect(tokens).toHaveLength(1);
  return tokens[0]!.tag;
}

describe('motivStreamParser', () => {
  it('tags spec names as variable names', () => {
    expect(tagOf('is-active')).toBe('variableName');
    expect(tagOf('orders')).toBe('variableName');
  });

  it('tags two-character operators', () => {
    expect(classify('a && b')).toEqual([
      { text: 'a', tag: 'variableName' },
      { text: '&&', tag: 'operator' },
      { text: 'b', tag: 'variableName' },
    ]);
    expect(tagOf('||')).toBe('operator');
  });

  it('tags one-character operators', () => {
    for (const operator of ['&', '|', '^', '!']) {
      expect(tagOf(operator)).toBe('operator');
    }
  });

  it('tags keywords', () => {
    for (const keyword of ['param', 'in', 'as']) {
      expect(tagOf(keyword)).toBe('keyword');
    }
  });

  it('tags quantifiers as keywords', () => {
    for (const quantifier of ['all', 'any', 'exactly', 'atLeast', 'atMost']) {
      expect(tagOf(quantifier)).toBe('keyword');
    }
  });

  it('tags type names', () => {
    for (const type of ['integer', 'number', 'string', 'boolean']) {
      expect(tagOf(type)).toBe('typeName');
    }
  });

  it('tags string literals', () => {
    expect(tagOf('"quota"')).toBe('string');
  });

  it('tags an unterminated string up to the end of the line', () => {
    expect(classify('"quota')).toEqual([{ text: '"quota', tag: 'string' }]);
  });

  it('tags backtick expressions as special strings', () => {
    expect(tagOf('`n > 0`')).toBe('string.special');
  });

  it('tags parameter references as special variable names', () => {
    expect(tagOf('@minOrders')).toBe('variableName.special');
  });

  it('tags numbers, including negative decimals', () => {
    expect(tagOf('3')).toBe('number');
    expect(classify('= -1.5')).toEqual([
      { text: '=', tag: 'punctuation' },
      { text: '-1.5', tag: 'number' },
    ]);
  });

  it('tags a trailing dot as invalid rather than part of the number', () => {
    expect(classify('2.')).toEqual([
      { text: '2', tag: 'number' },
      { text: '.', tag: 'invalid' },
    ]);
  });

  it('tags brackets', () => {
    for (const bracket of ['(', ')', '{', '}']) {
      expect(tagOf(bracket)).toBe('bracket');
    }
  });

  it('tags colons and equals as punctuation', () => {
    expect(tagOf(':')).toBe('punctuation');
    expect(tagOf('=')).toBe('punctuation');
  });

  it('tags a comma as punctuation, like the other separators', () => {
    expect(tagOf(',')).toBe('punctuation');
    expect(tagOf(':')).toBe('punctuation');
    expect(tagOf('=')).toBe('punctuation');
  });

  it('tags numbers with exponents as a single number token', () => {
    expect(tagOf('1e21')).toBe('number');
    expect(tagOf('1e+21')).toBe('number');
    expect(tagOf('1.5e-3')).toBe('number');
  });

  it('does not consume a trailing e that cannot complete an exponent', () => {
    expect(classify('2e')).toEqual([
      { text: '2', tag: 'number' },
      { text: 'e', tag: 'variableName' },
    ]);
  });

  it('tags an unrecognised character as invalid', () => {
    expect(tagOf('#')).toBe('invalid');
  });

  it('tags a dotted spec name as one token with no invalid tagging', () => {
    expect(classify('customer.is-active')).toEqual([
      { text: 'customer.is-active', tag: 'variableName' },
    ]);
  });

  it('does not let a parameter reference admit a dot', () => {
    expect(classify('@minOrders.foo')).toEqual([
      { text: '@minOrders', tag: 'variableName.special' },
      { text: '.', tag: 'invalid' },
      { text: 'foo', tag: 'variableName' },
    ]);
  });

  it('tokenises a whole parameter declaration', () => {
    expect(classify('param minOrders: integer = 3')).toEqual([
      { text: 'param', tag: 'keyword' },
      { text: 'minOrders', tag: 'variableName' },
      { text: ':', tag: 'punctuation' },
      { text: 'integer', tag: 'typeName' },
      { text: '=', tag: 'punctuation' },
      { text: '3', tag: 'number' },
    ]);
  });

  it('tokenises a quantifier body', () => {
    expect(classify('atLeast(@min) in orders { is-large as "big" }')).toEqual([
      { text: 'atLeast', tag: 'keyword' },
      { text: '(', tag: 'bracket' },
      { text: '@min', tag: 'variableName.special' },
      { text: ')', tag: 'bracket' },
      { text: 'in', tag: 'keyword' },
      { text: 'orders', tag: 'variableName' },
      { text: '{', tag: 'bracket' },
      { text: 'is-large', tag: 'variableName' },
      { text: 'as', tag: 'keyword' },
      { text: '"big"', tag: 'string' },
      { text: '}', tag: 'bracket' },
    ]);
  });
});

describe('motiv', () => {
  it('parses a document without emitting unknown-tag warnings', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const doc = 'param n: integer = -1.5\n@n && `x` || "s" ^ !(all in orders { is-a as "b" }) #';
    const state = EditorState.create({ doc, extensions: [motiv()] });

    ensureSyntaxTree(state, doc.length, 5000);

    expect(warn).not.toHaveBeenCalled();
    warn.mockRestore();
  });
});
