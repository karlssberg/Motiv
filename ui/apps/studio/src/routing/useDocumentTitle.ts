import { useEffect } from 'react';
import type { Page, Route } from './useHashRoute.js';

/** The product name every title ends with, so a tab is identifiable as Studio's at a glance. */
const PRODUCT = 'Motiv Studio';

/** What each page is called in a title — the same words the page switcher uses. */
const PAGE_NAMES: Record<Page, string> = {
  rules: 'Rules',
  propositions: 'Propositions',
  admin: 'Admin',
};

/**
 * The title for a route: the selection, then the page, then the product.
 *
 * Most specific first, because every surface that shows a title truncates from the right — browser
 * tabs, the history list, a screen reader's window announcement — and the half worth keeping is
 * which proposition is open, not which application it is open in.
 */
export function titleFor(route: Route): string {
  const page = PAGE_NAMES[route.page];
  const name = route.name?.trim() ?? '';
  return name === '' ? `${page} — ${PRODUCT}` : `${name} — ${page} — ${PRODUCT}`;
}

/**
 * Keeps `document.title` describing the current route (WCAG 2.1 SC 2.4.2, Page Titled).
 *
 * A hash-routed single-page application has one document and one `<title>`, so without this every
 * route shares the name in `index.html`. That passes a scan — axe's `document-title` rule asks only
 * that a title exist — while costing the two places a title is actually used: what a screen reader
 * announces when the route changes, and how a user tells four Studio entries in their history apart.
 * The enumeration in `a11y/conformance.ts` is what surfaced it; a green sweep never would have.
 */
export function useDocumentTitle(route: Route): void {
  useEffect(() => {
    document.title = titleFor(route);
  }, [route.page, route.name]);
}
