import type { NodeSpan } from './types.js';

/** A half-open source range `[from, to)`. */
export interface SourceRange {
  from: number;
  to: number;
}

/** The path one level up, or null once the root is reached. */
function parentPath(path: string): string | null {
  const index = path.lastIndexOf('.');
  return index <= 0 ? null : path.slice(0, index);
}

/**
 * The span recorded for `path`, or for its nearest ancestor that has one — so a sub-field path
 * like `$.rule.whenTrue` anchors on the node that owns it. Falls back to the whole document.
 *
 * The parser guarantees one span per path, widened to cover any parentheses and `as` clause, so a
 * grouped subtree resolves to a range including its parens rather than to the bare inner text.
 */
export function rangeOfPath(
  path: string,
  spans: readonly NodeSpan[],
  documentLength: number,
): SourceRange {
  for (let current: string | null = path; current !== null; current = parentPath(current)) {
    const span = spans.find((candidate) => candidate.path === current);
    if (span) return { from: span.from, to: span.to };
  }
  return { from: 0, to: documentLength };
}
