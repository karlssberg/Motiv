import { fireEvent, screen, waitFor } from '@testing-library/react';

/**
 * Rests the pointer on `trigger` and returns the tooltip that appears *because of that* — the one
 * not already open when the pointer arrived. Another control's tooltip may still be up from the
 * interaction that got here (the focus that opened a menu, say), so the first `role="tooltip"`
 * in the document is not enough.
 *
 * Real timers: the delay is well inside the wait, and faking timers in a test that also awaits
 * the catalog would stall it.
 */
export async function tooltipOf(trigger: HTMLElement): Promise<HTMLElement> {
  // Whatever was focused by the steps before has a tooltip pending; moving the pointer on means
  // leaving it, so its timer must not be allowed to fire after the snapshot below is taken.
  const focused = document.activeElement;
  if (focused instanceof HTMLElement && focused !== trigger) fireEvent.blur(focused);
  const before = new Set(screen.queryAllByRole('tooltip'));
  fireEvent.mouseEnter(trigger);
  return waitFor(() => {
    const fresh = screen.getAllByRole('tooltip').find((tip) => !before.has(tip));
    if (!fresh) throw new Error('no tooltip has appeared for the trigger yet');
    return fresh;
  }, { timeout: 2000 });
}

/** The in-place reveal of an ellipsised element's full text, once the pointer has rested on it. */
export async function peekOf(trigger: HTMLElement): Promise<HTMLElement> {
  // jsdom lays nothing out, so the element has to be told it overflows.
  Object.defineProperty(trigger, 'scrollWidth', { configurable: true, get: () => 1000 });
  Object.defineProperty(trigger, 'clientWidth', { configurable: true, get: () => 100 });
  fireEvent.mouseEnter(trigger);
  await screen.findByText((_, el) => el?.classList.contains('peek') === true, {}, { timeout: 2000 });
  return document.querySelector('.peek') as HTMLElement;
}
