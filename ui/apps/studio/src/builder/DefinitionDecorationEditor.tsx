import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { PayloadFields } from './PayloadFields.js';

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
      <PayloadFields
        statement={name}
        whenTrue={typeof definition.whenTrue === 'string' ? definition.whenTrue : ''}
        whenFalse={typeof definition.whenFalse === 'string' ? definition.whenFalse : ''}
        scope={`of definition ${name}`}
        onChange={(patch) => store.setDefinitionDecoration(name, patch)}
      />
    </div>
  );
}
