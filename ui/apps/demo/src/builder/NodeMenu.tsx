import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from 'react';
import { useRuleEditorStore } from '@motiv/rules-react';
import { placePopover } from '../dsl/popoverPlacement.js';

/** One entry in a node's actions menu. */
interface MenuAction {
  label: string;
  run: () => void;
}

/** A card that has yet to be measured is laid out off-screen rather than flashing in the wrong place. */
const UNMEASURED: CSSProperties = { position: 'fixed', top: 0, left: 0, visibility: 'hidden' };

/**
 * A node's actions, opened from the `⋯` on its summary row.
 *
 * Structural operations live here rather than in the detail panel. A panel that could remove its
 * own node — or change the node's kind — would re-render into something else mid-interaction, the
 * reveal invalidating its own trigger. A menu has no such problem: it is transient by
 * construction, closing as soon as an item is chosen, so the row it acted on is free to become
 * something else or to disappear.
 *
 * The menu is identical on every node kind, so there is one behaviour to learn regardless of what
 * the row holds. `Details` duplicates a leaf's caret deliberately: it means any row can be driven
 * entirely from the menu, which is the point of having one for touch.
 */
export function NodeMenu(props: {
  path: string;
  canRemove: boolean;
  open: boolean;
  onDetails: () => void;
  /** Opens this menu, or closes whichever is open. Held by the host so only one is ever open. */
  setOpen: (open: boolean) => void;
}) {
  const { path, canRemove, open, onDetails, setOpen } = props;
  const store = useRuleEditorStore();

  const [style, setStyle] = useState<CSSProperties>(UNMEASURED);

  const trigger = useRef<HTMLButtonElement | null>(null);
  const card = useRef<HTMLDivElement | null>(null);

  /** Closes the menu and returns the keyboard to the control that opened it. */
  const close = (): void => {
    setOpen(false);
    setStyle(UNMEASURED);
    trigger.current?.focus();
  };

  const actions: MenuAction[] = [
    { label: 'Details', run: onDetails },
    ...(canRemove ? [{ label: 'Remove', run: () => store.removeOperand(path) }] : []),
  ];

  // Measured after paint, so the card's natural size is known before it is positioned. The
  // placement math is the DSL pane's, which already handles flipping above a low anchor and
  // clamping into the viewport.
  useLayoutEffect(() => {
    const anchor = trigger.current?.getBoundingClientRect();
    const box = card.current?.getBoundingClientRect();
    if (!open || !anchor || !box) return;
    const placed = placePopover(
      { top: anchor.top, bottom: anchor.bottom, left: anchor.left },
      { width: box.width, height: box.height },
      { width: window.innerWidth, height: window.innerHeight, minTop: 0 },
    );
    setStyle({ position: 'fixed', top: placed.top, left: placed.left, maxHeight: placed.maxHeight });
  }, [open]);

  useEffect(() => {
    if (!open) return;

    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') close();
    };
    // `mousedown`, not `click`: a click that starts outside and ends on the menu would otherwise
    // dismiss and re-target, and the menu should be gone before anything under it is pressed.
    const onPointerDown = (event: MouseEvent): void => {
      const target = event.target as Node;
      if (card.current?.contains(target) || trigger.current?.contains(target)) return;
      setOpen(false);
    };

    document.addEventListener('keydown', onKeyDown);
    document.addEventListener('mousedown', onPointerDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('mousedown', onPointerDown);
    };
  }, [open]);

  return (
    <>
      <button
        ref={trigger}
        type="button"
        className={open ? 'node-menu-trigger open' : 'node-menu-trigger'}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`actions for ${path}`}
        onClick={() => setOpen(!open)}
      >
        ⋯
      </button>
      {open && (
        <div ref={card} role="menu" className="node-menu" style={style} aria-label={`actions for ${path}`}>
          {actions.map((action) => (
            <button
              key={action.label}
              type="button"
              role="menuitem"
              className="node-menu-item"
              onClick={() => { action.run(); close(); }}
            >
              {action.label}
            </button>
          ))}
        </div>
      )}
    </>
  );
}
