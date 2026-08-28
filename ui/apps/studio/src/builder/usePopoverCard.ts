import { useLayoutEffect, useEffect, useRef, useState, type CSSProperties, type MutableRefObject } from 'react';
import { placePopover, type PopoverPlacement } from '../dsl/popoverPlacement.js';

/** Whether a freshly measured placement is the one already applied to the card. */
function samePlacement(a: PopoverPlacement | null, b: PopoverPlacement): boolean {
  return a !== null && a.top === b.top && a.left === b.left && a.maxHeight === b.maxHeight;
}

/** The wiring a row's popup needs: where to put it, and how it goes away. */
export interface PopoverCard {
  /** Attach to the control that opens the card — it is the anchor, and takes focus back on close. */
  trigger: MutableRefObject<HTMLButtonElement | null>;
  /** Attach to the card itself, so it can be measured and so clicks inside it are not dismissals. */
  card: MutableRefObject<HTMLDivElement | null>;
  /** Fixed-position style for the card. Hidden until the first measurement lands. */
  style: CSSProperties;
  /**
   * Whether the card has been measured, and so is on screen where it belongs. A card waiting to
   * be measured is `visibility: hidden`, and a hidden element cannot take focus — so anything
   * moving focus into the card has to wait for this.
   */
  placed: boolean;
  /** Closes the card and returns the keyboard to the trigger. */
  close: () => void;
}

/**
 * The behaviour every row popup shares: measure, place, and dismiss.
 *
 * A row's popups are transient by construction — they close as soon as something is chosen, which
 * is what lets them host operations that change or remove the row they were opened from. That
 * makes dismissal, not content, the bulk of the code, and it is identical whether the card holds
 * actions or values.
 *
 * The card is fixed to the viewport so that no ancestor's `overflow` can clip it, which means
 * anything that moves the row under it — scrolling the pane, resizing the window — has to re-place
 * it, or it is left hanging over whatever slid beneath.
 *
 * `open` is owned by the caller rather than by the card, so that a tree of rows can enforce one
 * popup at a time — two open at once is reachable by keyboard alone, since only pointer dismissal
 * would close the other.
 */
export function usePopoverCard(open: boolean, setOpen: (open: boolean) => void): PopoverCard {
  const [placement, setPlacement] = useState<PopoverPlacement | null>(null);
  const trigger = useRef<HTMLButtonElement | null>(null);
  const card = useRef<HTMLDivElement | null>(null);

  const close = (): void => {
    setOpen(false);
    trigger.current?.focus();
  };

  // Measured after the card is in the DOM but before it is painted, so its natural size is known
  // before it is positioned and it is never seen in the wrong place. The placement math is the DSL
  // pane's, which already handles flipping above a low anchor and clamping into the viewport.
  useLayoutEffect(() => {
    if (!open) {
      setPlacement(null);
      return;
    }

    const place = (): void => {
      const anchor = trigger.current?.getBoundingClientRect();
      const box = card.current?.getBoundingClientRect();
      if (!anchor || !box) return;
      const next = placePopover(
        { top: anchor.top, bottom: anchor.bottom, left: anchor.left },
        { width: box.width, height: box.height },
        { width: window.innerWidth, height: window.innerHeight, minTop: 0 },
      );
      setPlacement((current) => (samePlacement(current, next) ? current : next));
    };

    place();
    // Capture, so scrolling any ancestor — the pane the rows sit in included — is seen too.
    window.addEventListener('scroll', place, true);
    window.addEventListener('resize', place);
    return () => {
      window.removeEventListener('scroll', place, true);
      window.removeEventListener('resize', place);
    };
  }, [open]);

  // Listeners are installed once per opening, not once per render, so they close over the
  // `setOpen` of the render that opened the card. That is only safe while `setOpen` stays a thin
  // wrapper over a `useState` setter — give it state of its own and it must be made stable first.
  useEffect(() => {
    if (!open) return;

    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') close();
    };
    // `mousedown`, not `click`: a click that starts outside and ends on the card would otherwise
    // dismiss and re-target, and the card should be gone before anything under it is pressed.
    // Focus stays where the pointer put it, so this dismissal does not claw it back.
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

  const style: CSSProperties = placement
    ? { position: 'fixed', top: placement.top, left: placement.left, maxHeight: placement.maxHeight }
    // A card that has yet to be measured is laid out where it falls, but not shown there.
    : { position: 'fixed', top: 0, left: 0, visibility: 'hidden' };

  return { trigger, card, style, placed: placement !== null, close };
}
