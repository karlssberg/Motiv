import type { Token, TokenKind } from './types.js';

const KEYWORDS = new Set(['param', 'in', 'as']);
const TYPES = new Set(['integer', 'number', 'string', 'boolean']);
const QUANTIFIERS = new Set(['all', 'any', 'exactly', 'atLeast', 'atMost']);

/**
 * Character classes (as `[...]`-ready fragments, no brackets) for a DSL word. Exported so that
 * anything outside this module needing to recognise or complete a word — the demo's CodeMirror
 * stream parser and its completion source, currently — composes its own regex from these instead
 * of hand-copying the character classes. That copying is exactly how they drifted out of sync
 * with this lexer when dots were admitted below: both `WORD_START`/`WORD_REST` and their demo
 * duplicates needed the same edit, and only one side got it.
 */
export const WORD_START_CHARS = 'A-Za-z_';
/**
 * Words are spec-shaped: a letter followed by letters, digits, hyphens or underscores — plus dots,
 * which namespace a spec name (`customer.eligibility.is-active`). A dot cannot be stolen from a
 * numeric literal, because numbers are lexed before words.
 */
export const WORD_REST_CHARS = 'A-Za-z0-9_.-';
/**
 * A parameter reference's continuation, deliberately narrower than {@link WORD_REST_CHARS}:
 * parameters aren't namespaced, so `@minOrders.foo` is a paramRef followed by a syntax error, not
 * one dotted reference. Giving it this separate class (rather than reusing `WORD_REST_CHARS`, as
 * this lexer once did) keeps that a decision instead of an accidental widening that rides along
 * whenever the spec-word class changes.
 */
export const PARAM_REST_CHARS = 'A-Za-z0-9_-';

const WORD_START = new RegExp(`[${WORD_START_CHARS}]`);
const WORD_REST = new RegExp(`[${WORD_REST_CHARS}]`);
const PARAM_REST = new RegExp(`[${PARAM_REST_CHARS}]`);
const DIGIT = /[0-9]/;

function wordKind(word: string): TokenKind {
  if (KEYWORDS.has(word)) return 'keyword';
  if (TYPES.has(word)) return 'type';
  if (QUANTIFIERS.has(word)) return 'quantifier';
  return 'spec';
}

/** Reads a delimited run starting at `from`, returning the index just past the closing delimiter. */
function readDelimited(text: string, from: number, delimiter: string): number {
  let i = from + 1;
  while (i < text.length && text[i] !== delimiter) i++;
  return i < text.length ? i + 1 : text.length;
}

/** Reads a run of digits starting at `from`, returning the index just past the last digit. */
function readDigits(text: string, from: number): number {
  let i = from;
  while (i < text.length && DIGIT.test(text[i]!)) i++;
  return i;
}

/**
 * Lexes DSL text into tokens carrying absolute source offsets. Never throws: an
 * unrecognised character becomes an `error` token so the parser can report it in place.
 */
export function tokenize(text: string): Token[] {
  const tokens: Token[] = [];
  let i = 0;
  const push = (kind: TokenKind, from: number, to: number): void => {
    tokens.push({ kind, value: text.slice(from, to), from, to });
  };

  while (i < text.length) {
    const char = text[i]!;

    if (/\s/.test(char)) { i++; continue; }

    if (char === '&' && text[i + 1] === '&') { push('operator', i, i + 2); i += 2; continue; }
    if (char === '|' && text[i + 1] === '|') { push('operator', i, i + 2); i += 2; continue; }
    if ('&|^!'.includes(char)) { push('operator', i, i + 1); i++; continue; }

    if (char === '(' || char === ')') { push('paren', i, i + 1); i++; continue; }
    if (char === '{' || char === '}') { push('brace', i, i + 1); i++; continue; }
    if (char === ':') { push('colon', i, i + 1); i++; continue; }
    if (char === '=') { push('equals', i, i + 1); i++; continue; }
    if (char === ',') { push('comma', i, i + 1); i++; continue; }

    if (char === '"') { const end = readDelimited(text, i, '"'); push('string', i, end); i = end; continue; }
    if (char === '`') { const end = readDelimited(text, i, '`'); push('expression', i, end); i = end; continue; }

    if (char === '@') {
      let j = i + 1;
      while (j < text.length && PARAM_REST.test(text[j]!)) j++;
      push('paramRef', i, j); i = j; continue;
    }

    // A `-` starts a number only when a digit follows it. Elsewhere `-` is either part of a
    // spec word (`is-active`, consumed whole below) or an unrecognised character.
    const negative = char === '-' && DIGIT.test(text[i + 1] ?? '');
    if (negative || DIGIT.test(char)) {
      let j = readDigits(text, negative ? i + 1 : i);
      // A single `.` continues the number only when a digit follows, so `2.` lexes as `2`
      // then an error character rather than a number the parser cannot re-read.
      if (text[j] === '.' && DIGIT.test(text[j + 1] ?? '')) j = readDigits(text, j + 1);
      // An exponent continues the number only when it is followed by a digit, or by
      // +/- followed by a digit, so `1e` lexes as `1` then `e`, not as an incomplete exponent.
      const exponentChar = text[j];
      if (exponentChar === 'e' || exponentChar === 'E') {
        const nextChar = text[j + 1];
        const hasDigit = DIGIT.test(nextChar ?? '');
        const hasSignAndDigit = (nextChar === '+' || nextChar === '-') && DIGIT.test(text[j + 2] ?? '');
        if (hasDigit || hasSignAndDigit) {
          j = (nextChar === '+' || nextChar === '-') ? j + 2 : j + 1;
          j = readDigits(text, j);
        }
      }
      push('number', i, j); i = j; continue;
    }

    if (WORD_START.test(char)) {
      let j = i;
      while (j < text.length && WORD_REST.test(text[j]!)) j++;
      push(wordKind(text.slice(i, j)), i, j); i = j; continue;
    }

    push('error', i, i + 1); i++;
  }

  return tokens;
}
