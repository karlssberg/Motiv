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

/**
 * A stateless CodeMirror stream parser mirroring the core lexer's classification.
 * Returns `@lezer/highlight` tag names; unrecognised characters are tagged `invalid`.
 */
export const motivStreamParser: StreamParser<unknown> = {
  name: 'motiv',

  token(stream) {
    if (stream.eatSpace()) return null;

    if (stream.match('&&') || stream.match('||')) return 'operator';

    const char = stream.next();
    if (!char) return null;

    if ('&|^!'.includes(char)) return 'operator';
    if ('(){}'.includes(char)) return 'bracket';
    if (char === ':' || char === '=' || char === ',') return 'punctuation';
    if (char === '"') { skipDelimited(stream, '"'); return 'string'; }
    if (char === '`') { skipDelimited(stream, '`'); return 'string.special'; }
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
