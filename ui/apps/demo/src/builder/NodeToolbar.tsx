import { isSpecNode, type RuleNode } from '@motiv-rules/core';

/**
 * The extension points offered beside a node's decoration fields. Currently only the disabled
 * `expression` affordance, which is documented as requiring backend support.
 *
 * Nothing here may change the node it belongs to. A control that re-kinded the node — wrapping a
 * leaf in AND, negating it, removing it — would re-render the panel that hosts it into a panel
 * for something else, so the reveal would invalidate its own trigger. Composition is authored by
 * typing DSL in the node's row, where the change and the thing changed are the same surface.
 */
export function NodeToolbar(props: { path: string; node: RuleNode }) {
  const { node } = props;

  if (!isSpecNode(node)) return null;

  return (
    <div className="node-toolbar">
      <button
        type="button"
        className="btn ext-point"
        disabled
        title="requires backend (coming)"
      >
        expression — coming
      </button>
    </div>
  );
}
