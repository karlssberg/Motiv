import { cloneElement, useId, useRef, type CSSProperties, type ReactElement } from 'react';
import { createPortal } from 'react-dom';
import { placeTooltip } from './tooltipPlacement.js';
import { UNPLACED, useAnchoredPlacement } from './useAnchoredPlacement.js';
import { chainedOnto, useHoverIntent } from './useHoverIntent.js';

export { TOOLTIP_DELAY_MS, TOOLTIP_WARM_MS, _resetTooltipWarmth } from './useHoverIntent.js';

/**
 * A tooltip: what a control does, shown once the pointer or the keyboard focus has *rested* on it.
 *
 * It appears after a delay and, once one tooltip has shown, its neighbours open at once — so a
 * toolbar can be read glyph by glyph without waiting per glyph, while a pointer merely crossing
 * the page never triggers anything (see `useHoverIntent`). Escape dismisses it without moving
 * the pointer, and it stays while the pointer is over the tip itself — the three properties WCAG
 * 1.4.13 asks of content that appears on hover.
 *
 * It is the one tooltip mechanism: the native `title` is never used alongside it, because a
 * browser would then show both, and `title` shows for no keyboard user at all.
 *
 * ARIA: the tip is `role="tooltip"` and is the trigger's *description* — linked with
 * `aria-describedby` only while mounted, since an IDREF to an absent element is invalid rather
 * than harmless — unless it would repeat what the trigger already says (its `aria-label`) or the
 * trigger already carries a description, in which case that one stands.
 */
export function Tooltip(props: {
  text: string;
  shortcut?: string | undefined;
  children: ReactElement;
}) {
  const { text, shortcut, children } = props;
  const id = useId();
  const tip = useRef<HTMLDivElement | null>(null);
  const { anchor, bind, hide } = useHoverIntent<HTMLElement>({
    isInside: (target) => target instanceof Node && tip.current?.contains(target) === true,
  });
  const placement = useAnchoredPlacement(anchor, (trigger) => {
    const tipBox = tip.current?.getBoundingClientRect();
    if (!tipBox) return undefined;
    return placeTooltip(trigger.getBoundingClientRect(), tipBox, { width: window.innerWidth, height: window.innerHeight });
  });

  const childProps = children.props as Record<string, unknown>;
  const repeatsName = childProps['aria-label'] === text;
  const alreadyDescribed = typeof childProps['aria-describedby'] === 'string';
  const describes = anchor !== null && !repeatsName && !alreadyDescribed;

  const trigger = cloneElement(children, {
    ...chainedOnto(children, bind),
    ...(describes ? { 'aria-describedby': id } : {}),
  });

  const style: CSSProperties = placement
    ? { position: 'fixed', top: placement.top, left: placement.left }
    : UNPLACED;

  return (
    <>
      {trigger}
      {anchor !== null && createPortal(
        <div
          ref={tip}
          id={id}
          role="tooltip"
          className={`tooltip tooltip-${placement?.side ?? 'above'}`}
          style={style}
          onMouseLeave={(event) => { if (event.relatedTarget !== anchor) hide(); }}
        >
          {text}
          {shortcut !== undefined && <kbd className="tooltip-kbd">{shortcut}</kbd>}
        </div>,
        document.body,
      )}
    </>
  );
}
