import { useMemo } from 'react';
import { RulesApiClient } from '@motiv-rules/core';
import { useHashRoute } from './routing/useHashRoute.js';
import { useDocumentTitle } from './routing/useDocumentTitle.js';
import { WorkspaceShell } from './shell/WorkspaceShell.js';
import { AdminPage } from './panes/AdminPage.js';

/** The model type Studio's rules are validated and evaluated against. */
export const MODEL_TYPE = 'customer';

/**
 * The Studio shell: owns the client and the route, and hands both to the documents shell — or to
 * the admin page, which is still a route of its own.
 */
export function App(props: { client?: RulesApiClient }) {
  // Seam: the transport. A RulesApiClient is the only thing that talks to the backend (GET
  // /catalog, POST /validate, POST /evaluate, the rule and proposition endpoints). Swap baseUrl or
  // inject a custom `fetch` to point at your own host.
  const client = useMemo(
    () => props.client ?? new RulesApiClient({ baseUrl: '/api/rules' }),
    [props.client],
  );

  const [route, navigate] = useHashRoute();
  // The document has one <title>, so the title has to follow the route: it is what a screen
  // reader announces on navigation and what tells two Studio history entries apart.
  useDocumentTitle(route);

  return (
    <main className="app">
      {route.page === 'admin'
        ? <AdminPage />
        : <WorkspaceShell client={client} route={route} navigate={navigate} />}
    </main>
  );
}
