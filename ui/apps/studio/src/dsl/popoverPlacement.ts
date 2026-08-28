/**
 * Where the payload popover goes: anchored to the spec token it belongs to, the way an editor
 * hover-card is — CodeMirror's own tooltips included.
 *
 * It is a card floating over the page, not a panel inside the editor, so it is placed and clamped
 * against the viewport. Overhanging the pane it was opened from is expected: the pane is only
 * ~400px wide and the card 320px, and confining it there would leave the clamp absorbing all of
 * the anchoring and the card blanketing the text it is meant to annotate.
 *
 * The arithmetic lives here, apart from the component, because it is the whole of the logic and
 * jsdom has no layout to exercise it through the DOM.
 */

/** Space kept between the anchor token and the card. */
const GAP = 6;
/** Space kept between the card and the edges of the viewport. */
const MARGIN = 8;

/** The anchored token's box, in viewport coordinates. */
export interface AnchorBox {
  top: number;
  bottom: number;
  left: number;
}

/** The card's natural size. */
export interface PopoverSize {
  width: number;
  height: number;
}

/**
 * The box the card must stay inside. `minTop` is the lowest row it may start at — the editor
 * toolbar's bottom edge, so a card that flips above its token still leaves the toolbar visible.
 * It is only a floor on top of the viewport's own, so a toolbar scrolled off the top of the
 * screen (or above it) does not drag the card off with it.
 */
export interface ViewportBox {
  width: number;
  height: number;
  minTop: number;
}

/** Where to put the card, in viewport coordinates. */
export interface PopoverPlacement {
  top: number;
  left: number;
  /** The tallest the card may be and still fit between the toolbar and the viewport's bottom. */
  maxHeight: number;
}

/** `value`, held within `[min, max]` — and to `min` when the range has collapsed. */
function clamp(value: number, min: number, max: number): number {
  if (max <= min) return min;
  return Math.min(Math.max(value, min), max);
}

/**
 * Places the card below `anchor`, flipping above it when there is no room below, then clamps the
 * result into `viewport`. Degenerate measurements (a headless DOM reports zeroes) simply clamp to
 * the viewport's origin rather than producing a position off in space.
 */
export function placePopover(
  anchor: AnchorBox,
  popover: PopoverSize,
  viewport: ViewportBox,
): PopoverPlacement {
  const highestTop = Math.max(MARGIN, viewport.minTop);
  const maxHeight = Math.max(0, viewport.height - highestTop - MARGIN);
  const height = Math.min(popover.height, maxHeight);

  const left = clamp(anchor.left, MARGIN, viewport.width - popover.width - MARGIN);

  const lowestTop = viewport.height - height - MARGIN;
  const below = anchor.bottom + GAP;
  const above = anchor.top - GAP - height;
  const fitsBelow = below <= lowestTop;
  const fitsAbove = above >= highestTop;
  const preferred = !fitsBelow && fitsAbove ? above : below;

  return { top: clamp(preferred, highestTop, lowestTop), left, maxHeight };
}
