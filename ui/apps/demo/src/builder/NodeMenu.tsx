import { useRuleEditorStore } from '@motiv/rules-react';
import { usePopoverCard } from './usePopoverCard.js';

/** One entry in a node's actions menu. */
interface MenuAction {
  label: string;
  run: () => void;
}

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
  const { trigger, card, style, close } = usePopoverCard(open, setOpen);

  const actions: MenuAction[] = [
    { label: 'Details', run: onDetails },
    ...(canRemove ? [{ label: 'Remove', run: () => store.removeOperand(path) }] : []),
  ];

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
