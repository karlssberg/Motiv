/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * The pointer and keyboard conventions every variant's tab answers to, so they cannot drift:
 * middle-click closes, Delete closes, Enter/Space activates, ←/→ (or ↑/↓ in a vertical list)
 * move between tabs with roving focus.
 */
import type { KeyboardEvent, MouseEvent } from 'react';
import type { TabId } from './workspace.js';

export function tabInteractions(
  id: TabId,
  tabs: TabId[],
  orientation: 'horizontal' | 'vertical',
  handlers: { onActivate: (id: TabId) => void; onRequestClose: (id: TabId) => void },
) {
  const prevKey = orientation === 'horizontal' ? 'ArrowLeft' : 'ArrowUp';
  const nextKey = orientation === 'horizontal' ? 'ArrowRight' : 'ArrowDown';
  return {
    onClick: () => handlers.onActivate(id),
    onAuxClick: (event: MouseEvent<HTMLElement>) => { if (event.button === 1) { event.preventDefault(); handlers.onRequestClose(id); } },
    onKeyDown: (event: KeyboardEvent<HTMLElement>) => {
      const index = tabs.indexOf(id);
      if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); handlers.onActivate(id); }
      else if (event.key === 'Delete' || event.key === 'Backspace') { event.preventDefault(); handlers.onRequestClose(id); }
      else if (event.key === prevKey || event.key === nextKey) {
        event.preventDefault();
        const next = tabs[(index + (event.key === nextKey ? 1 : -1) + tabs.length) % tabs.length];
        if (next) {
          handlers.onActivate(next);
          (event.currentTarget.parentElement?.querySelector(`[data-tab="${CSS.escape(next)}"]`) as HTMLElement | null)?.focus();
        }
      }
    },
  };
}
