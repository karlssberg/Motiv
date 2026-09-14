import type { RuleNode } from '@motiv-rules/core';
import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import type { DecorationPatch } from '../decorationPatch.js';
import { PayloadFields } from './PayloadFields.js';

/**
 * Editable whenTrue/whenFalse payload fields for the node at a path. There is no Name field: the
 * DSL has no inline name, so a name the tree could author would be invisible in the text view yet
 * change the bound `Reason`. A rule is named by its document, and everything below by a definition.
 */
export function DecorationEditor(props: { path: string; node: RuleNode }) {
  const { path, node } = props;
  const store = useRuleEditorStore();
  const { document } = useRuleEditor(store);

  return (
    <div className="decoration">
      <PayloadFields
        // A legacy inline name, where a document still carries one, is what the bound `Reason` says.
        statement={node.name ?? document.name}
        whenTrue={typeof node.whenTrue === 'string' ? node.whenTrue : ''}
        whenFalse={typeof node.whenFalse === 'string' ? node.whenFalse : ''}
        scope={`at ${path}`}
        onChange={(patch) => store.setDecoration(path, patch as DecorationPatch)}
      />
    </div>
  );
}
