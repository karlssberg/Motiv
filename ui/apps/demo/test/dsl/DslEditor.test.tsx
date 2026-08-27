import { describe, it, expect } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore } from '@motiv-rules/core';
import type { Catalog, RuleNode } from '@motiv-rules/core';
import { DslEditor } from '../../src/dsl/DslEditor.js';
import { useDslSync } from '@motiv-rules/react';
import { editorText, editorView, replaceBuffer } from '../support/codemirror.js';

const CATALOG: Catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Currently active.', origin: 'Compiled' },
    { name: 'is-verified', modelType: 'customer', metadataType: 'String', isAsync: false, origin: 'Compiled' },
  ],
  collections: [],
};

/** Stands in for the pane that owns the buffer, which the editor takes as a prop. */
function Host(props: { store: RuleEditorStore }) {
  const sync = useDslSync(props.store);
  return <DslEditor store={props.store} catalog={CATALOG} sync={sync} />;
}

const BOTH_SPECS = { andAlso: [{ spec: 'is-active' }, { spec: 'is-verified' }] };

function renderEditor(rule: RuleNode = { spec: 'is-active' }) {
  const store = new RuleEditorStore({ rule });
  const { container } = render(<Host store={store} />);
  return { store, container };
}

/** The chip a spec node is edited from, which is the only way the card is opened by pointer. */
function payloadChip(spec: string): HTMLElement {
  return screen.getByRole('button', { name: `Edit ${spec} payload` });
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

  // Moving the caret is how you start typing, so it must not summon a card over the text. The
  // card is opened deliberately instead — from a spec node's chip, or from the keyboard.
  it('leaves the payload popover closed when the caret moves onto a spec node', () => {
    const { container } = renderEditor();
    const view = editorView(container);

    act(() => view.dispatch({ selection: { anchor: 2 } }));

    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('marks every spec node with a payload chip', () => {
    renderEditor(BOTH_SPECS);

    expect(payloadChip('is-active')).toBeTruthy();
    expect(payloadChip('is-verified')).toBeTruthy();
  });

  // The chip is chrome drawn into the document, not document text — it must not leak into the
  // buffer that is parsed and committed.
  it('keeps the chips out of the editor text', () => {
    const { container } = renderEditor();

    expect(payloadChip('is-active')).toBeTruthy();
    expect(editorText(container)).toBe('is-active');
  });

  it('opens the payload popover from a spec node chip', async () => {
    const user = userEvent.setup();
    renderEditor();

    await user.click(payloadChip('is-active'));

    expect(screen.getByRole('dialog', { name: 'Payload for is-active' })).toBeTruthy();
  });

  // The chip is a toggle: the press that opened the card puts it away again, so a misfired click
  // is undone by repeating it rather than by hunting for Cancel.
  it('closes the payload popover when its own chip is pressed again', async () => {
    const user = userEvent.setup();
    renderEditor();

    await user.click(payloadChip('is-active'));
    await user.click(payloadChip('is-active'));

    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('switches to the other node when a second chip is pressed', async () => {
    const user = userEvent.setup();
    renderEditor(BOTH_SPECS);

    await user.click(payloadChip('is-active'));
    await user.click(payloadChip('is-verified'));

    expect(screen.getByRole('dialog', { name: 'Payload for is-verified' })).toBeTruthy();
  });

  it('opens the payload popover for the node under the caret from the keyboard', async () => {
    const user = userEvent.setup();
    const { container } = renderEditor();
    const view = editorView(container);

    act(() => view.dispatch({ selection: { anchor: 2 } }));
    view.focus();
    await user.keyboard('{Control>}.{/Control}');

    expect(screen.getByRole('dialog', { name: 'Payload for is-active' })).toBeTruthy();
  });

  // The card is anchored to a token in the text. Editing that text moves it out from under the
  // card — and may delete the node the draft would be saved onto — so the card goes with it.
  it('closes the payload popover when the text is edited', async () => {
    const user = userEvent.setup();
    const { container } = renderEditor();

    await user.click(payloadChip('is-active'));
    replaceBuffer(container, 'is-verified');

    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('closes the payload popover on Escape', async () => {
    const user = userEvent.setup();
    renderEditor();

    await user.click(payloadChip('is-active'));
    await user.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).toBeNull();
  });

  // jsdom has no layout, so `coordsAtPos` and every rect come back null or zero. The popover
  // must still be placed — degenerately, but on screen — rather than throwing or being left to
  // whatever the stylesheet's default corner is.
  it('positions the popover explicitly even without layout to measure', async () => {
    const user = userEvent.setup();
    renderEditor();

    await user.click(payloadChip('is-active'));

    const popover = screen.getByRole('dialog', { name: 'Payload for is-active' });
    expect(popover.style.top).toMatch(/^-?\d+(\.\d+)?px$/);
    expect(popover.style.left).toMatch(/^-?\d+(\.\d+)?px$/);
    expect(Number.parseFloat(popover.style.top)).toBeGreaterThanOrEqual(0);
    expect(Number.parseFloat(popover.style.left)).toBeGreaterThanOrEqual(0);
  });

  // The popover keeps an unsaved draft of the node it was opened for. Opening it on a different
  // node changes which node a save writes to, so a draft carried across that move is not a stale
  // field — it is one node's edits landing on another.
  it('does not carry an unsaved draft from one spec node onto the next', async () => {
    const user = userEvent.setup();
    const { container, store } = renderEditor(BOTH_SPECS);
    expect(editorText(container)).toBe('is-active && is-verified');

    // Start naming the first spec, then open the second without saving.
    await user.click(payloadChip('is-active'));
    await user.type(screen.getByLabelText('Name'), 'activity');
    await user.click(payloadChip('is-verified'));

    expect(screen.getByLabelText<HTMLInputElement>('Name').value).toBe('');

    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(store.getState().document).toEqual({ rule: BOTH_SPECS });
  });

  it('anchors the popover rather than pinning it to a corner', async () => {
    const user = userEvent.setup();
    renderEditor();

    await user.click(payloadChip('is-active'));

    // A `right`/`bottom` corner pin is what let the card cover the toolbar; it is placed from
    // the measured token instead, and only `top`/`left` are ever written.
    const popover = screen.getByRole('dialog', { name: 'Payload for is-active' });
    expect(popover.style.right).toBe('');
    expect(popover.style.bottom).toBe('');
  });
});
