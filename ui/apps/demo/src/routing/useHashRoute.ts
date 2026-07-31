import { useCallback, useEffect, useState } from 'react';

/** The two pages the demo shell switches between. */
export type Page = 'rules' | 'propositions';

/** Where the user is: which page, and what is selected on it. */
export interface Route {
  page: Page;
  name: string | null;
}

const PAGES: readonly Page[] = ['rules', 'propositions'];
const DEFAULT_ROUTE: Route = { page: 'rules', name: null };

/**
 * Reads a route out of a location hash. Hash routing rather than history routing so a fork needs no
 * server-side fallback to make deep links work — the demo's host happens to have one, but the
 * skeleton should not depend on it.
 */
export function parseHash(hash: string): Route {
  const [page, ...rest] = hash.replace(/^#\/?/, '').split('/');
  if (!page || !PAGES.includes(page as Page)) return DEFAULT_ROUTE;
  const name = rest.join('/');
  return { page: page as Page, name: name === '' ? null : decodeURIComponent(name) };
}

/** The hash for a route. Dots are left unescaped, so a namespaced name stays readable in the bar. */
export function formatHash(route: Route): string {
  return route.name === null
    ? `#/${route.page}`
    : `#/${route.page}/${encodeURIComponent(route.name)}`;
}

/**
 * The current route, and a setter that writes it to the address bar. Listens on `hashchange`, so the
 * back button and a hand-edited URL both work without a router dependency.
 */
export function useHashRoute(): [Route, (route: Route) => void] {
  const [route, setRoute] = useState<Route>(() => parseHash(window.location.hash));

  useEffect(() => {
    const onHashChange = (): void => setRoute(parseHash(window.location.hash));
    window.addEventListener('hashchange', onHashChange);
    // The hash may have moved between the initial render and this effect running (React 18's
    // StrictMode double-invoke, or a same-tick navigation), so resync rather than trust the seed.
    onHashChange();
    return () => window.removeEventListener('hashchange', onHashChange);
  }, []);

  // Writing the hash fires `hashchange`, which is what actually updates the state — so the address
  // bar stays the single source of truth and a manual edit behaves identically to a click.
  const navigate = useCallback((next: Route): void => {
    window.location.hash = formatHash(next);
  }, []);

  return [route, navigate];
}
