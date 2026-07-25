import { describe, it, expect, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { EditorView } from '@codemirror/view';
import { RuleEditorStore, type Catalog, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { EditorPane } from '../../src/panes/EditorPane.js';

const catalog: Catalog = {
  specs: [
    {
      name: 'is-active',
      modelType: 'customer',
      metadataType: 'String',
      isAsync: false,
      description: 'Whether the customer account is active',
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

describe('EditorPane', () => {
  it('shows the Builder surface by default', async () => {
    renderPane();
    await settleCatalog();

    expect(tab('Builder').getAttribute('aria-selected')).toBe('true');
    expect(tab('DSL').getAttribute('aria-selected')).toBe('false');
    expect(screen.getByLabelText('spec at $.rule')).toBeDefined();
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
    expect(screen.getByLabelText('spec at $.rule')).toBeDefined();
  });

  it('keeps an uncommitted DSL edit across a round trip through the Builder', async () => {
    const { container } = renderPane();
    await settleCatalog();

    fireEvent.click(tab('DSL'));
    replaceBuffer(container, 'is-active && is-adult');
    expect(screen.getByLabelText('sync status').textContent).toBe('unsynced');

    fireEvent.click(tab('Builder'));
    fireEvent.click(tab('DSL'));

    // The buffer outlives the view, so the pending commit is neither lost nor forced through.
    expect(editorText(container)).toBe('is-active && is-adult');
    expect(screen.getByLabelText('sync status').textContent).toBe('unsynced');
  });

  it('hands the loaded catalog to the DSL surface', async () => {
    const { container } = renderPane();
    await settleCatalog();

    fireEvent.click(tab('DSL'));
    expect(screen.queryByRole('alert')).toBeNull();

    // Putting the caret inside the spec opens the popover, which renders catalog metadata —
    // proof the catalog reached the DSL surface rather than the empty fallback.
    const view = editorView(container);
    act(() => view.dispatch({ selection: { anchor: 2 } }));

    expect(screen.getByText('Whether the customer account is active')).toBeDefined();
  });
});
