import type { KeyboardEvent, MouseEvent } from 'react';
import type { TabId } from './workspace.js';

/**
 * The pointer and keyboard conventions every tab answers to, whether drawn as a chip or as a row
 * of the compact dropdown, so they cannot drift: middle-click closes, Delete closes, ←/→ (↑/↓ in
 * a vertical list) move the roving focus and activate as they go.
 *
 * Every tab carries `data-tab` with its id so the next one can be found and focused without the
 * strip holding a ref per tab.
 */
export function tabInteractions(
  id: TabId,
  tabs: readonly TabId[],
  orientation: 'horizontal' | 'vertical',
  handlers: { onActivate: (id: TabId) => void; onRequestClose: (id: TabId) => void },
) {
  const prevKey = orientation === 'horizontal' ? 'ArrowLeft' : 'ArrowUp';
  const nextKey = orientation === 'horizontal' ? 'ArrowRight' : 'ArrowDown';
  return {
    onClick: () => handlers.onActivate(id),
    onAuxClick: (event: MouseEvent<HTMLElement>) => {
      if (event.button === 1) { event.preventDefault(); handlers.onRequestClose(id); }
    },
    onKeyDown: (event: KeyboardEvent<HTMLElement>) => {
      if (event.key === 'Delete' || event.key === 'Backspace') {
        event.preventDefault();
        handlers.onRequestClose(id);
        return;
      }
      if (event.key !== prevKey && event.key !== nextKey) return;
      event.preventDefault();
      const index = tabs.indexOf(id);
      const next = tabs[(index + (event.key === nextKey ? 1 : -1) + tabs.length) % tabs.length];
      if (next === undefined) return;
      handlers.onActivate(next);
      const list = event.currentTarget.closest('[role="tablist"], [role="menu"]');
      for (const el of list?.querySelectorAll<HTMLElement>('[data-tab]') ?? []) {
        if (el.dataset['tab'] === next) { el.focus(); break; }
      }
    },
  };
}
