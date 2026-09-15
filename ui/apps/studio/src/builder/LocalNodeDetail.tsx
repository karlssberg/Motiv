import {
  definitionBodyPath, printInline, tokenSpans, type Catalog, type LocalNode,
} from '@motiv-rules/core';
import { useRuleEditor, useRuleEditorStore, useRuleNode } from '@motiv-rules/react';
import { Tooltip } from '../shell/Tooltip.js';

/**
 * What a `let` row discloses: the definition it stands for, and the two ways out of it (#234).
 *
 * Read-only deliberately. A definition has exactly one home — its row in the definitions panel —
 * and editing it from a reference would mean every reference is also an editor for the same text,
 * with no indication that the edit lands on all of them. So this shows the body and offers to go
 * where it is authored, or to dissolve the reference back into the tree.
 *
 * The body is read reactively at the definition's own path rather than passed in, so an edit made
 * in the definitions panel is reflected here without the reference having changed at all.
 */
export function LocalNodeDetail(props: { path: string; node: LocalNode; catalog: Catalog }) {
  const { path, node, catalog } = props;
  const store = useRuleEditorStore();
  const { node: body } = useRuleNode(definitionBodyPath(node.local));
  // A definition's body can itself reference another local (#234) — without telling `tokenSpans`
  // the document's full set of definition names, that reference would colour as an ordinary spec.
  // Named `ruleDocument` rather than `document`: the DOM global of that name is still needed below,
  // for `getElementById`, and shadowing it silently broke that call once before.
  const { document: ruleDocument } = useRuleEditor(store);
  const locals = new Set(Object.keys(ruleDocument.definitions ?? {}));

  return (
    <div className="node-local-detail">
      <p className="caption">definition of {node.local}</p>
      <code className="node-local-body">
        {body
          ? tokenSpans(printInline(body, { catalog }), locals).map((span) => (
            <span key={span.key} className={`tok-${span.kind}`}>{span.value}</span>
          ))
          // A reference with no definition behind it is a broken document rather than an empty
          // one, and saying so beats rendering a blank line.
          : <span className="error">no definition named {node.local}</span>}
      </code>
      <div className="node-local-actions">
        <Tooltip text="Replace this reference with the definition's own expression"><button
          type="button"
          className="btn"
          aria-label={`inline ${path}`}
          onClick={() => store.inlineLocal(path)}
        >
          Inline
        </button></Tooltip>
        <Tooltip text="Jump to the definition"><button
          type="button"
          className="btn"
          aria-label={`go to definition ${path}`}
          // Lands in the row's name field, not on the row: a focused row shows nothing (script
          // focus after a click draws no ring), so the button read as inert, while a caret in the
          // name is unmistakable and is the field an author most often came to edit. Centred, so
          // the row is not left flush with the bottom edge. Without a definitions panel mounted
          // there is nowhere to go, and doing nothing beats throwing.
          onClick={() => {
            const row = document.getElementById(`definition-${node.local}`);
            if (!row) return;
            row.scrollIntoView({ block: 'center' });
            (row.querySelector<HTMLElement>('input') ?? row).focus();
          }}
        >
          Go to definition
        </button></Tooltip>
      </div>
    </div>
  );
}
