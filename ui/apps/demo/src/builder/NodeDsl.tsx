import { printInline, type Catalog, type RuleNode } from '@motiv/rules-core';
import { tokenSpans } from './dslTokens.js';

/**
 * A node rendered as one line of DSL — what a leaf always shows, and what a parent shows once
 * its subtree is collapsed.
 *
 * The text is `printInline`'s output, which the parser accepts verbatim, so the row is safe to
 * hand back after an edit. It is truncated with an ellipsis rather than wrapped, so a long
 * expression cannot push the row's controls out of reach.
 */
export function NodeDsl(props: { path: string; node: RuleNode; modelType: string; catalog: Catalog }) {
  const { path, node } = props;
  const text = printInline(node);

  return (
    <span className="node-dsl" aria-label={`expression at ${path}`}>
      {tokenSpans(text).map((span) => (
        <span key={span.key} className={`tok-${span.kind}`}>{span.value}</span>
      ))}
    </span>
  );
}
