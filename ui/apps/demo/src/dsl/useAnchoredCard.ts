import {
  useLayoutEffect, useRef, useState,
  type CSSProperties, type MutableRefObject, type RefObject,
} from 'react';
import type { EditorView } from '@codemirror/view';
import { placePopover, type AnchorBox, type PopoverPlacement } from './popoverPlacement.js';

/** Whether a freshly measured placement is the one already applied to the card. */
function samePlacement(a: PopoverPlacement | null, b: PopoverPlacement): boolean {
  return a !== null && a.top === b.top && a.left === b.left && a.maxHeight === b.maxHeight;
}

/**
 * The token's box on screen, or null when it has none. A position that is not currently drawn
 * has no coordinates, and one past the end of the document throws outright — both of which the
 * caller treats the same way, as "unmeasurable".
 */
function tokenCoordsAt(view: EditorView, position: number): AnchorBox | null {
  const clamped = Math.max(0, Math.min(position, view.state.doc.length));
  try {
    return view.coordsAtPos(clamped);
  } catch {
    return null;
  }
}

/**
 * Where to anchor when the token cannot be measured: the surface's first row, which is where the
 * clamping would have pulled the card anyway.
 */
function surfaceAnchor(surface: HTMLElement): AnchorBox {
  const { top } = surface.getBoundingClientRect();
  return { top, bottom: top, left: 0 };
}

/** What the caller needs to render a card that follows a position in the text. */
interface AnchoredCard {
  /** Hand this to the card's root; it is what gets measured. */
  cardRef: MutableRefObject<HTMLDivElement | null>;
  /** The card's inline position, or a hidden card while it has yet to be measured. */
  style: CSSProperties;
  /** Whether the card has been measured, and so is on screen where it belongs. */
  placed: boolean;
}

/**
 * Keeps a floating card pinned to `anchor`, a position in `view`'s document.
 *
 * The card is anchored to a token, so it can only be placed once both it and the token have been
 * laid out — hence measuring in a layout effect, after the card is in the DOM but before it is
 * painted. It renders hidden until then, so it is never seen in the wrong place.
 *
 * Everything is measured in viewport coordinates because the card is positioned against the
 * viewport, not the editor: it is a hover-card over the page and may overhang the pane. Anything
 * that moves the token under it — scrolling the page, scrolling the editor, resizing the window —
 * has to re-place it, or a fixed card is left pointing at nothing.
 */
export function useAnchoredCard(options: {
  /** The document position to point at, or null when there is no card. */
  anchor: number | null;
  /** The editing surface the anchor lives in, and the fallback to anchor against. */
  surface: RefObject<HTMLElement | null>;
  /** Chrome the card belongs to and so may not cover. */
  clearOf: RefObject<HTMLElement | null>;
  view: RefObject<EditorView | null>;
}): AnchoredCard {
  const { anchor, surface, clearOf, view } = options;
  const cardRef = useRef<HTMLDivElement | null>(null);
  const [placement, setPlacement] = useState<PopoverPlacement | null>(null);

  useLayoutEffect(() => {
    const surfaceEl = surface.current;
    const clearOfEl = clearOf.current;
    const cardEl = cardRef.current;
    if (anchor === null || !surfaceEl || !clearOfEl || !cardEl) {
      setPlacement(null);
      return;
    }

    const place = () => {
      const cardBox = cardEl.getBoundingClientRect();
      const coords = view.current && tokenCoordsAt(view.current, anchor);

      const next = placePopover(
        coords ?? surfaceAnchor(surfaceEl),
        { width: cardBox.width, height: cardBox.height },
        {
          width: window.innerWidth,
          height: window.innerHeight,
          minTop: clearOfEl.getBoundingClientRect().bottom,
        },
      );
      setPlacement((current) => (samePlacement(current, next) ? current : next));
    };

    place();
    // Capture, so scrolling any ancestor — the editing surface included — is seen too.
    window.addEventListener('scroll', place, true);
    window.addEventListener('resize', place);
    // The card's own height is an input to its placement, and it grows in use — a rejected
    // payload adds an error line. Without this it would grow downwards from a position chosen
    // for the shorter card, and could push its own bottom off the screen.
    const observer = new ResizeObserver(place);
    observer.observe(cardEl);
    return () => {
      observer.disconnect();
      window.removeEventListener('scroll', place, true);
      window.removeEventListener('resize', place);
    };
  }, [anchor, clearOf, surface, view]);

  const style: CSSProperties = placement
    ? { top: `${placement.top}px`, left: `${placement.left}px`, maxHeight: `${placement.maxHeight}px` }
    : { visibility: 'hidden' };

  return { cardRef, style, placed: placement !== null };
}
