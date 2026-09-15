import { cloneElement, type CSSProperties, type ReactElement } from 'react';
import { createPortal } from 'react-dom';
import { UNPLACED, useAnchoredPlacement } from './useAnchoredPlacement.js';
import { chainedOnto, useHoverIntent } from './useHoverIntent.js';

/** Whether `el`, or anything inside it, has been cut short by `text-overflow: ellipsis`. */
export function isTruncated(el: HTMLElement): boolean {
  if (el.scrollWidth > el.clientWidth) return true;
  for (const inner of Array.from(el.querySelectorAll<HTMLElement>('*'))) {
    if (inner.scrollWidth > inner.clientWidth) return true;
  }
  return false;
}

/** The peek's box: over the element, in its font and colours, running right until the viewport's edge. */
function overElement(el: HTMLElement): CSSProperties {
  const box = el.getBoundingClientRect();
  const computed = getComputedStyle(el);
  return {
    position: 'fixed',
    top: box.top,
    left: box.left,
    height: box.height,
    maxWidth: Math.max(0, window.innerWidth - box.left - 8),
    font: computed.font,
    lineHeight: computed.lineHeight,
    padding: computed.padding,
    color: computed.color,
    borderRadius: computed.borderRadius,
  };
}

/**
 * The full text of something that may have been ellipsised, revealed *in place* once the pointer
 * or focus rests on it — and only when the ellipsis has actually engaged, so an element with room
 * to spare stays quiet.
 *
 * Not a tooltip. A tooltip is a second thing that appears near the first; this extends the
 * element's own box to the right, in its own font and colours, so what the reader sees is the
 * text they were already reading, finished. It is decoration for the eye only: the full text is
 * already in the DOM, so assistive technology has it without any of this, and the peek is
 * `aria-hidden` rather than a second copy.
 *
 * The peek cannot wrap — a second line would no longer read as the same element — so it runs to
 * the viewport's edge and stops there. That is the trade the in-place reveal makes against a
 * floating box, and it is the right one for names and expressions, which are read left to right.
 */
export function Truncated(props: { text: string; children: ReactElement }) {
  const { text, children } = props;
  const { anchor, bind } = useHoverIntent<HTMLElement>({ shouldShow: isTruncated });
  const style = useAnchoredPlacement(anchor, overElement) ?? UNPLACED;

  return (
    <>
      {cloneElement(children, chainedOnto(children, bind))}
      {anchor !== null && createPortal(
        <div className="peek" style={style} aria-hidden="true">{text}</div>,
        document.body,
      )}
    </>
  );
}
