import { describe, it, expect, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RuleEditorStore, type Catalog, type RulesApiClient } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { EditorPane } from '../../src/panes/EditorPane.js';
import { editorText, replaceBuffer } from '../support/codemirror.js';

const catalog: Catalog = {
  specs: [
    {
      name: 'is-active',
      modelType: 'customer',
      metadataType: 'String',
      isAsync: false,
      description: 'Whether the customer account is active',
      origin: 'Compiled',
    },
  ],
  collections: [],
};

function client(): RulesApiClient {
  return {
    getCatalog: vi.fn().mockResolvedValue(catalog),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    evaluate: vi.fn(),
  } as unknown as RulesApiClient;
}

function renderPane() {
  const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
  const { container } = render(
    <RuleEditorProvider store={store}>
      <EditorPane client={client()} />
    </RuleEditorProvider>,
  );
  return { store, container };
}

/** Flushes the mocked getCatalog resolution into the pane's state. */
const settleCatalog = () => act(async () => {});

const tab = (name: string) => screen.getByRole('tab', { name });

describe('EditorPane', () => {
  it('shows the Builder surface by default', async () => {
    renderPane();
    await settleCatalog();

    expect(tab('Builder').getAttribute('aria-selected')).toBe('true');
    expect(tab('DSL').getAttribute('aria-selected')).toBe('false');
    expect(screen.getByRole('button', { name: 'details for $.rule' })).toBeDefined();
    expect(screen.queryByLabelText('sync status')).toBeNull();
  });

  it('switches to the DSL surface when its tab is clicked', async () => {
    renderPane();
    await settleCatalog();

    fireEvent.click(tab('DSL'));

    expect(tab('DSL').getAttribute('aria-selected')).toBe('true');
    expect(tab('Builder').getAttribute('aria-selected')).toBe('false');
    expect(screen.getByLabelText('sync status')).toBeDefined();
    expect(screen.getByText('text is the source of truth')).toBeDefined();
  });

  // The header is a single row of three items and the pane is narrow, so the hint — the least
  // important of them — is the one that gives way. Without this the row wraps to two lines and
  // collides with the editor toolbar below it.
  it('lets the hint truncate rather than wrap the header', async () => {
    renderPane();
    await settleCatalog();

    fireEvent.click(tab('DSL'));

    expect(screen.getByText('text is the source of truth').classList.contains('truncate')).toBe(true);
  });

  it('nests no second region inside the Editor pane', async () => {
    renderPane();
    await settleCatalog();

    fireEvent.click(tab('DSL'));

    expect(screen.queryByRole('region', { name: 'DSL' })).toBeNull();
  });

  it('returns to the Builder surface when its tab is clicked again', async () => {
    renderPane();
    await settleCatalog();

    fireEvent.click(tab('DSL'));
    fireEvent.click(tab('Builder'));

    expect(tab('Builder').getAttribute('aria-selected')).toBe('true');
    expect(screen.queryByLabelText('sync status')).toBeNull();
    expect(screen.getByRole('button', { name: 'details for $.rule' })).toBeDefined();
  });

  it('keeps an uncommitted DSL edit across a round trip through the Builder', async () => {
    const { container } = renderPane();
    await settleCatalog();

    fireEvent.click(tab('DSL'));
    replaceBuffer(container, 'is-active && is-adult');
    expect(screen.getByLabelText('sync status').textContent).toBe('unsynced');

    fireEvent.click(tab('Builder'));
    fireEvent.click(tab('DSL'));

    expect(editorText(container)).toBe('is-active && is-adult');
    expect(screen.getByLabelText('sync status').textContent).toBe('unsynced');
  });

  it('lands the pending commit even while the DSL surface is unmounted', async () => {
    const { container, store } = renderPane();
    await settleCatalog();

    fireEvent.click(tab('DSL'));
    replaceBuffer(container, 'is-active && is-adult');
    fireEvent.click(tab('Builder'));

    // The debounced commit was armed by the edit and belongs to the buffer, not the view, so
    // tearing the view down must not cancel it — a cancelled timer would also leave the text
    // intact, which is why the store, not the buffer, is what proves the commit survived.
    await waitFor(() => expect(store.getState().document).toEqual({
      rule: { andAlso: [{ spec: 'is-active' }, { spec: 'is-adult' }] },
    }));
  });

  it('hands the loaded catalog to the DSL surface', async () => {
    renderPane();
    await settleCatalog();

    fireEvent.click(tab('DSL'));
    expect(screen.queryByRole('alert')).toBeNull();

    // Opening a spec's payload card renders catalog metadata — proof the catalog reached the DSL
    // surface rather than the empty fallback.
    fireEvent.click(screen.getByRole('button', { name: 'Edit is-active payload' }));

    expect(screen.getByText('Whether the customer account is active')).toBeDefined();
  });
});
