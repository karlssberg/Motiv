import {
  HighlightStyle,
  LanguageSupport,
  StreamLanguage,
  syntaxHighlighting,
} from '@codemirror/language';
import type { StreamParser, StringStream } from '@codemirror/language';
import { tags } from '@lezer/highlight';
import {
  DSL_KEYWORDS, DSL_QUANTIFIERS, DSL_TYPES,
  PARAM_REST_CHARS, WORD_REST_CHARS, WORD_START_CHARS,
} from '@motiv-rules/core';

/** Quantifiers are highlighted exactly as keywords, so both share one lookup. */
const KEYWORD_LIKE: ReadonlySet<string> = new Set([...DSL_KEYWORDS, ...DSL_QUANTIFIERS]);
const TYPES: ReadonlySet<string> = new Set(DSL_TYPES);

/**
 * Word shapes, built from the core lexer's exported character classes rather than hand-copied —
 * a hand-copy is exactly how this stream parser drifted out of sync with `tokenize` before: dots
 * were admitted to spec words in `@motiv-rules/core`'s lexer, and this file's own copies of
 * `WORD_START`/`WORD_REST` silently kept the old, non-dotted shape.
 */
const WORD_START = new RegExp(`[${WORD_START_CHARS}]`);
const WORD_REST = new RegExp(`[${WORD_REST_CHARS}]`);
/** A parameter reference's continuation — narrower than {@link WORD_REST}, same reasoning as the
 * core lexer's `PARAM_REST`: parameters aren't namespaced, so a dot after one is not part of it. */
const PARAM_REST = new RegExp(`[${PARAM_REST_CHARS}]`);
const DIGIT = /[0-9]/;

/** Consumes the rest of a delimited literal; an unterminated one simply runs to end-of-line. */
function skipDelimited(stream: StringStream, delimiter: string): void {
  while (!stream.eol()) {
    if (stream.next() === delimiter) return;
  }
}

/** The highlight tag for a completed word token. */
function wordTag(word: string): string {
  if (KEYWORD_LIKE.has(word)) return 'keyword';
  if (TYPES.has(word)) return 'typeName';
  return 'variableName';
}

/** Parser state: whether we're inside a backtick-delimited leaf, its lambda parameters, and
 * whether the previous token was a `.` (distinguishes a field from a root name). */
export interface MotivState { inLeaf: boolean; vars: Set<string>; prevDot: boolean }

/** Collection methods recognised as such only directly after a dot, e.g. `orders.where(`. */
const LEAF_METHODS = new Set(['where', 'any', 'all', 'count', 'sum', 'min', 'max', 'equalsIgnoreCase']);
const LEAF_ATOMS = new Set(['null', 'true', 'false']);

