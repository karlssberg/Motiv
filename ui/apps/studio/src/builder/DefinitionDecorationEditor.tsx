import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';

/**
 * A definition's `whenTrue` / `whenFalse`, shown in its body root's detail panel — the same place
 * the rule's root shows its own decoration, so a definition reads and edits like the rule (#234).
 *
 * No Name field: a definition's name is the row's own input above the tree, and renaming it
 * rewrites every reference, which is not something a decoration field should do on each keystroke.
 */
export function DefinitionDecorationEditor(props: { name: string }) {
  const { name } = props;
  const store = useRuleEditorStore();
  const { document } = useRuleEditor(store);
  const definition = document.definitions?.[name];
  if (!definition) return null;

  return (
    <div className="decoration">
      <label className="field">
        <span>When true</span>
        <input
          aria-label={`whenTrue of definition ${name}`}
          className="control"
          type="text"
          value={typeof definition.whenTrue === 'string' ? definition.whenTrue : ''}
          onChange={(e) => store.setDefinitionDecoration(name, { whenTrue: e.target.value || undefined })}
        />
      </label>
      <label className="field">
        <span>When false</span>
        <input
          aria-label={`whenFalse of definition ${name}`}
          className="control"
          type="text"
          value={typeof definition.whenFalse === 'string' ? definition.whenFalse : ''}
          onChange={(e) => store.setDefinitionDecoration(name, { whenFalse: e.target.value || undefined })}
        />
      </label>
    </div>
  );
}
