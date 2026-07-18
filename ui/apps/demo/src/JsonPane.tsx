import { useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';

/** Shows the live rule document as formatted JSON and lists current validation errors. */
export function JsonPane() {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);

  return (
    <section aria-label="Document">
      <h2>Document</h2>
      <pre aria-label="rule document">{JSON.stringify(state.document, null, 2)}</pre>
      {state.errors.length > 0 && (
        <ul aria-label="validation errors">
          {state.errors.map((error, i) => (
            <li key={`${error.path}-${i}`} role="alert">{error.code} at {error.path}: {error.message}</li>
          ))}
        </ul>
      )}
    </section>
  );
}
