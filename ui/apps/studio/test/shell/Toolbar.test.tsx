import { tooltipOf } from '../support/tooltip.js';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Toolbar } from '../../src/shell/Toolbar.js';
import { IconSave } from '../../src/shell/icons.js';

describe('Toolbar', () => {
  it('names each icon button, since a glyph alone announces nothing', async () => {
    render(<Toolbar actions={[{ id: 'save', label: 'Save', icon: IconSave, onActivate: () => {} }]} />);
    const button = screen.getByRole('button', { name: 'Save' });
    expect(button).toBeTruthy();
    expect(button.getAttribute('aria-label')).toBe('Save');
    // Both, and for different people: `aria-label` is what assistive technology reads, the tooltip
    // is what a sighted pointer user gets. Never a native `title`, which would double the tooltip.
    expect(button.hasAttribute('title')).toBe(false);
    expect((await tooltipOf(button)).textContent).toBe('Save');
  });

  it('prefers the hint to the label in the tooltip, where a label names a glyph without explaining it', async () => {
    render(<Toolbar actions={[{ id: 'json', label: 'JSON', hint: 'Show the document as JSON', icon: IconSave, onActivate: () => {} }]} />);
    expect((await tooltipOf(screen.getByRole('button', { name: 'JSON' }))).textContent).toBe('Show the document as JSON');
  });

  it('says in the tooltip why an unavailable action is unavailable', async () => {
    render(<Toolbar actions={[{
      id: 'save', label: 'Save', icon: IconSave, onActivate: () => {},
      unavailable: 'Nothing to save.',
    }]} />);
    expect((await tooltipOf(screen.getByRole('button', { name: 'Save' }))).textContent).toBe('Save — Nothing to save.');
  });

  it('activates on click', async () => {
    const onActivate = vi.fn();
    render(<Toolbar actions={[{ id: 'save', label: 'Save', icon: IconSave, onActivate }]} />);
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onActivate).toHaveBeenCalledTimes(1);
  });

  it('keeps an unavailable action reachable and does not activate it', async () => {
    // Deliberately NOT the `disabled` attribute: a disabled button leaves the tab order, so a
    // keyboard screen-reader user never reaches it and never hears the reason.
    const onActivate = vi.fn();
    render(<Toolbar actions={[{
      id: 'save', label: 'Save', icon: IconSave, onActivate,
      unavailable: 'Nothing to save: this name is served by a compiled spec.',
    }]} />);

    const button = screen.getByRole('button', { name: 'Save' });
    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(button.hasAttribute('disabled')).toBe(false);

    await userEvent.click(button);
    expect(onActivate).not.toHaveBeenCalled();
  });

  it('ties the reason to the button so it is announced on arrival', () => {
    render(<Toolbar actions={[{
      id: 'save', label: 'Save', icon: IconSave, onActivate: () => {},
      unavailable: 'Nothing to save.',
    }]} />);

    const button = screen.getByRole('button', { name: 'Save' });
    const describedBy = button.getAttribute('aria-describedby');
    expect(describedBy).not.toBeNull();
    expect(document.getElementById(describedBy!)?.textContent).toBe('Nothing to save.');
  });

  it('does not describe an available action', () => {
    render(<Toolbar actions={[{ id: 'save', label: 'Save', icon: IconSave, onActivate: () => {} }]} />);
    expect(screen.getByRole('button', { name: 'Save' }).getAttribute('aria-describedby')).toBeNull();
  });

  it('renders a primary action as a labelled button, keeping the same accessible name', () => {
    // Save is the one action a page is *for*, so it wears its word beside its glyph. The name is
    // stated once — the visible label — rather than an `aria-label` that could drift from it.
    render(<Toolbar actions={[{ id: 'save', label: 'Save', icon: IconSave, onActivate: () => {}, emphasis: 'primary' }]} />);
    const button = screen.getByRole('button', { name: 'Save' });
    expect(button.textContent).toBe('Save');
    expect(button.className).toContain('btn');
    expect(button.className).not.toContain('ghost');
  });

  it('renders every action labelled when the toolbar is', () => {
    // The palette's footer offers four actions a user meets rarely; a word beside each glyph is
    // recognition where a tooltip would be recall.
    render(<Toolbar labelled actions={[{ id: 'new', label: 'New', icon: IconSave, onActivate: () => {} }]} />);
    const button = screen.getByRole('button', { name: 'New' });
    expect(button.textContent).toBe('New');
  });
});
