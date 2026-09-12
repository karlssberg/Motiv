import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DocumentActions } from '../../src/shell/DocumentActions.js';


function renderActions(overrides: Partial<Parameters<typeof DocumentActions>[0]> = {}) {
  const handlers = {
    onOpen: vi.fn(),
    onJson: vi.fn(),
    onSave: vi.fn().mockResolvedValue(true),
    onClose: vi.fn(),
    ...overrides,
  };
  render(
    <DocumentActions
      kind="rule"
      name="can-checkout"
      dirty={false}
      saveUnavailable={undefined}
      {...handlers}
    />,
  );
  return handlers;
}

describe('DocumentActions', () => {
  it('offers Open, JSON, Close and Save', () => {
    renderActions();
    for (const name of ['Open', 'JSON', 'Close', 'Save', 'Save options']) {
      expect(screen.getByRole('button', { name })).toBeTruthy();
    }
  });

  it('makes JSON and Close unavailable while nothing is open, saying so', () => {
    renderActions({ name: null, saveUnavailable: 'Nothing loaded yet.' });
    for (const name of ['JSON', 'Close']) {
      const button = screen.getByRole('button', { name });
      expect(button.getAttribute('aria-disabled')).toBe('true');
      expect(document.getElementById(button.getAttribute('aria-describedby')!)?.textContent)
        .toBe('Nothing open — choose a rule first.');
    }
    expect(screen.getByRole('button', { name: 'Save' }).getAttribute('aria-disabled')).toBe('true');
  });

  it('closes a clean document outright', async () => {
    const handlers = renderActions();
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(handlers.onClose).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('asks before closing a dirty document, and keeps editing by default', async () => {
    const handlers = renderActions({ dirty: true });
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    const dialog = screen.getByRole('dialog', { name: 'Unsaved changes' });
    expect(within(dialog).getByRole('heading').textContent).toContain('can-checkout');
    expect(handlers.onClose).not.toHaveBeenCalled();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Keep editing' }));
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(handlers.onClose).not.toHaveBeenCalled();
  });

  it('discards on request', async () => {
    const handlers = renderActions({ dirty: true });
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    await userEvent.click(screen.getByRole('button', { name: 'Discard changes' }));
    expect(handlers.onClose).toHaveBeenCalledTimes(1);
    expect(handlers.onSave).not.toHaveBeenCalled();
  });

  it('saves then closes from the dialog, only when the save landed', async () => {
    const handlers = renderActions({ dirty: true, onSave: vi.fn().mockResolvedValue(false) });
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Save & close' }));
    await waitFor(() => expect(handlers.onSave).toHaveBeenCalledTimes(1));
    // A conflict or rejection is reported by the page's banners; closing over it would hide them.
    expect(handlers.onClose).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('runs Save from the primary and Save & close from its menu, closing only after success', async () => {
    const handlers = renderActions();
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(handlers.onSave).toHaveBeenCalledTimes(1));
    expect(handlers.onClose).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Save options' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Save & close/ }));
    await waitFor(() => expect(handlers.onClose).toHaveBeenCalledTimes(1));
    expect(handlers.onSave).toHaveBeenCalledTimes(2);
  });

  it('remembers the chosen variant as the primary, across mounts', async () => {
    renderActions();
    await userEvent.click(screen.getByRole('button', { name: 'Save options' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Save & close/ }));
    expect(screen.getByRole('button', { name: 'Save & close' })).toBeTruthy();

    // A fresh mount — the next page, the next visit — starts on what was last chosen.
    renderActions();
    expect(screen.getAllByRole('button', { name: 'Save & close' })).toHaveLength(2);
  });

  it('rides the save detail on both variants', () => {
    renderActions({ saveDetail: '(2)' });
    expect(screen.getByRole('button', { name: 'Save (2)' })).toBeTruthy();
  });
});
