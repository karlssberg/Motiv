import { useLayoutEffect, useState, type CSSProperties } from 'react';

/**
 * Fixed from the first frame, for a hint that has not been placed yet. A tip that is merely
 * `visibility: hidden` is still laid out as a block in `<body>`, measures at its max-width, and is
 * then centred on that — which put the first tooltip after every reload a hundred pixels to the
 * left.
 */
export const UNPLACED: CSSProperties = { position: 'fixed', top: 0, left: 0, visibility: 'hidden' };

/**
 * Where a hint anchored to `anchor` goes, as `measure` works it out: after the hint is in the DOM
 * but before it is painted, so it is never seen in the wrong place, and again on scroll and
 * resize, which move the anchor under it. `null` once there is no anchor, so the next hint does
 * not measure itself at the last one's position. `measure` may return `undefined` when the hint
 * is not yet in the DOM to be measured, in which case the placement is left as it was.
 */
export function useAnchoredPlacement<E extends HTMLElement, P>(
  anchor: E | null,
  measure: (anchor: E) => P | undefined,
): P | null {
  const [placement, setPlacement] = useState<P | null>(null);

  useLayoutEffect(() => {
    if (anchor === null) { setPlacement(null); return; }
    const place = (): void => {
      const next = measure(anchor);
      if (next !== undefined) setPlacement(next);
    };
    place();
    window.addEventListener('scroll', place, true);
    window.addEventListener('resize', place);
    return () => {
      window.removeEventListener('scroll', place, true);
      window.removeEventListener('resize', place);
    };
  }, [anchor]);

  return placement;
}
