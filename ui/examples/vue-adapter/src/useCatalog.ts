import { onScopeDispose, shallowRef, toValue, watch, type MaybeRefOrGetter, type ShallowRef } from 'vue';
import type { Catalog, RulesApiClient } from '@motiv-rules/core';

/** The state of an async catalog load. */
export type CatalogState =
  | { status: 'loading' }
  | { status: 'ready'; data: Catalog }
  | { status: 'error'; error: unknown };

/** Loads the spec catalog once per client and tracks its async state. */
export function useCatalog(client: MaybeRefOrGetter<RulesApiClient>): Readonly<ShallowRef<CatalogState>> {
  const state = shallowRef<CatalogState>({ status: 'loading' });

  // One generation per load. A late reply from a client that has since been replaced — or from
  // one still in flight when the scope ended — is dropped rather than written over the current
  // state, which is the whole of what React's `active` flag buys in the same hook.
  let generation = 0;
  const load = (from: RulesApiClient): void => {
    const mine = ++generation;
    state.value = { status: 'loading' };
    from.getCatalog()
      .then((data) => { if (mine === generation) state.value = { status: 'ready', data }; })
      .catch((error: unknown) => { if (mine === generation) state.value = { status: 'error', error }; });
  };

  load(toValue(client));
  watch(() => toValue(client), load, { flush: 'sync' });
  onScopeDispose(() => { generation++; });

  return state;
}
