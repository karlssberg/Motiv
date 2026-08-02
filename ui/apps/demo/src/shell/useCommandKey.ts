import { useEffect, useRef } from 'react';

/**
 * Whether a modal is already on screen.
 *
 * Asked of the document rather than tracked in state, because the top layer is where the answer
 * actually lives: every modal in the app is a `<dialog>` opened with `showModal()`, and `open` is
 * exactly what that sets. A page enumerating its own modal flags instead would have to be kept in
 * step by hand — `PropositionsPage` holds three — and that is the drift this shared hook exists to
 * prevent.
 *
 * The guard matters because the chord is bound on `window` and a keydown inside a modal still
 * bubbles there. Without it, ⌘K stacks a palette over whatever is already showing, and choosing a
 * row navigates the page underneath — discarding a half-filled authoring form. `openDialog` refuses
 * to stack in the other direction for the same reason.
 */
function aModalIsShowing(): boolean {
  return document.querySelector('dialog[open]') !== null;
}

/**
 * ⌘K / Ctrl-K opens the page's command palette.
 *
 * The palette is the only way to a page's listing, so it needs a key of its own — hunting for a
 * toolbar button is what a shortcut exists to avoid. `preventDefault` because the chord is bound
 * in browsers (the address bar's search) and that must not fire as well.
 *
 * Bound on `window` rather than on a page's own tree: the key has to work wherever focus happens
 * to be, including inside the editor surfaces, which are not descendants of either page's chrome.
 *
 * `open` is read back through a ref so the listener is attached once for the page's lifetime.
 * Callers pass an inline arrow, so a plain `[open]` dependency would detach and reattach on every
 * render — the same reason `CommandPalette` keeps `props.match` out of its memo's dependencies.
 *
 * Inert while any modal is already showing — see {@link aModalIsShowing}.
 */
export function useCommandKey(open: () => void): void {
  const latest = useRef(open);
  latest.current = open;

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        // Claimed from the browser either way: whether or not this page acts on the chord, the
        // address bar's search must not open over an app that binds it.
        event.preventDefault();
        if (aModalIsShowing()) return;
        latest.current();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);
}
