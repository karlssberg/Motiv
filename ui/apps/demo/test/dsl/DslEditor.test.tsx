import { describe, it, expect } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { EditorView } from '@codemirror/view';
import { RuleEditorStore } from '@motiv/rules-core';
import type { Catalog } from '@motiv/rules-core';
import { DslEditor } from '../../src/dsl/DslEditor.js';

const CATALOG: Catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Currently active.' },
    { name: 'is-verified', modelType: 'customer', metadataType: 'String', isAsync: false },
  ],
  collections: [],
};

function renderEditor() {
  const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
  const { container } = render(<DslEditor store={store} catalog={CATALOG} />);
  return { store, container };
}

/** The live CodeMirror view, so tests drive the editor the way a keystroke would. */
function editorView(container: HTMLElement): EditorView {
  const view = EditorView.findFromDOM(container);
  if (!view) throw new Error('No CodeMirror view was mounted.');
  return view;
}

/** The editor's visible text. */
function editorText(container: HTMLElement): string {
  return container.querySelector('.cm-content')?.textContent ?? '';
}

/** Types over the whole document, as a user replacing the buffer would. */
function replaceBuffer(container: HTMLElement, text: string): void {
  const view = editorView(container);
  act(() => view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: text } }));
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

  it('opens the payload popover for the spec node under the caret', () => {
    const { container } = renderEditor();
    const view = editorView(container);

    act(() => view.dispatch({ selection: { anchor: 2 } }));

    expect(screen.getByRole('dialog', { name: 'Payload for is-active' })).toBeTruthy();
  });
});
