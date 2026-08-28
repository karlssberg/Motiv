import { useEffect, useMemo, useState } from 'react';
import { RuleEditorStore, RulesApiClient, createValidationController } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { useHashRoute, type Page } from './routing/useHashRoute.js';
import { RulesPage } from './panes/RulesPage.js';
import { PropositionsPage } from './panes/PropositionsPage.js';
import { AdminPage } from './panes/AdminPage.js';

const MODEL_TYPE = 'customer';

/** The Studio shell: owns the store + client, runs debounced validation, and lays out the three panes. */
export function App(props: { client?: RulesApiClient; store?: RuleEditorStore }) {
  const store = useMemo(
    () => props.store ?? new RuleEditorStore({ rule: { spec: 'customer.is-active' } }),
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
  // /validate, writing errors back onto the store for the panes to render. When the
  // loaded rule is async, validation allows async spec references too.
  const [isAsync, setIsAsync] = useState(false);
  useEffect(
    () => createValidationController(store, client, { modelType: MODEL_TYPE, debounceMs: 300, isAsync }),
    [store, client, isAsync],
  );

  const [route, navigate] = useHashRoute();
  // Switching page always drops the selection: a rule name means nothing on the propositions page.
  const goToPage = (page: Page): void => navigate({ page, name: null });

  let page: JSX.Element;
  if (route.page === 'propositions') {
    page = (
      <PropositionsPage
        client={client}
        page={route.page}
        selected={route.name}
        onNavigate={goToPage}
        onSelect={(name) => navigate({ page: 'propositions', name })}
      />
    );
  } else if (route.page === 'admin') {
    page = <AdminPage page={route.page} onNavigate={goToPage} />;
  } else {
    page = (
      <RulesPage
        client={client}
        page={route.page}
        onNavigate={goToPage}
        onLoaded={(entry) => setIsAsync(entry?.isAsync ?? false)}
      />
    );
  }

  return (
    // Seam: the store hookup. RuleEditorProvider exposes the single RuleEditorStore
    // to every builder component (useRuleEditorStore / useRuleNode) below it.
    <RuleEditorProvider store={store}>
      <main className="app">
        {page}
      </main>
    </RuleEditorProvider>
  );
}

export { MODEL_TYPE };
