/**
 * Where a tooltip goes: centred above its trigger, below it when there is no room above, and
 * clamped into the viewport either way. The arithmetic is apart from the component for the same
 * reason `popoverPlacement.ts` is — it is the whole of the logic, and jsdom has no layout to
 * exercise it through the DOM.
 */

/** Space kept between the trigger and the tip. */
const GAP = 6;
/** Space kept between the tip and the edges of the viewport. */
const MARGIN = 8;

/** The trigger's box, in viewport coordinates. */
export interface TriggerBox {
  top: number;
  bottom: number;
  left: number;
  right: number;
}

export interface TipSize {
  width: number;
  height: number;
}

export interface Viewport {
  width: number;
  height: number;
}

export interface TooltipPlacement {
  top: number;
  left: number;
  /** Which side of the trigger the tip landed on — the arrow, if any, points the other way. */
  side: 'above' | 'below';
}

/** `value`, held within `[min, max]` — and to `min` when the range has collapsed. */
function clamp(value: number, min: number, max: number): number {
  if (max <= min) return min;
  return Math.min(Math.max(value, min), max);
}

export function placeTooltip(trigger: TriggerBox, tip: TipSize, viewport: Viewport): TooltipPlacement {
  const above = trigger.top - GAP - tip.height;
  const side: TooltipPlacement['side'] = above >= MARGIN ? 'above' : 'below';
  const top = side === 'above' ? above : trigger.bottom + GAP;
  const centre = (trigger.left + trigger.right) / 2;
  const left = clamp(centre - tip.width / 2, MARGIN, viewport.width - tip.width - MARGIN);
  return { top, left, side };
}
