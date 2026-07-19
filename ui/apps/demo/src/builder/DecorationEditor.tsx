import type { Decoration, RuleNode } from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';

// The store's Decoration type declares whenTrue/whenFalse as optional Payload fields (no `| undefined`
// in their type), but exactOptionalPropertyTypes then rejects an object literal that explicitly assigns
// `undefined` to clear one. The cast below is the intentional escape hatch for that clear-to-undefined case.
type DecorationPatch = Partial<Pick<Decoration, 'whenTrue' | 'whenFalse'>>;

/** Editable name/whenTrue/whenFalse decoration fields for the node at a path. */
export function DecorationEditor(props: { path: string; node: RuleNode }) {
  const { path, node } = props;
  const store = useRuleEditorStore();
  const decorated = node as RuleNode & { name?: string; whenTrue?: unknown; whenFalse?: unknown };

  return (
    <div className="decoration">
      <label className="field">
        <span>Name</span>
        <input
          aria-label={`name at ${path}`}
          className="control"
          type="text"
          value={typeof decorated.name === 'string' ? decorated.name : ''}
          onChange={(e) => store.setName(path, e.target.value || undefined)}
        />
      </label>
      <label className="field">
        <span>When true</span>
        <input
          aria-label={`whenTrue at ${path}`}
          className="control"
          type="text"
          value={typeof decorated.whenTrue === 'string' ? decorated.whenTrue : ''}
          onChange={(e) => store.setDecoration(path, { whenTrue: e.target.value || undefined } as DecorationPatch)}
        />
      </label>
      <label className="field">
        <span>When false</span>
        <input
          aria-label={`whenFalse at ${path}`}
          className="control"
          type="text"
          value={typeof decorated.whenFalse === 'string' ? decorated.whenFalse : ''}
          onChange={(e) => store.setDecoration(path, { whenFalse: e.target.value || undefined } as DecorationPatch)}
        />
      </label>
    </div>
  );
}
