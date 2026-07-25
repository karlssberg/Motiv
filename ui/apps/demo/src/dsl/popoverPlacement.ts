/**
 * Where the payload popover goes: anchored to the spec token it belongs to, the way an editor
 * hover-card is, and clamped into the editor frame — which clips, and whose first rows are the
 * toolbar the card must never cover.
 *
 * The arithmetic lives here, apart from the component, because it is the whole of the logic and
 * jsdom has no layout to exercise it through the DOM.
 */

/** Space kept between the anchor token and the card. */
const GAP = 6;
/** Space kept between the card and the edges of the frame. */
const MARGIN = 8;

/** The anchored token's box, in frame coordinates. */
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
 * The box the card must stay inside. `minTop` is the first row it may occupy — the top of the
 * editing surface, so the toolbar above it stays visible.
 */
export interface FrameBox {
  width: number;
  height: number;
  minTop: number;
}

/** Where to put the card, in frame coordinates. */
export interface PopoverPlacement {
  top: number;
  left: number;
  /** The tallest the card may be and still fit between the toolbar and the frame's bottom. */
  maxHeight: number;
}

/** `value`, held within `[min, max]` — and to `min` when the range has collapsed. */
function clamp(value: number, min: number, max: number): number {
  if (max <= min) return min;
  return Math.min(Math.max(value, min), max);
}

/**
 * Places the card below `anchor`, flipping above it when there is no room below, then clamps the
 * result into `frame`. Degenerate measurements (a headless DOM reports zeroes) simply clamp to
 * the frame's origin rather than producing a position off in space.
 */
export function placePopover(
  anchor: AnchorBox,
  popover: PopoverSize,
  frame: FrameBox,
): PopoverPlacement {
  const maxHeight = Math.max(0, frame.height - frame.minTop - MARGIN * 2);
  const height = Math.min(popover.height, maxHeight);

  const left = clamp(anchor.left, MARGIN, frame.width - popover.width - MARGIN);

  const lowestTop = frame.height - height - MARGIN;
  const below = anchor.bottom + GAP;
  const above = anchor.top - GAP - height;
  const fitsBelow = below <= lowestTop;
  const fitsAbove = above >= frame.minTop;
  const preferred = !fitsBelow && fitsAbove ? above : below;

  return { top: clamp(preferred, frame.minTop, lowestTop), left, maxHeight };
}
