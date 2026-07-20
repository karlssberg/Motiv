import { useEffect, useState } from 'react';
import type { Catalog, RulesApiClient } from '@motiv/rules-core';

/** The state of an async catalog load. */
export type CatalogState =
  | { status: 'loading' }
  | { status: 'ready'; data: Catalog }
  | { status: 'error'; error: unknown };

/** Loads the spec catalog once per client and tracks its async state. */
export function useCatalog(client: RulesApiClient): CatalogState {
  const [state, setState] = useState<CatalogState>({ status: 'loading' });

  useEffect(() => {
    let active = true;
    setState({ status: 'loading' });
    client.getCatalog()
      .then((data) => { if (active) setState({ status: 'ready', data }); })
      .catch((error: unknown) => { if (active) setState({ status: 'error', error }); });
    return () => { active = false; };
  }, [client]);

  return state;
}
