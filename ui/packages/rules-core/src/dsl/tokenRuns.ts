import { declaredLocals } from './locals.js';
import { tokenize } from './lexer.js';
import type { TokenKind } from './types.js';

/** One rendered run of DSL text: a lexed token, or the gap of whitespace before it. */
export interface TokenSpan {
  key: string;
  kind: TokenKind | 'local' | 'gap';
  value: string;
}

/**
 * Splits DSL text into renderable runs. The lexer skips whitespace, so the gaps between tokens
 * are re-inserted verbatim — a row that dropped them would render `a&b` for `a & b`, and the
 * text is the node's only visible description once its subtree is collapsed.
 *
 * A `spec`-kind token whose value is a declared local is rendered as `'local'` instead, so a
 * reference to a `let` name reads differently from a catalog spec reference. `locals` defaults to
 * {@link declaredLocals} scraped from `text` itself; a caller that already knows the document's
 * locals (e.g. from a completed parse) can pass them instead of re-scraping.
 */
export function tokenSpans(text: string, locals?: ReadonlySet<string>): TokenSpan[] {
  const names = locals ?? declaredLocals(text);
  const spans: TokenSpan[] = [];
  let cursor = 0;
  for (const token of tokenize(text)) {
    if (token.from > cursor) {
      spans.push({ key: `gap-${cursor}`, kind: 'gap', value: text.slice(cursor, token.from) });
    }
    const kind = token.kind === 'spec' && names.has(token.value) ? 'local' : token.kind;
    spans.push({ key: `${token.kind}-${token.from}`, kind, value: token.value });
    cursor = token.to;
  }
  if (cursor < text.length) {
    spans.push({ key: `gap-${cursor}`, kind: 'gap', value: text.slice(cursor) });
  }
  return spans;
}
