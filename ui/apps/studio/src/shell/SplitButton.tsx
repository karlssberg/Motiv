import { useEffect, useId, useState, type KeyboardEvent } from 'react';
import { usePopoverCard } from '../builder/usePopoverCard.js';
import { IconCheck, IconChevronDown, type IconProps } from './icons.js';

/** One way of doing the split button's action: a word, and the consequence it carries. */
export interface SplitVariant {
  id: string;
  label: string;
  /** What choosing it does beyond the label — read out with the item, not hidden in a tooltip. */
  description: string;
}

/**
 * A primary action with variants — Save, and Save & close — after the pattern GitHub uses on
 * "Close issue": one filled button wearing the variant in force, and a chevron beside it opening a
 * radio menu of the rest. The main button runs the default; a menu item runs *its* variant, and
 * the caller may adopt it as the new default (see `DocumentActions`).
 *
 * The menu runs the chosen variant rather than only re-arming the button. Re-arming alone is the
 * part of GitHub's control people trip on — a click that changes a label and does nothing — and
 * the item's description already says what is about to happen.
 *
 * Unavailability follows `Toolbar`'s convention: `aria-disabled` and an early return, never the
 * `disabled` attribute, so the button stays in the tab order and its reason can be heard. The
 * toggle is unavailable with it: there are no options to an action that cannot run.
 *
 * The menu is a `usePopoverCard` card, so it is fixed to the viewport, dismisses on Escape and on
 * a press outside, and hands focus back to the toggle. Arrow keys move between items and wrap;
 * opening lands on the variant in force. `aria-controls` is set only while the menu is mounted.
 */
export function SplitButton(props: {
  id: string;
  icon: (props: IconProps) => JSX.Element;
  variants: readonly SplitVariant[];
  defaultId: string;
  onChoose: (variant: SplitVariant) => void;
  unavailable?: string | undefined;
}) {
  const [open, setOpen] = useState(false);
  const menuId = useId();
  const { trigger, card, style, placed, close } = usePopoverCard(open, setOpen);
  const unavailable = props.unavailable !== undefined;
  const reasonId = `toolbar-${props.id}-reason`;
  const current = props.variants.find((variant) => variant.id === props.defaultId) ?? props.variants[0]!;
  // The toggle and the menu it opens carry the same name: one control, named once.
  const optionsLabel = `${current.label} options`;
  const Icon = props.icon;

  // Focus follows the card being placed, not opened: an unmeasured card is `visibility: hidden`,
  // and a hidden element cannot take focus.
  useEffect(() => {
    if (!open || !placed) return;
    const checked = card.current?.querySelector<HTMLElement>('[aria-checked="true"]');
    (checked ?? card.current?.querySelector<HTMLElement>('[role="menuitemradio"]'))?.focus();
  }, [open, placed, card]);

  const choose = (variant: SplitVariant): void => {
    close();
    props.onChoose(variant);
  };

  const onMenuKeyDown = (event: KeyboardEvent<HTMLDivElement>): void => {
    const items = Array.from(card.current?.querySelectorAll<HTMLElement>('[role="menuitemradio"]') ?? []);
    if (items.length === 0) return;
    // Wraps from either end. With no item focused — a press on the menu's own padding leaves
    // focus on the menu — the first arrow enters from the end it came from: Down lands on the
    // first item, Up on the last.
    const at = items.findIndex((item) => item === document.activeElement);
    const step = (by: number): void => {
      event.preventDefault();
      const next = at < 0 ? (by > 0 ? 0 : items.length - 1) : (at + by + items.length) % items.length;
      items[next]?.focus();
    };
    switch (event.key) {
      case 'ArrowDown': step(1); break;
      case 'ArrowUp': step(-1); break;
      case 'Home': items[0]?.focus(); break;
      case 'End': items[items.length - 1]?.focus(); break;
      default: break;
    }
  };

  return (
    <span className="split">
      <button
        type="button"
        className="btn split-main"
        aria-disabled={unavailable ? true : undefined}
        aria-describedby={unavailable ? reasonId : undefined}
        onClick={() => { if (!unavailable) props.onChoose(current); }}
      >
        <Icon size={14} />
        {current.label}
      </button>
      <button
        ref={trigger}
        type="button"
        className="btn split-toggle"
        aria-label={optionsLabel}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-disabled={unavailable ? true : undefined}
        onClick={() => { if (!unavailable) setOpen(!open); }}
      >
        <IconChevronDown size={14} />
      </button>
      {unavailable && <span id={reasonId} className="sr-only">{props.unavailable}</span>}
      {open && (
        <div
          ref={card}
          id={menuId}
          role="menu"
          className="split-menu"
          style={style}
          // Focusable so a press on its own padding keeps focus inside it — the arrow keys then
          // still reach `onMenuKeyDown` and enter the list from the end they came from.
          tabIndex={-1}
          aria-label={optionsLabel}
          onKeyDown={onMenuKeyDown}
        >
          {props.variants.map((variant) => (
            <button
              key={variant.id}
              type="button"
              role="menuitemradio"
              aria-checked={variant.id === current.id}
              className="split-item"
              tabIndex={-1}
              onClick={() => choose(variant)}
            >
              <span className="split-check"><IconCheck size={14} /></span>
              <span className="split-item-text">
                <span className="split-item-label">{variant.label}</span>
                <span className="split-item-desc">{variant.description}</span>
              </span>
            </button>
          ))}
        </div>
      )}
    </span>
  );
}
