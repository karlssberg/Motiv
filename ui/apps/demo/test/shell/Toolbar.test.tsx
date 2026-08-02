import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Toolbar } from '../../src/shell/Toolbar.js';
import { IconSave } from '../../src/shell/icons.js';

describe('Toolbar', () => {
  it('names each icon button, since a glyph alone announces nothing', () => {
    render(<Toolbar actions={[{ id: 'save', label: 'Save', icon: IconSave, onActivate: () => {} }]} />);
    const button = screen.getByRole('button', { name: 'Save' });
    expect(button).toBeTruthy();
    expect(button.getAttribute('aria-label')).toBe('Save');
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
});
