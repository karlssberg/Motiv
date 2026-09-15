import { useCallback, useEffect, useState, type MouseEvent, type ReactNode } from 'react';

/** A pop-up menu's open state, and the two ways it is worked: its trigger, and closing it. */
export interface MenuState {
  open: boolean;
  close: () => void;
  /** The trigger's click: opens or shuts the menu, and does not count as a click out of it. */
  toggle: (event: MouseEvent<HTMLElement>) => void;
}

/**
 * A menu that a click anywhere else — or Escape — closes, which is what every menu in the shell
 * wants. The trigger's own click is stopped where it happens, so the click that opens a menu does
 * not reach the window listener put in place for it and shut it again.
 */
export function useMenu(): MenuState {
  const [open, setOpen] = useState(false);
  useEffect(() => {
    if (!open) return;
    const close = (): void => setOpen(false);
    const onKey = (event: KeyboardEvent): void => { if (event.key === 'Escape') close(); };
    window.addEventListener('click', close);
    window.addEventListener('keydown', onKey);
    return () => { window.removeEventListener('click', close); window.removeEventListener('keydown', onKey); };
  }, [open]);

  // Stable, so an effect may depend on `close` without re-running — and re-closing — every render.
  const close = useCallback((): void => setOpen(false), []);
  const toggle = useCallback((event: MouseEvent<HTMLElement>): void => {
    event.stopPropagation();
    setOpen((was) => !was);
  }, []);

  return { open, close, toggle };
}

/** The list a `useMenu` menu drops: a click inside it is not a click out. */
export function MenuList(props: { label: string; className?: string; children: ReactNode }) {
  return (
    <ul
      role="menu"
      className={`overflow-menu${props.className ? ` ${props.className}` : ''}`}
      aria-label={props.label}
      onClick={(event) => event.stopPropagation()}
    >
      {props.children}
    </ul>
  );
}
