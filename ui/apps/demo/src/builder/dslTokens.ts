import { tokenize, type TokenKind } from '@motiv-rules/core';

/** One rendered run of DSL text: a lexed token, or the gap of whitespace before it. */
export interface TokenSpan {
  key: string;
  kind: TokenKind | 'gap';
  value: string;
}

/**
 * Splits DSL text into renderable runs. The lexer skips whitespace, so the gaps between tokens
 * are re-inserted verbatim — a row that dropped them would render `a&b` for `a & b`, and the
 * text is the node's only visible description once its subtree is collapsed.
 */
export function tokenSpans(text: string): TokenSpan[] {
  const spans: TokenSpan[] = [];
  let cursor = 0;
  for (const token of tokenize(text)) {
    if (token.from > cursor) {
      spans.push({ key: `gap-${cursor}`, kind: 'gap', value: text.slice(cursor, token.from) });
    }
    spans.push({ key: `${token.kind}-${token.from}`, kind: token.kind, value: token.value });
    cursor = token.to;
  }
  if (cursor < text.length) {
    spans.push({ key: `gap-${cursor}`, kind: 'gap', value: text.slice(cursor) });
  }
  return spans;
}