/** Colours one token of a leaf. `prevDot` tells a field from a root name; `vars` remembers lambda parameters. */
function leafToken(stream: StringStream, state: MotivState): string | null {
  if (stream.eatSpace()) return null;
  if (stream.match(/^(=>|==|!=|<=|>=|&&|\|\|)/)) { state.prevDot = false; return 'operator'; }
  const char = stream.next();
  if (!char) return null;
  if ('<>!+-*/'.includes(char)) { state.prevDot = false; return 'operator'; }
  if (char === '.') { state.prevDot = true; return 'punctuation'; }
  if ('(),'.includes(char)) { state.prevDot = false; return 'bracket'; }
  if (char === '"') { skipDelimited(stream, '"'); state.prevDot = false; return 'string'; }
  if (char === '@') { stream.eatWhile(/\w/); state.prevDot = false; return 'variableName.special'; }
  if (DIGIT.test(char)) { stream.eatWhile(/[0-9.]/); state.prevDot = false; return 'number'; }
  if (/[A-Za-z_]/.test(char)) {
    stream.eatWhile(/\w/);
    const word = stream.current();
    const afterDot = state.prevDot;
    state.prevDot = false;
    if (LEAF_ATOMS.has(word)) return 'atom';
    if (stream.match(/^\s*=>/, false)) { state.vars.add(word); return 'variableName.local'; }
    if (afterDot && LEAF_METHODS.has(word) && stream.match(/^\s*\(/, false)) return 'variableName.function';
    if (!afterDot && state.vars.has(word)) return 'variableName.local';
    return 'propertyName';
  }
  return 'invalid';
}

/**
 * A CodeMirror stream parser mirroring the core lexer's classification, with a nested mode for
 * the expression-leaf language found inside backticks. Returns `@lezer/highlight` tag names;
 * unrecognised characters are tagged `invalid`.
 */
export const motivStreamParser: StreamParser<MotivState> = {
  name: 'motiv',

  startState: () => ({ inLeaf: false, vars: new Set(), prevDot: false }),
  copyState: (s) => ({ inLeaf: s.inLeaf, vars: new Set(s.vars), prevDot: s.prevDot }),

  token(stream, state) {
    if (state.inLeaf) {
      if (stream.peek() === '`') { stream.next(); state.inLeaf = false; state.vars.clear(); return 'string.special'; }
      return leafToken(stream, state);
    }

    if (stream.eatSpace()) return null;

    if (stream.match('&&') || stream.match('||')) return 'operator';

    const char = stream.next();
    if (!char) return null;

    if ('&|^!'.includes(char)) return 'operator';
    if ('(){}'.includes(char)) return 'bracket';
    if (char === ':' || char === '=' || char === ',') return 'punctuation';
    if (char === '"') { skipDelimited(stream, '"'); return 'string'; }
    if (char === '`') { state.inLeaf = true; state.prevDot = false; return 'string.special'; }
    if (char === '@') { stream.eatWhile(PARAM_REST); return 'variableName.special'; }

    // A `-` starts a number only when a digit follows; elsewhere it is part of a spec word
    // (consumed whole below) or an unrecognised character.
    if (DIGIT.test(char) || (char === '-' && DIGIT.test(stream.peek() ?? ''))) {
      stream.eatWhile(DIGIT);
      // A `.` continues the number only when a digit follows, so `2.` is a number then an error.
      stream.match(/^\.[0-9]+/);
      // An exponent continues the number only when it is followed by a digit, or by +/- followed
      // by a digit, so `2e` is a number then an identifier, not an incomplete exponent.
      stream.match(/^[eE][+-]?[0-9]+/);
      return 'number';
    }

    if (WORD_START.test(char)) {
      stream.eatWhile(WORD_REST);
      return wordTag(stream.current());
    }

    return 'invalid';
  },
};

/** Maps Motiv token tags onto the `--dsl-*` colour custom properties. */
export const motivHighlightStyle = HighlightStyle.define([
  { tag: tags.variableName, color: 'var(--dsl-spec)' },
  { tag: tags.special(tags.variableName), color: 'var(--dsl-param)' },
  { tag: tags.propertyName, color: 'var(--dsl-type)' },
  { tag: tags.function(tags.variableName), color: 'var(--dsl-keyword)' },
  { tag: tags.local(tags.variableName), color: 'var(--dsl-param)', fontStyle: 'italic' },
  { tag: tags.atom, color: 'var(--dsl-keyword)' },
  { tag: tags.keyword, color: 'var(--dsl-keyword)' },
  { tag: tags.typeName, color: 'var(--dsl-type)' },
  { tag: tags.operator, color: 'var(--dsl-operator)' },
  { tag: tags.bracket, color: 'var(--dsl-bracket)' },
  { tag: tags.punctuation, color: 'var(--dsl-punctuation)' },
  { tag: tags.string, color: 'var(--dsl-string)' },
  { tag: tags.special(tags.string), color: 'var(--dsl-expression)' },
  { tag: tags.number, color: 'var(--dsl-number)' },
  { tag: tags.invalid, color: 'var(--danger)' },
]);

/** The Motiv DSL language, with its highlighting attached. */
export function motiv(): LanguageSupport {
  return new LanguageSupport(StreamLanguage.define(motivStreamParser), [
    syntaxHighlighting(motivHighlightStyle),
  ]);
}
