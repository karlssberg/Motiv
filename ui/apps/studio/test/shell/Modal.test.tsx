import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
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

  it('reports the close control, and titles it like every other icon button', async () => {
    const onClose = vi.fn();
    render(<Modal label="Propositions" onClose={onClose}>body</Modal>);
    const close = screen.getByRole('button', { name: /close/i });

    // A bare glyph teaches nothing on first sight, so an icon-only button carries both — the
    // `aria-label` for assistive technology and the `title` for the pointer. `Toolbar` states the
    // same rule for the three shell actions; this was the one button departing from it.
    expect(close.getAttribute('title')).toBe('Close');

    await userEvent.click(close);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('reports a native cancel, which is what Escape raises, and prevents its default', () => {
    // Escape on a <dialog> fires `cancel`, not a keydown the component sees. Dispatching the
    // event directly is the only honest way to reach that path under jsdom, which has no
    // top layer and so never raises it on its own.
    const onClose = vi.fn();
    render(<Modal label="Propositions" onClose={onClose}>body</Modal>);

    const notPrevented = screen.getByRole('dialog').dispatchEvent(new Event('cancel', { cancelable: true }));

    expect(onClose).toHaveBeenCalledTimes(1);
    // Without `preventDefault` the browser closes the dialog itself, behind React's back, while
    // the caller's state still says it is open — and the modal cannot be reopened. jsdom raises no
    // native cancel, but `dispatchEvent` returns false exactly when the default was prevented,
    // which is the same signal a browser reads.
    expect(notPrevented).toBe(false);
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

  it('keeps the dialog when a drag that began inside releases on the backdrop', () => {
    // A `click` fires on the nearest common ancestor of where the press and the release landed —
    // so a drag that starts on content and ends past the dialog's edge targets the dialog itself,
    // exactly as a backdrop click does. Comparing only the click cannot tell them apart, and the
    // one it gets wrong is the one that destroys work: drag-selecting the text in a field and
    // releasing outside dismissed the authoring dialog and discarded every value typed into it.
    //
    // fireEvent rather than userEvent, because userEvent drives a *complete* press-and-release on
    // one target and this is precisely the case where the two targets differ.
    const onClose = vi.fn();
    render(<Modal label="Propositions" onClose={onClose}><button>inside</button></Modal>);

    fireEvent.mouseDown(screen.getByRole('button', { name: 'inside' }));
    fireEvent.click(screen.getByRole('dialog'));

    expect(onClose).not.toHaveBeenCalled();
  });
});
