import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { Truncated } from '../../src/shell/Truncated.js';
import { TOOLTIP_DELAY_MS, _resetTooltipWarmth } from '../../src/shell/Tooltip.js';

/** jsdom lays nothing out, so an element's overflow is whatever the test says it is. */
function overflow(el: HTMLElement, scrollWidth: number, clientWidth: number): void {
  Object.defineProperty(el, 'scrollWidth', { configurable: true, get: () => scrollWidth });
  Object.defineProperty(el, 'clientWidth', { configurable: true, get: () => clientWidth });
}

const peek = (): HTMLElement | null => document.querySelector('.peek');

describe('Truncated', () => {
  beforeEach(() => { vi.useFakeTimers(); _resetTooltipWarmth(); });
  afterEach(() => { vi.useRealTimers(); });

  it('shows the full text in place once the pointer rests on an ellipsised element', () => {
    render(<Truncated text="customer.is-active & customer.has-orders"><span className="doc-name">customer.is-active & …</span></Truncated>);
    const el = screen.getByText('customer.is-active & …');
    overflow(el, 400, 120);
    fireEvent.mouseEnter(el);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(peek()?.textContent).toBe('customer.is-active & customer.has-orders');
  });

  it('shows nothing when the text fits — there is nothing hidden to reveal', () => {
    render(<Truncated text="short"><span>short</span></Truncated>);
    const el = screen.getByText('short');
    overflow(el, 40, 120);
    fireEvent.mouseEnter(el);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(peek()).toBeNull();
  });

  it('measures a descendant too, since the ellipsis may engage on an inner span', () => {
    render(
      <Truncated text="a.very.long.name">
        <button type="button"><span className="inner">a.very.long…</span></button>
      </Truncated>,
    );
    const inner = screen.getByText('a.very.long…');
    overflow(inner, 300, 50);
    fireEvent.mouseEnter(screen.getByRole('button'));
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(peek()?.textContent).toBe('a.very.long.name');
  });

  it('is presentational: the full text is already in the DOM for assistive technology', () => {
    render(<Truncated text="full"><span>fu…</span></Truncated>);
    const el = screen.getByText('fu…');
    overflow(el, 400, 120);
    fireEvent.mouseEnter(el);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(peek()?.getAttribute('aria-hidden')).toBe('true');
    expect(el.hasAttribute('aria-describedby')).toBe(false);
  });

  it('goes away on leave, on blur and on Escape', () => {
    render(<Truncated text="full"><button type="button">fu…</button></Truncated>);
    const el = screen.getByRole('button');
    overflow(el, 400, 120);
    fireEvent.mouseEnter(el);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(peek()).not.toBeNull();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(peek()).toBeNull();
    fireEvent.focus(el);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(peek()).not.toBeNull();
    fireEvent.blur(el);
    expect(peek()).toBeNull();
  });
});
