import { useEffect, useRef } from 'react';

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
 */
export function useCommandKey(open: () => void): void {
  const latest = useRef(open);
  latest.current = open;

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        latest.current();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);
}
