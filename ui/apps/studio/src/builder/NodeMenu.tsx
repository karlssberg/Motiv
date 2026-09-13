import type { MutableRefObject } from 'react';
import { useRuleEditorStore } from '@motiv-rules/react';
import { usePopoverCard } from './usePopoverCard.js';
import { IconMore } from '../shell/icons.js';

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
  /**
   * Opens an insertion slot at index 0 of this node's operand list. Absent on rows with no list.
   *
   * Here rather than beside the row's `+` because a single button per row cannot address both the
   * list a row belongs to and the list it owns — and because the menu is already where this builder
   * puts structural actions, so the item is self-labelling where a second glyph would not be.
   */
  onInsertFirst?: () => void;
  /**
   * Turns this node into a document-local definition, named by a prompt the host opens. Offered
   * on every node but the root — which is the rule itself — and on no `local`, which already is
   * one (#234).
   */
  onExtractLocal?: () => void;
  /** Promotes this node into a catalog proposition. Present only where the host offers the dialog. */
  onExtractCatalog?: () => void;
  /** Dissolves a `local` reference back into the tree. Local rows only. */
  onInline?: () => void;
  /**
   * Handed this menu's `⋯`, so a card the host opens from one of these items can anchor to it.
   * The menu is gone by then — the item that opened the card closed it — so the trigger is the
   * only thing left on the row that the card can be measured against.
   */
  triggerRef?: MutableRefObject<HTMLButtonElement | null>;
}) {
  const {
    path, canRemove, open, onDetails, setOpen, onInsertFirst,
    onExtractLocal, onExtractCatalog, onInline, triggerRef,
  } = props;
  const store = useRuleEditorStore();
  const { trigger, card, style, close } = usePopoverCard(open, setOpen);

  const actions: MenuAction[] = [
    { label: 'Details', run: onDetails },
    ...(onInsertFirst ? [{ label: 'Insert first operand', run: onInsertFirst }] : []),
    ...(onExtractLocal ? [{ label: 'Extract to definition', run: onExtractLocal }] : []),
    ...(onExtractCatalog ? [{ label: 'Extract to catalog…', run: onExtractCatalog }] : []),
    ...(onInline ? [{ label: 'Inline', run: onInline }] : []),
    ...(canRemove ? [{ label: 'Remove', run: () => store.removeOperand(path) }] : []),
  ];

  return (
    <>
      <button
        ref={(element) => {
          trigger.current = element;
          if (triggerRef) triggerRef.current = element;
        }}
        type="button"
        className={open ? 'node-menu-trigger open' : 'node-menu-trigger'}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`actions for ${path}`}
        onClick={() => setOpen(!open)}
      >
        <IconMore size={14} />
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
