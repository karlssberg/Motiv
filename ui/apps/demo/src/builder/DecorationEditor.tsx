import type { RuleNode } from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import type { DecorationPatch } from '../decorationPatch.js';

/** Editable name/whenTrue/whenFalse decoration fields for the node at a path. */
export function DecorationEditor(props: { path: string; node: RuleNode }) {
  const { path, node } = props;
  const store = useRuleEditorStore();

  return (
    <div className="decoration">
      <label className="field">
        <span>Name</span>
        <input
          aria-label={`name at ${path}`}
          className="control"
          type="text"
          value={node.name ?? ''}
          onChange={(e) => store.setName(path, e.target.value || undefined)}
        />
      </label>
      <label className="field">
        <span>When true</span>
        <input
          aria-label={`whenTrue at ${path}`}
          className="control"
          type="text"
          value={typeof node.whenTrue === 'string' ? node.whenTrue : ''}
          onChange={(e) => store.setDecoration(path, { whenTrue: e.target.value || undefined } as DecorationPatch)}
        />
      </label>
      <label className="field">
        <span>When false</span>
        <input
          aria-label={`whenFalse at ${path}`}
          className="control"
          type="text"
          value={typeof node.whenFalse === 'string' ? node.whenFalse : ''}
          onChange={(e) => store.setDecoration(path, { whenFalse: e.target.value || undefined } as DecorationPatch)}
        />
      </label>
    </div>
  );
}
