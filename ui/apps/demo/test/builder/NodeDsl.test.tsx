import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';
import { editorView, replaceBuffer } from '../support/codemirror.js';

const catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
    { name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
  ],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

describe('DSL rows', () => {
  it('renders a leaf as its bare spec name', async () => {
    renderWith(new RuleEditorStore({ rule: { spec: 'is-active' } }));
    const row = await screen.findByRole('button', { name: 'edit expression at $.rule' });
    expect(row.textContent).toBe('is-active');
  });

  it('renders a collapsed subtree as one line of DSL', async () => {
    const store = new RuleEditorStore({
      rule: { or: [{ spec: 'is-active' }, { not: { spec: 'is-adult' } }] },
    });
    renderWith(store);
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    expect(screen.getByRole('button', { name: 'edit expression at $.rule' }).textContent).toBe('is-active | !is-adult');
  });

  it('renders a collapsed quantifier body on the same line', async () => {
    const store = new RuleEditorStore({
      rule: { asAtLeastNSatisfied: { spec: 'is-active' }, n: 2, path: 'orders' },
    });
    renderWith(store);
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    expect(screen.getByRole('button', { name: 'edit expression at $.rule' }).textContent)
      .toBe('atLeast(2) in orders { is-active }');
  });

  it('shows the badge and gloss while expanded, not the DSL', async () => {
    const store = new RuleEditorStore({ rule: { or: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await screen.findByRole('button', { name: 'collapse $.rule' });
    expect(screen.queryByRole('button', { name: 'edit expression at $.rule' })).toBeNull();
    expect(screen.getByText('any may hold')).toBeDefined();
  });

  it('classifies tokens so they can be coloured', async () => {
    renderWith(new RuleEditorStore({ rule: { not: { spec: 'is-active' } } }));
    const row = await screen.findByRole('button', { name: 'edit expression at $.rule.not' });
    expect(row.querySelector('.tok-spec')).not.toBeNull();
  });
});

describe('DSL row editing', () => {
  const focusRow = async (path: string) => {
    fireEvent.focus(await screen.findByRole('button', { name: `edit expression at ${path}` }));
  };
  const content = (container: HTMLElement) => container.querySelector('.cm-content')!;

  /**
   * The two ways into a row want opposite selections, so they are tested as a pair.
   *
   * A keyboard entry has no point to aim at — Tab lands on the row as a whole, and selecting the
   * buffer makes the obvious next keystroke replace it. A click *does* carry a point, and honouring
   * it is the difference between editing a word and retyping the line. Where that point lands is a
   * question of layout, so the mapping itself is proved in `e2e/inline-edit.spec.ts`; jsdom has no
   * layout, and can only prove that a click is not treated as a keyboard entry.
   */
  it('selects the whole expression when the row is reached by keyboard', async () => {
    const { container } = renderWith(new RuleEditorStore({ rule: { spec: 'is-active' } }));
    await focusRow('$.rule');
    const { from, to } = editorView(container).state.selection.main;
    expect([from, to]).toEqual([0, 'is-active'.length]);
  });

  // Deliberately not titled "places a caret where you clicked": with no layout, `posAtCoords` has
  // nothing to resolve against, so this reaches the same end-of-buffer fallback a mapping-free
  // implementation would. What it does prove is that a click is not routed to the keyboard's
  // select-all — the mapping itself is `e2e/inline-edit.spec.ts`'s to prove.
  it('does not select the whole expression when the row is clicked', async () => {
    const { container } = renderWith(new RuleEditorStore({ rule: { spec: 'is-active' } }));
    const row = await screen.findByRole('button', { name: 'edit expression at $.rule' });
    fireEvent.mouseDown(row, { clientX: 20, clientY: 5 });
    expect(editorView(container).state.selection.main.empty).toBe(true);
  });

  it('commits a valid edit into the document', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    await focusRow('$.rule');
    replaceBuffer(container, 'is-adult & is-active');
    fireEvent.keyDown(content(container), { key: 'Enter' });
    expect(store.getState().document.rule).toEqual({
      and: [{ spec: 'is-adult' }, { spec: 'is-active' }],
    });
  });

  it('blocks an unparseable edit and leaves the document alone', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    await focusRow('$.rule');
    replaceBuffer(container, 'is-active &');
    fireEvent.keyDown(content(container), { key: 'Enter' });
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
    expect(screen.getByRole('alert').textContent).toMatch(/expected|unexpected/i);
  });

  it('clears a refused commit\'s error as soon as you keep typing', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    await focusRow('$.rule');

    // Half-typed parenthesis: the commit is refused and the error appears.
    replaceBuffer(container, '(is-active & is-adult');
    fireEvent.keyDown(content(container), { key: 'Enter' });
    expect(screen.getByRole('alert')).toBeDefined();

    // Finishing the expression must not leave the message sitting beside the field, competing
    // with it for the row's width while you are still typing.
    replaceBuffer(container, '(is-active & is-adult)');
    expect(screen.queryByRole('alert')).toBeNull();

    fireEvent.keyDown(content(container), { key: 'Enter' });
    expect(store.getState().document.rule).toEqual({
      and: [{ spec: 'is-active' }, { spec: 'is-adult' }],
    });
  });

  it('keeps the whole message reachable when the row truncates it', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    await focusRow('$.rule');
    replaceBuffer(container, '(is-active');
    fireEvent.keyDown(content(container), { key: 'Enter' });

    const alert = screen.getByRole('alert');
    expect(alert.getAttribute('title')).toBe(alert.textContent);
  });

  it('escape reverts to the node as it stands', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    await focusRow('$.rule');
    replaceBuffer(container, 'is-adult');
    fireEvent.keyDown(content(container), { key: 'Escape' });
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
    expect(screen.getByRole('button', { name: 'edit expression at $.rule' }).textContent).toBe('is-active');
  });

  it('round-trips a focus-and-blur with no edit', async () => {
    const rule = { asAtLeastNSatisfied: { spec: 'is-active' }, n: '@minOrders', path: 'orders' };
    const store = new RuleEditorStore({
      parameters: { minOrders: { type: 'integer', default: 3 } },
      rule,
    });
    const { container } = renderWith(store);
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    await focusRow('$.rule');
    fireEvent.blur(content(container));
    expect(store.getState().document.rule).toEqual(rule);
  });

  it('does not write back when the row unmounts mid-edit', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container, unmount } = renderWith(store);
    await focusRow('$.rule');
    replaceBuffer(container, 'is-adult');
    unmount();
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
  });
});
