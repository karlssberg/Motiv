import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

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
