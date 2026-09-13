import { describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DocActions } from '../../src/shell/DocActions.js';

function renderActions(overrides: Partial<Parameters<typeof DocActions>[0]> = {}) {
  const handlers = {
    onJson: vi.fn(),
    onSave: vi.fn().mockResolvedValue(true),
    onClose: vi.fn(),
    ...overrides,
  };
  render(<DocActions saveUnavailable={undefined} {...handlers} />);
  return handlers;
}

describe('DocActions', () => {
  it('offers JSON and Save, and neither Open nor Close', () => {
    renderActions();
    for (const name of ['JSON', 'Save', 'Save options']) {
      expect(screen.getByRole('button', { name })).toBeTruthy();
    }
    expect(screen.queryByRole('button', { name: 'Open' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Close' })).toBeNull();
  });

  it('makes Save unavailable for the reason given', () => {
    renderActions({ saveUnavailable: 'Nothing loaded yet.' });
    const save = screen.getByRole('button', { name: 'Save' });
    expect(save.getAttribute('aria-disabled')).toBe('true');
    expect(document.getElementById(save.getAttribute('aria-describedby')!)?.textContent).toBe('Nothing loaded yet.');
  });

  it('ignores a remembered default it does not recognise', () => {
    window.localStorage.setItem('motiv.studio.save-default', 'save-and-deploy');
    renderActions();
    expect(screen.getByRole('button', { name: 'Save' })).toBeTruthy();
    window.localStorage.removeItem('motiv.studio.save-default');
  });

  it('runs Save from the primary and Save & close from its menu, closing only after success', async () => {
    const handlers = renderActions({ onSave: vi.fn().mockResolvedValueOnce(false).mockResolvedValue(true) });
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(handlers.onSave).toHaveBeenCalledTimes(1);
    expect(handlers.onClose).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Save options' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Save & close/ }));
    await waitFor(() => expect(handlers.onSave).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(handlers.onClose).toHaveBeenCalledTimes(1));
  });

  it('does not close when the save did not land', async () => {
    const handlers = renderActions({ onSave: vi.fn().mockResolvedValue(false) });
    await userEvent.click(screen.getByRole('button', { name: 'Save options' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Save & close/ }));
    await waitFor(() => expect(handlers.onSave).toHaveBeenCalledTimes(1));
    expect(handlers.onClose).not.toHaveBeenCalled();
  });

  it('remembers the chosen variant as the primary, across mounts', async () => {
    const first = renderActions();
    await userEvent.click(screen.getByRole('button', { name: 'Save options' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Save & close/ }));
    await waitFor(() => expect(first.onClose).toHaveBeenCalled());

    cleanup();
    const second = renderActions();
    await userEvent.click(screen.getByRole('button', { name: 'Save & close' }));
    await waitFor(() => expect(second.onClose).toHaveBeenCalled());
    window.localStorage.removeItem('motiv.studio.save-default');
  });

  it('rides the save detail on both variants', () => {
    renderActions({ saveDetail: '(2)' });
    expect(screen.getByRole('button', { name: 'Save (2)' })).toBeTruthy();
  });
});
