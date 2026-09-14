import { tokenize } from './lexer.js';
import type { Token, TokenKind } from './types.js';

/**
 * The token kinds a word can lex as. The lexer classifies words context-free, so `all` is a
 * `quantifier` and `string` a `type` wherever they appear.
 */
const WORD_KINDS: ReadonlySet<TokenKind> = new Set<TokenKind>(['spec', 'keyword', 'type', 'quantifier']);

/** One `let NAME = …` declaration found by {@link collectLocalNames}: the name and its token. */
export interface LocalDeclaration {
  name: string;
  token: Token;
}

/**
 * Scans the leading run of `param`/`let` statements — the document's preamble — for every
 * `let NAME = …` declaration, in declaration order, first occurrence only. This is the one real
 * definition of "declared": it walks lexer tokens, not raw text, so a `let` that only appears
 * inside a string literal (`s(label = "let x = 1")`) or after the preamble has ended (the rule body has
 * begun) is never mistaken for a declaration — the outer loop stops the moment it meets a token
 * that isn't a preamble `param`/`let` keyword. Tracks paren/brace depth so a group or quantifier
 * body nested inside a `let`'s own expression isn't mistaken for the *next* preamble statement.
 *
 * Takes tokens rather than text so it has no parser dependency; the parser feeds its own token
 * array, and {@link declaredLocals} feeds a fresh tokenization for callers with only text.
 */
export function collectLocalNames(tokens: readonly Token[]): LocalDeclaration[] {
  const declarations: LocalDeclaration[] = [];
  const seen = new Set<string>();
  let i = 0;
  while (i < tokens.length) {
    const token = tokens[i]!;
    if (token.kind !== 'keyword' || (token.value !== 'param' && token.value !== 'let')) break;

    if (token.value === 'let') {
      const nameToken = tokens[i + 1];
      if (nameToken && WORD_KINDS.has(nameToken.kind) && !seen.has(nameToken.value)) {
        seen.add(nameToken.value);
        declarations.push({ name: nameToken.value, token: nameToken });
      }
    }

    // Skip past this statement's body to the next one, tracking bracket depth so a nested
    // group or quantifier body isn't mistaken for the following statement.
    i++;
    let depth = 0;
    while (i < tokens.length) {
      const inner = tokens[i]!;
      if (inner.kind === 'paren' || inner.kind === 'brace') {
        depth += inner.value === '(' || inner.value === '{' ? 1 : -1;
      }
      if (depth <= 0 && inner.kind === 'keyword' && (inner.value === 'param' || inner.value === 'let')) {
        break;
      }
      i++;
    }
  }
  return declarations;
}

/**
 * The names declared by `text`'s preamble, per {@link collectLocalNames} — the single definition
 * of "declared" that `tokenSpans` and `completeDsl` share with the parser itself. Tokenizing text
 * that isn't valid DSL still produces a token stream `collectLocalNames` can walk; it never throws.
 */
export function declaredLocals(text: string): Set<string> {
  return new Set(collectLocalNames(tokenize(text)).map((declaration) => declaration.name));
}
