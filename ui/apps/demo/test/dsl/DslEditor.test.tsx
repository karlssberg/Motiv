import { describe, it, expect } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore } from '@motiv/rules-core';
import type { Catalog } from '@motiv/rules-core';
import { DslEditor } from '../../src/dsl/DslEditor.js';
import { useDslSync } from '../../src/dsl/useDslSync.js';
import { editorText, editorView, replaceBuffer } from '../support/codemirror.js';

const CATALOG: Catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Currently active.' },
    { name: 'is-verified', modelType: 'customer', metadataType: 'String', isAsync: false },
  ],
  collections: [],
};

/** Stands in for the pane that owns the buffer, which the editor takes as a prop. */
function Host(props: { store: RuleEditorStore }) {
  const sync = useDslSync(props.store);
  return <DslEditor store={props.store} catalog={CATALOG} sync={sync} />;
}

function renderEditor() {
  const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
  const { container } = render(<Host store={store} />);
  return { store, container };
}

describe('DslEditor', () => {
  it('renders the store document as DSL text', () => {
    const { container } = renderEditor();
    expect(editorText(container)).toBe('is-active');
  });

  it('reports a synced buffer by default', () => {
    renderEditor();
    expect(screen.getByLabelText('sync status').textContent).toBe('synced');
  });

  it('marks the buffer unsynced while an edit is uncommitted', () => {
    const { container } = renderEditor();
    replaceBuffer(container, 'is-verified');
    expect(screen.getByLabelText('sync status').textContent).toBe('unsynced');
  });

  it('exposes a Format button', () => {
    renderEditor();
    expect(screen.getByRole('button', { name: 'Format' })).toBeTruthy();
  });

  it('formats the buffer canonically on demand', async () => {
    const user = userEvent.setup();
    const { container } = renderEditor();

    replaceBuffer(container, 'is-active   &&   is-verified');
    await user.click(screen.getByRole('button', { name: 'Format' }));

    expect(editorText(container)).toBe('is-active && is-verified');
  });

  it('reprints silently when the store changes and the buffer is clean', () => {
    const { container, store } = renderEditor();

    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));

    expect(editorText(container)).toBe('is-verified');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('raises a conflict banner when the store changes while the buffer is dirty', () => {
    const { container, store } = renderEditor();

    replaceBuffer(container, 'is-active && is-verified');
    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));

    expect(screen.getByRole('alert').textContent).toContain('Builder');
    expect(screen.getByRole('button', { name: 'Reformat from tree' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Keep editing' })).toBeTruthy();
    expect(editorText(container)).toBe('is-active && is-verified');
  });

  it('restores the store text when reformatting from the tree', async () => {
    const user = userEvent.setup();
    const { container, store } = renderEditor();

    replaceBuffer(container, 'is-active && is-verified');
    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));
    await user.click(screen.getByRole('button', { name: 'Reformat from tree' }));

    expect(editorText(container)).toBe('is-verified');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('keeps the local text when the conflict is dismissed', async () => {
    const user = userEvent.setup();
    const { container, store } = renderEditor();

    replaceBuffer(container, 'is-active && is-verified');
    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));
    await user.click(screen.getByRole('button', { name: 'Keep editing' }));

    expect(screen.queryByRole('alert')).toBeNull();
    expect(editorText(container)).toBe('is-active && is-verified');
  });

  it('underlines a backend error, which arrives without a document change', () => {
    const { container, store } = renderEditor();

    act(() => store.setErrors([{ path: '$.rule', code: 'UnknownSpec', message: 'no such spec' }]));

    expect(container.querySelector('.cm-lintRange-error')).toBeTruthy();
  });

  it('clears the underline once the backend errors go away', () => {
    const { container, store } = renderEditor();

    act(() => store.setErrors([{ path: '$.rule', code: 'UnknownSpec', message: 'no such spec' }]));
    act(() => store.setErrors([]));

    expect(container.querySelector('.cm-lintRange-error')).toBeNull();
  });

  it('opens the payload popover for the spec node under the caret', () => {
    const { container } = renderEditor();
    const view = editorView(container);

    act(() => view.dispatch({ selection: { anchor: 2 } }));

    expect(screen.getByRole('dialog', { name: 'Payload for is-active' })).toBeTruthy();
  });

  // jsdom has no layout, so `coordsAtPos` and every rect come back null or zero. The popover
  // must still be placed — degenerately, but inside the frame — rather than throwing or being
  // left to whatever the stylesheet's default corner is.
  it('positions the popover explicitly even without layout to measure', () => {
    const { container } = renderEditor();
    const view = editorView(container);

    act(() => view.dispatch({ selection: { anchor: 2 } }));

    const popover = screen.getByRole('dialog', { name: 'Payload for is-active' });
    expect(popover.style.top).toMatch(/^-?\d+(\.\d+)?px$/);
    expect(popover.style.left).toMatch(/^-?\d+(\.\d+)?px$/);
    expect(Number.parseFloat(popover.style.top)).toBeGreaterThanOrEqual(0);
    expect(Number.parseFloat(popover.style.left)).toBeGreaterThanOrEqual(0);
  });

  it('anchors the popover rather than pinning it to a corner of the frame', () => {
    const { container } = renderEditor();
    const view = editorView(container);

    act(() => view.dispatch({ selection: { anchor: 2 } }));

    // `right`/`bottom` would let the card ride up over the toolbar; it is placed from the
    // frame's top-left instead, below the editing surface's first row.
    const popover = screen.getByRole('dialog', { name: 'Payload for is-active' });
    expect(popover.style.right).toBe('');
    expect(popover.style.bottom).toBe('');
  });
});
