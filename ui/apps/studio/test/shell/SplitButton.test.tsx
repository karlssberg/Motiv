import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SplitButton, type SplitVariant } from '../../src/shell/SplitButton.js';
import { IconSave } from '../../src/shell/icons.js';

const variants: SplitVariant[] = [
  { id: 'save', label: 'Save', description: 'Keep editing after saving.' },
  { id: 'save-close', label: 'Save & close', description: 'Save, then return to the listing.' },
];

function renderSplit(overrides: Partial<Parameters<typeof SplitButton>[0]> = {}) {
  const onChoose = vi.fn();
  render(
    <SplitButton
      id="save"
      icon={IconSave}
      variants={variants}
      defaultId="save"
      onChoose={onChoose}
      {...overrides}
    />,
  );
  return onChoose;
}

describe('SplitButton', () => {
  it('wears the default variant as its visible name and runs it on click', async () => {
    const onChoose = renderSplit();
    const main = screen.getByRole('button', { name: 'Save' });
    expect(main.textContent).toBe('Save');
    await userEvent.click(main);
    expect(onChoose).toHaveBeenCalledWith(variants[0]);
  });

  it('names the toggle after the action it holds the options for, and no menu until opened', () => {
    renderSplit();
    const toggle = screen.getByRole('button', { name: 'Save options' });
    expect(toggle.getAttribute('aria-haspopup')).toBe('menu');
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    // An IDREF to an element that is not mounted is invalid, not harmless.
    expect(toggle.getAttribute('aria-controls')).toBeNull();
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('opens a radio menu marking the default, controlled by the toggle', async () => {
    renderSplit();
    const toggle = screen.getByRole('button', { name: 'Save options' });
    await userEvent.click(toggle);

    const menu = screen.getByRole('menu', { name: 'Save options' });
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(toggle.getAttribute('aria-controls')).toBe(menu.id);
    const items = screen.getAllByRole('menuitemradio');
    expect(items.map((item) => item.getAttribute('aria-checked'))).toEqual(['true', 'false']);
    // The description is part of what a screen reader gets, so it is in the item, not a tooltip.
    expect(items[1]!.textContent).toContain('Save, then return to the listing.');
  });

  it('runs the chosen variant and closes the menu', async () => {
    const onChoose = renderSplit();
    await userEvent.click(screen.getByRole('button', { name: 'Save options' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Save & close/ }));
    expect(onChoose).toHaveBeenCalledWith(variants[1]);
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('moves through the items on the arrow keys and closes on Escape, focus back on the toggle', async () => {
    renderSplit();
    const toggle = screen.getByRole('button', { name: 'Save options' });
    toggle.focus();
    await userEvent.keyboard('{Enter}');
    const items = screen.getAllByRole('menuitemradio');
    // Opening lands on the variant in force, so the list starts where the value is.
    expect(document.activeElement).toBe(items[0]);
    await userEvent.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(items[1]);
    await userEvent.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(items[0]);
    await userEvent.keyboard('{ArrowUp}');
    expect(document.activeElement).toBe(items[1]);
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('menu')).toBeNull();
    expect(document.activeElement).toBe(toggle);
  });

  it('keeps an unavailable action reachable, explained, and inert — toggle included', async () => {
    const onChoose = renderSplit({ unavailable: 'Nothing loaded yet.' });
    const main = screen.getByRole('button', { name: 'Save' });
    const toggle = screen.getByRole('button', { name: 'Save options' });
    expect(main.getAttribute('aria-disabled')).toBe('true');
    expect(main.hasAttribute('disabled')).toBe(false);
    expect(document.getElementById(main.getAttribute('aria-describedby')!)?.textContent).toBe('Nothing loaded yet.');
    expect(toggle.getAttribute('aria-disabled')).toBe('true');
    await userEvent.click(main);
    await userEvent.click(toggle);
    expect(onChoose).not.toHaveBeenCalled();
    expect(screen.queryByRole('menu')).toBeNull();
  });
});
