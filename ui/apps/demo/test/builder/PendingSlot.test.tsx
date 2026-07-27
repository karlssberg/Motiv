import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PendingSlot } from '../../src/builder/PendingSlot.js';
import { replaceBuffer } from '../support/codemirror.js';

const catalog = { specs: [], collections: [] };
const content = (container: HTMLElement) => container.querySelector('.cm-content')!;

/** Renders the slot with spy callbacks, returning both so a test can assert on either. */
function renderSlot() {
  const onCommit = vi.fn();
  const onCancel = vi.fn();
  const view = render(
    <PendingSlot modelType="customer" catalog={catalog} onCommit={onCommit} onCancel={onCancel} />,
  );
  return { onCommit, onCancel, ...view };
}

describe('PendingSlot', () => {
  it('mounts a CodeMirror editor on the phantom row', () => {
    const { container } = renderSlot();
    expect(container.querySelector('.node-row-pending')).not.toBeNull();
    expect(content(container)).not.toBeNull();
  });

  it('commits a parsed node', () => {
    const { onCommit, container } = renderSlot();

    replaceBuffer(container, 'is-active');
    fireEvent.keyDown(content(container), { key: 'Enter' });

    expect(onCommit).toHaveBeenCalledWith({ spec: 'is-active' });
  });

  it('refuses an unparseable buffer and reports it without committing', () => {
    const { onCommit, container } = renderSlot();

    replaceBuffer(container, 'a &');
    fireEvent.keyDown(content(container), { key: 'Enter' });

    expect(onCommit).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toBeDefined();
  });

  it('cancels on Escape without committing', () => {
    const { onCommit, onCancel, container } = renderSlot();

    replaceBuffer(container, 'is-active');
    fireEvent.keyDown(content(container), { key: 'Escape' });

    expect(onCancel).toHaveBeenCalled();
    expect(onCommit).not.toHaveBeenCalled();
  });

  it('cancels rather than committing when the buffer is empty', () => {
    const { onCommit, onCancel, container } = renderSlot();

    fireEvent.keyDown(content(container), { key: 'Enter' });

    expect(onCommit).not.toHaveBeenCalled();
    expect(onCancel).toHaveBeenCalled();
  });

  it('retires the error message on the next keystroke', () => {
    const { container } = renderSlot();

    replaceBuffer(container, 'a &');
    fireEvent.keyDown(content(container), { key: 'Enter' });
    expect(screen.getByRole('alert')).toBeDefined();

    replaceBuffer(container, 'a & b');

    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('names the editable region for assistive technology', () => {
    renderSlot();
    // The name must land on the element carrying role="textbox" — CodeMirror's `.cm-content` —
    // not on the host span, which is why the hook applies it via contentAttributes.
    expect(screen.getByRole('textbox', { name: 'new expression' })).toBeDefined();
  });
});
