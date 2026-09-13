import { finishLocalName, isValidLocalName, type RuleNode } from '@motiv-rules/core';
import { useRuleEditorStore } from '@motiv-rules/react';
import { extractionSeed } from './extractionSeed.js';

/** Renders a payload for display: a string as itself, anything else as compact JSON. */
function show(payload: unknown): string {
  return typeof payload === 'string' ? payload : JSON.stringify(payload);
}

/**
 * What a nested node's decoration has become: a read-only statement of it, and one way out (#234).
 *
 * Naming a node in place is no longer how a sub-proposition is named — it is a definition now —
 * but documents written before that, and documents arriving from the API, still carry the old
 * form. Dropping it silently would lose an author's words; offering the old editor would keep
 * producing a shape nothing else in the app now speaks. So it is shown as it stands, with the
 * single action that migrates it.
 *
 * There is no naming prompt here, unlike the actions menu's `Extract to definition`: the node
 * already carries the name the author chose, so asking for it again would be asking a question
 * whose answer is on screen. The button is disabled instead when that name cannot be used, and
 * says why — the menu's prompt is then the way through.
 */
export function InlineDecorationNotice(props: { path: string; node: RuleNode }) {
  const { path, node } = props;
  const store = useRuleEditorStore();

  const name = finishLocalName(node.name ?? extractionSeed(node));
  const taken = Object.keys(store.getState().document.definitions ?? {}).includes(name);
  const refusal = taken
    ? `A definition named "${name}" already exists — use Extract to definition to choose another.`
    : !isValidLocalName(name)
      ? 'This name cannot be used for a definition — use Extract to definition to choose another.'
      : undefined;

  return (
    <div className="decoration-notice">
      <p className="decoration-notice-head">
        {node.name !== undefined && <span className="node-name">as &quot;{node.name}&quot;</span>}
        <span className="caption">named here, which definitions have replaced</span>
      </p>
      {node.whenTrue !== undefined && (
        <p className="decoration-notice-payload">when true — {show(node.whenTrue)}</p>
      )}
      {node.whenFalse !== undefined && (
        <p className="decoration-notice-payload">when false — {show(node.whenFalse)}</p>
      )}
      <button
        type="button"
        className="btn"
        aria-label={`extract ${path}`}
        disabled={refusal !== undefined}
        {...(refusal ? { title: refusal } : {})}
        onClick={() => store.defineLocal(path, name)}
      >
        Extract to definition
      </button>
    </div>
  );
}
