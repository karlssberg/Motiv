import {
  definitionBodyPath, printInline, tokenSpans, type Catalog, type LocalNode,
} from '@motiv-rules/core';
import { useRuleEditor, useRuleEditorStore, useRuleNode } from '@motiv-rules/react';

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
        <button
          type="button"
          className="btn"
          aria-label={`inline ${path}`}
          onClick={() => store.inlineLocal(path)}
        >
          Inline
        </button>
        <button
          type="button"
          className="btn"
          aria-label={`go to definition ${path}`}
          // The definitions panel is the other half of this (Task 11); until its rows are on
          // screen there is nowhere to go, and a control that throws would be worse than one that
          // quietly does nothing.
          onClick={() => {
            const row = document.getElementById(`definition-${node.local}`);
            row?.scrollIntoView({ block: 'nearest' });
            row?.focus();
          }}
        >
          Go to definition
        </button>
      </div>
    </div>
  );
}
