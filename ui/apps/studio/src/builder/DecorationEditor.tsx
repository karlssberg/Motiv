import type { RuleNode } from '@motiv-rules/core';
import { useRuleEditorStore } from '@motiv-rules/react';
import type { DecorationPatch } from '../decorationPatch.js';
import { PayloadDisclosurePrototype } from './PayloadDisclosure.prototype.js';

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
      {/* PROTOTYPE: the whenTrue/whenFalse boxes behind a disclosure — see the .prototype file. */}
      <PayloadDisclosurePrototype
        statement={node.name}
        whenTrue={typeof node.whenTrue === 'string' ? node.whenTrue : ''}
        whenFalse={typeof node.whenFalse === 'string' ? node.whenFalse : ''}
        scope={`at ${path}`}
        onChange={(patch) => store.setDecoration(path, patch as DecorationPatch)}
      />
    </div>
  );
}
