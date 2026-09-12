import type { Page } from '../routing/useHashRoute.js';

/**
 * What each page last had open, so that switching pages and back lands where the user left off.
 *
 * The page switch is a link to a bare page (`#/propositions`), which is what drops the selection —
 * and the page's editor with it, since a page with nothing open shows nothing rather than the
 * shared store's stale document. Remembering the selection here lets `AppBar` mint the link *with*
 * the name, so the route round-trips: leaving and returning is the same as never having left.
 *
 * `sessionStorage`, not React state: the app bar is mounted afresh by every page, so state would
 * have to be threaded through `App` into three parents for one string — and a tab that reloads
 * keeps its place for free. Session-scoped on purpose: two tabs are two sessions, and a selection
 * should not leak from one to the other. Every access is guarded, because storage can be denied
 * (a locked-down browser, a sandboxed frame) and the shell must still switch pages without it.
 */
const KEY = (page: Page): string => `motiv.studio.last.${page}`;

export function rememberSelection(page: Page, name: string | null): void {
  try {
    if (name === null) sessionStorage.removeItem(KEY(page));
    else sessionStorage.setItem(KEY(page), name);
  } catch {
    // No storage: the link falls back to the bare page, which is what it always was.
  }
}

export function recallSelection(page: Page): string | null {
  try {
    return sessionStorage.getItem(KEY(page));
  } catch {
    return null;
  }
}
