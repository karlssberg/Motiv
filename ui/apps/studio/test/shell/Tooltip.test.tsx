import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { Tooltip, TOOLTIP_DELAY_MS, TOOLTIP_WARM_MS, _resetTooltipWarmth } from '../../src/shell/Tooltip.js';

function tip(): HTMLElement | null {
  return screen.queryByRole('tooltip');
}

describe('Tooltip', () => {
  beforeEach(() => { vi.useFakeTimers(); _resetTooltipWarmth(); });
  afterEach(() => { vi.useRealTimers(); });

  it('renders nothing until the pointer has rested on the trigger for the delay', () => {
    render(<Tooltip text="New tab"><button type="button" aria-label="New tab">+</button></Tooltip>);
    const button = screen.getByRole('button');
    fireEvent.mouseEnter(button);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS - 1); });
    expect(tip()).toBeNull();
    act(() => { vi.advanceTimersByTime(1); });
    expect(tip()?.textContent).toBe('New tab');
  });

  it('never sets a native title, which would double the tooltip', () => {
    render(<Tooltip text="New tab"><button type="button" aria-label="New tab">+</button></Tooltip>);
    expect(screen.getByRole('button').hasAttribute('title')).toBe(false);
  });

  it('is cancelled by leaving before the delay elapses', () => {
    render(<Tooltip text="New tab"><button type="button">+</button></Tooltip>);
    const button = screen.getByRole('button');
    fireEvent.mouseEnter(button);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS / 2); });
    fireEvent.mouseLeave(button);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(tip()).toBeNull();
  });

  it('opens a neighbour instantly while a tooltip was just shown, so a toolbar can be scanned', () => {
    render(
      <>
        <Tooltip text="First"><button type="button">1</button></Tooltip>
        <Tooltip text="Second"><button type="button">2</button></Tooltip>
      </>,
    );
    const [first, second] = screen.getAllByRole('button');
    fireEvent.mouseEnter(first!);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(tip()?.textContent).toBe('First');
    fireEvent.mouseLeave(first!);
    act(() => { vi.advanceTimersByTime(TOOLTIP_WARM_MS - 1); });
    fireEvent.mouseEnter(second!);
    act(() => { vi.advanceTimersByTime(0); });
    expect(tip()?.textContent).toBe('Second');
  });

  it('cools down: after the warm window, a neighbour waits the full delay again', () => {
    render(
      <>
        <Tooltip text="First"><button type="button">1</button></Tooltip>
        <Tooltip text="Second"><button type="button">2</button></Tooltip>
      </>,
    );
    const [first, second] = screen.getAllByRole('button');
    fireEvent.mouseEnter(first!);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    fireEvent.mouseLeave(first!);
    act(() => { vi.advanceTimersByTime(TOOLTIP_WARM_MS + 1); });
    fireEvent.mouseEnter(second!);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS - 1); });
    expect(tip()).toBeNull();
  });

  it('shows on keyboard focus after the same delay, so keyboard users get the same help', () => {
    render(<Tooltip text="New tab"><button type="button">+</button></Tooltip>);
    fireEvent.focus(screen.getByRole('button'));
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(tip()?.textContent).toBe('New tab');
    fireEvent.blur(screen.getByRole('button'));
    expect(tip()).toBeNull();
  });

  it('is dismissed by Escape without moving the pointer (WCAG 1.4.13)', () => {
    render(<Tooltip text="New tab"><button type="button">+</button></Tooltip>);
    fireEvent.mouseEnter(screen.getByRole('button'));
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(tip()).not.toBeNull();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(tip()).toBeNull();
  });

  it('stays while the pointer moves onto the tooltip itself (WCAG 1.4.13, hoverable)', () => {
    render(<Tooltip text="New tab"><button type="button">+</button></Tooltip>);
    const button = screen.getByRole('button');
    fireEvent.mouseEnter(button);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    const shown = tip()!;
    fireEvent.mouseLeave(button, { relatedTarget: shown });
    expect(tip()).not.toBeNull();
    fireEvent.mouseLeave(shown, { relatedTarget: document.body });
    expect(tip()).toBeNull();
  });

  it('describes the trigger only while shown, since an IDREF to an absent element is invalid', () => {
    render(<Tooltip text="Step the draft back one change"><button type="button">Undo</button></Tooltip>);
    const button = screen.getByRole('button');
    expect(button.hasAttribute('aria-describedby')).toBe(false);
    fireEvent.mouseEnter(button);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(button.getAttribute('aria-describedby')).toBe(tip()!.id);
    fireEvent.mouseLeave(button);
    expect(button.hasAttribute('aria-describedby')).toBe(false);
  });

  it('does not describe a trigger with the text that is already its name', () => {
    render(<Tooltip text="New tab"><button type="button" aria-label="New tab">+</button></Tooltip>);
    const button = screen.getByRole('button');
    fireEvent.mouseEnter(button);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(tip()).not.toBeNull();
    expect(button.hasAttribute('aria-describedby')).toBe(false);
  });

  it('leaves an existing description alone rather than pointing at two', () => {
    render(
      <>
        <span id="why">Nothing to save.</span>
        <Tooltip text="Save — Nothing to save."><button type="button" aria-describedby="why">Save</button></Tooltip>
      </>,
    );
    const button = screen.getByRole('button');
    fireEvent.mouseEnter(button);
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(button.getAttribute('aria-describedby')).toBe('why');
  });

  it('keeps the child\'s own handlers', () => {
    const onMouseEnter = vi.fn();
    const onFocus = vi.fn();
    render(<Tooltip text="x"><button type="button" onMouseEnter={onMouseEnter} onFocus={onFocus}>+</button></Tooltip>);
    fireEvent.mouseEnter(screen.getByRole('button'));
    fireEvent.focus(screen.getByRole('button'));
    expect(onMouseEnter).toHaveBeenCalledTimes(1);
    expect(onFocus).toHaveBeenCalledTimes(1);
  });

  it('shows the shortcut beside the text', () => {
    render(<Tooltip text="Open" shortcut="⌘K"><button type="button">?</button></Tooltip>);
    fireEvent.mouseEnter(screen.getByRole('button'));
    act(() => { vi.advanceTimersByTime(TOOLTIP_DELAY_MS); });
    expect(tip()!.querySelector('kbd')?.textContent).toBe('⌘K');
  });
});
