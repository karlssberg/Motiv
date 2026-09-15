import {
  useEffect, useRef, useState, type FocusEvent, type MouseEvent, type ReactElement, type SyntheticEvent,
} from 'react';

/** How long the pointer (or focus) rests on a trigger before its hint appears. */
export const TOOLTIP_DELAY_MS = 600;
/**
 * How long after one hint closes the next opens with no delay at all. This is what lets a toolbar
 * be *read*: the first glyph costs the full delay, its neighbours cost nothing, and a pointer
 * merely crossing the bar — which never rests — still triggers nothing.
 */
export const TOOLTIP_WARM_MS = 300;

/**
 * When the last hint closed. Module-level on purpose: warmth is a property of the page, not of
 * one trigger, so that moving from a toolbar button to its neighbour is instant.
 */
let lastClosedAt = -Infinity;

/** Test seam: module state would otherwise carry warmth from one test into the next. */
export function _resetTooltipWarmth(): void {
  lastClosedAt = -Infinity;
}

/** The handlers a trigger spreads, and the element the hint is currently anchored to. */
export interface HoverIntent<E extends HTMLElement> {
  anchor: E | null;
  bind: {
    onMouseEnter: (event: MouseEvent<E>) => void;
    onMouseLeave: (event: MouseEvent<E>) => void;
    onFocus: (event: FocusEvent<E>) => void;
    onBlur: (event: FocusEvent<E>) => void;
  };
  /** Closes the hint now — Escape, or the pointer leaving the hint itself. */
  hide: () => void;
}

/**
 * Hover and focus with intent: a hint appears once the trigger has been rested on, and goes away
 * as soon as it has not. Escape dismisses it without the pointer moving (WCAG 1.4.13), and it
 * stays while the pointer is over the hint itself — `isInside` says whether a `mouseleave`'s
 * destination is the hint.
 *
 * `shouldShow` is asked at the moment of entry, so a hint that only applies sometimes — the full
 * text of an element that is only *sometimes* ellipsised — can decline without unmounting.
 */
export function useHoverIntent<E extends HTMLElement>(options: {
  shouldShow?: (anchor: E) => boolean;
  isInside?: (target: EventTarget | null) => boolean;
} = {}): HoverIntent<E> {
  const [anchor, setAnchor] = useState<E | null>(null);
  const timer = useRef<number | null>(null);

  const cancel = (): void => {
    if (timer.current !== null) {
      window.clearTimeout(timer.current);
      timer.current = null;
    }
  };

  const show = (el: E): void => {
    if (options.shouldShow?.(el) === false) return;
    cancel();
    const warm = Date.now() - lastClosedAt < TOOLTIP_WARM_MS;
    timer.current = window.setTimeout(() => {
      timer.current = null;
      setAnchor(el);
    }, warm ? 0 : TOOLTIP_DELAY_MS);
  };

  const hide = (): void => {
    cancel();
    if (anchor !== null) lastClosedAt = Date.now();
    setAnchor(null);
  };

  // Escape while shown, wherever the keyboard focus is: a hovered-but-unfocused trigger never
  // receives the key, so it has to be heard at the document.
  useEffect(() => {
    if (anchor === null) return;
    const onKeyDown = (event: KeyboardEvent): void => { if (event.key === 'Escape') hide(); };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [anchor]);

  useEffect(() => cancel, []);

  return {
    anchor,
    hide,
    bind: {
      onMouseEnter: (event) => show(event.currentTarget),
      onMouseLeave: (event) => { if (!options.isInside?.(event.relatedTarget)) hide(); },
      onFocus: (event) => show(event.currentTarget),
      onBlur: () => hide(),
    },
  };
}

/** A handler chain: the child's own first, then ours. */
function chain<Ev extends SyntheticEvent>(
  own: ((event: Ev) => void) | undefined,
  ours: (event: Ev) => void,
): (event: Ev) => void {
  return (event) => { own?.(event); ours(event); };
}

/**
 * `bind`, with each handler running after any the child already has of the same name — so
 * cloning them onto the child (`cloneElement(child, chainedOnto(child, bind))`) loses nothing the
 * child wired itself.
 */
export function chainedOnto<E extends HTMLElement>(
  child: ReactElement,
  bind: HoverIntent<E>['bind'],
): HoverIntent<E>['bind'] {
  const own = child.props as Partial<HoverIntent<E>['bind']>;
  return {
    onMouseEnter: chain(own.onMouseEnter, bind.onMouseEnter),
    onMouseLeave: chain(own.onMouseLeave, bind.onMouseLeave),
    onFocus: chain(own.onFocus, bind.onFocus),
    onBlur: chain(own.onBlur, bind.onBlur),
  };
}
