import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';
import { replaceBuffer } from '../support/codemirror.js';

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
    const row = await screen.findByLabelText('expression at $.rule');
    expect(row.textContent).toBe('is-active');
  });

  it('renders a collapsed subtree as one line of DSL', async () => {
    const store = new RuleEditorStore({
      rule: { or: [{ spec: 'is-active' }, { not: { spec: 'is-adult' } }] },
    });
    renderWith(store);
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    expect(screen.getByLabelText('expression at $.rule').textContent).toBe('is-active | !is-adult');
  });

  it('renders a collapsed quantifier body on the same line', async () => {
    const store = new RuleEditorStore({
      rule: { asAtLeastNSatisfied: { spec: 'is-active' }, n: 2, path: 'orders' },
    });
    renderWith(store);
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    expect(screen.getByLabelText('expression at $.rule').textContent)
      .toBe('atLeast(2) in orders { is-active }');
  });

  it('shows the badge and gloss while expanded, not the DSL', async () => {
    const store = new RuleEditorStore({ rule: { or: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await screen.findByRole('button', { name: 'collapse $.rule' });
    expect(screen.queryByLabelText('expression at $.rule')).toBeNull();
    expect(screen.getByText('any may hold')).toBeDefined();
  });

  it('classifies tokens so they can be coloured', async () => {
    renderWith(new RuleEditorStore({ rule: { not: { spec: 'is-active' } } }));
    const row = await screen.findByLabelText('expression at $.rule.not');
    expect(row.querySelector('.tok-spec')).not.toBeNull();
  });
});

describe('DSL row editing', () => {
  const focusRow = async (path: string) => {
    fireEvent.focus(await screen.findByRole('button', { name: `edit expression at ${path}` }));
  };
  const content = (container: HTMLElement) => container.querySelector('.cm-content')!;

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

  it('escape reverts to the node as it stands', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    await focusRow('$.rule');
    replaceBuffer(container, 'is-adult');
    fireEvent.keyDown(content(container), { key: 'Escape' });
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
    expect(screen.getByLabelText('expression at $.rule').textContent).toBe('is-active');
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
