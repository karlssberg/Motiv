import {
  HighlightStyle,
  LanguageSupport,
  StreamLanguage,
  syntaxHighlighting,
} from '@codemirror/language';
import type { StreamParser, StringStream } from '@codemirror/language';
import { tags } from '@lezer/highlight';

/** The DSL's reserved words, in the order they are offered for completion. */
export const DSL_KEYWORDS = ['param', 'in', 'as'] as const;
/** The higher-order quantifiers, styled and completed as keywords. */
export const DSL_QUANTIFIERS = ['all', 'any', 'exactly', 'atLeast', 'atMost'] as const;
/** The parameter type names. */
export const DSL_TYPES = ['integer', 'number', 'string', 'boolean'] as const;

const KEYWORDS: ReadonlySet<string> = new Set(DSL_KEYWORDS);
const TYPES: ReadonlySet<string> = new Set(DSL_TYPES);
const QUANTIFIERS: ReadonlySet<string> = new Set(DSL_QUANTIFIERS);

/** Word shapes, mirroring the core lexer: a letter or `_`, then letters, digits, `-` or `_`. */
const WORD_START = /[A-Za-z_]/;
const WORD_REST = /[A-Za-z0-9_-]/;
const DIGIT = /[0-9]/;

/** Consumes the rest of a delimited literal; an unterminated one simply runs to end-of-line. */
function skipDelimited(stream: StringStream, delimiter: string): void {
  while (!stream.eol()) {
    if (stream.next() === delimiter) return;
  }
}

/** The highlight tag for a completed word token. */
function wordTag(word: string): string {
  if (KEYWORDS.has(word) || QUANTIFIERS.has(word)) return 'keyword';
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
    if (char === ':' || char === '=') return 'punctuation';
    if (char === '"') { skipDelimited(stream, '"'); return 'string'; }
    if (char === '`') { skipDelimited(stream, '`'); return 'string.special'; }
    if (char === '@') { stream.eatWhile(WORD_REST); return 'variableName.special'; }

    // A `-` starts a number only when a digit follows; elsewhere it is part of a spec word
    // (consumed whole below) or an unrecognised character.
    if (DIGIT.test(char) || (char === '-' && DIGIT.test(stream.peek() ?? ''))) {
      stream.eatWhile(DIGIT);
      // A `.` continues the number only when a digit follows, so `2.` is a number then an error.
      stream.match(/^\.[0-9]+/);
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
