import { useEffect, useMemo } from 'react';
import { RuleEditorStore, RulesApiClient, createValidationController } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from './panes/BuilderPane.js';
import { JsonPane } from './panes/JsonPane.js';
import { EvaluatePane } from './panes/EvaluatePane.js';

const MODEL_TYPE = 'customer';

/** The demo shell: owns the store + client, runs debounced validation, and lays out the three panes. */
export function App(props: { client?: RulesApiClient; store?: RuleEditorStore }) {
  const store = useMemo(
    () => props.store ?? new RuleEditorStore({ rule: { spec: 'is-active' } }),
    [props.store],
  );
  // Seam: the transport. A RulesApiClient is the only thing that talks to the
  // backend (GET /catalog, POST /validate, POST /evaluate). Swap baseUrl or inject
  // a custom `fetch` to point at your own host.
  const client = useMemo(
    () => props.client ?? new RulesApiClient({ baseUrl: '/api/rules' }),
    [props.client],
  );

  // Seam: live validation. Debounces edits to the store and pushes the document to
  // /validate, writing errors back onto the store for the panes to render.
  useEffect(
    () => createValidationController(store, client, { modelType: MODEL_TYPE, debounceMs: 300 }),
    [store, client],
  );

  return (
    // Seam: the store hookup. RuleEditorProvider exposes the single RuleEditorStore
    // to every builder component (useRuleEditorStore / useRuleNode) below it.
    <RuleEditorProvider store={store}>
      <main className="app">
        <BuilderPane client={client} />
        <JsonPane />
        <EvaluatePane client={client} />
      </main>
    </RuleEditorProvider>
  );
}

export { MODEL_TYPE };
