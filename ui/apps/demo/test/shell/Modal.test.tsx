import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Modal } from '../../src/shell/Modal.js';

describe('Modal', () => {
  it('opens itself modally on mount', () => {
    render(<Modal label="Propositions" onClose={() => {}}>body</Modal>);
    expect(screen.getByRole('dialog')).toHaveProperty('open', true);
  });

  it('names itself for assistive technology', () => {
    render(<Modal label="Propositions" onClose={() => {}}>body</Modal>);
    expect(screen.getByRole('dialog', { name: 'Propositions' })).toBeTruthy();
  });

  it('reports the close control', async () => {
    const onClose = vi.fn();
    render(<Modal label="Propositions" onClose={onClose}>body</Modal>);
    await userEvent.click(screen.getByRole('button', { name: /close/i }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('reports a native cancel, which is what Escape raises', () => {
    // Escape on a <dialog> fires `cancel`, not a keydown the component sees. Dispatching the
    // event directly is the only honest way to reach that path under jsdom, which has no
    // top layer and so never raises it on its own.
    const onClose = vi.fn();
    render(<Modal label="Propositions" onClose={onClose}>body</Modal>);
    screen.getByRole('dialog').dispatchEvent(new Event('cancel', { cancelable: true }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('reports a click on the backdrop but not one inside the content', async () => {
    // A <dialog>'s backdrop is part of the dialog element, so a backdrop click targets the
    // dialog itself. A click on anything inside must not close it.
    const onClose = vi.fn();
    render(<Modal label="Propositions" onClose={onClose}><button>inside</button></Modal>);

    await userEvent.click(screen.getByRole('button', { name: 'inside' }));
    expect(onClose).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('dialog'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
