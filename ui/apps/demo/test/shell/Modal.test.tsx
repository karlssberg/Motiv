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

  it('puts the close control last, so it is not what showModal focuses', () => {
    // Structure, not behaviour — and deliberately so. `showModal()` runs the dialog focusing steps,
    // which hand focus to the first focusable descendant unless one carries the `autofocus`
    // *attribute*; React's `autoFocus` prop is not that attribute. With Close rendered first, every
    // modal in the app opened with focus on Close instead of the palette's search box or the
    // authoring dialog's Name field.
    //
    // jsdom's `showModal` shim sets `open` and moves no focus, so the *consequence* is only
    // provable in a browser — `propositions.spec.ts › the palette traps focus…` does that. But the
    // *cause* is one line of JSX ordering, and pinning it here is what stops a reorder reverting a
    // production bug with nothing but the e2e suite to notice.
    render(<Modal label="Propositions" onClose={() => {}}><button>inside</button></Modal>);

    const dialog = screen.getByRole('dialog');
    expect(dialog.lastElementChild).toBe(screen.getByRole('button', { name: /close/i }));
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
