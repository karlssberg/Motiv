import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = {
  specs: [
    { name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
    { name: 'is-large-order', modelType: 'order', metadataType: 'String', isAsync: false, description: null },
  ],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

describe('QuantifierNode', () => {
  it('inserts an all-satisfied quantifier over a collection with an element-scoped child', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-adult' } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule');

    fireEvent.click(screen.getByRole('button', { name: 'wrap $.rule in AND' }));       // AND[is-adult, is-adult]
    fireEvent.click(screen.getByRole('button', { name: 'add quantifier to $.rule' })); // AND[..., quantifier]

    const q = store.getState().document.rule as unknown as { and: Array<Record<string, unknown>> };
    const quant = q.and[q.and.length - 1];
    expect(quant).toMatchObject({ asAllSatisfied: { spec: 'is-large-order' }, path: 'orders' });
  });

  it('changes kind to at-least-N, sets n, and scopes the child picker to element specs', async () => {
    const store = new RuleEditorStore({
      rule: { asAllSatisfied: { spec: 'is-large-order' }, path: 'orders' },
    });
    renderWith(store);

    fireEvent.change(await screen.findByLabelText('quantifier kind at $.rule'), { target: { value: 'asAtLeastNSatisfied' } });
    fireEvent.change(screen.getByLabelText('quantifier n at $.rule'), { target: { value: '2' } });

    const rule = store.getState().document.rule as unknown as Record<string, unknown>;
    expect(rule).toMatchObject({ asAtLeastNSatisfied: { spec: 'is-large-order' }, n: 2, path: 'orders' });

    const childSelect = screen.getByLabelText('spec at $.rule.asAtLeastNSatisfied') as HTMLSelectElement;
    const options = Array.from(childSelect.options).map((o) => o.value);
    expect(options).toEqual(['is-large-order']);
  });

  it('preserves decoration when the quantifier kind changes', async () => {
    const store = new RuleEditorStore({ rule: { asAllSatisfied: { spec: 'is-large-order' }, path: 'orders', name: 'big spender' } });
    renderWith(store);
    fireEvent.change(await screen.findByLabelText('quantifier kind at $.rule'), { target: { value: 'asAnySatisfied' } });
    const rule = store.getState().document.rule as unknown as Record<string, unknown>;
    expect(rule).toMatchObject({ asAnySatisfied: { spec: 'is-large-order' }, path: 'orders', name: 'big spender' });
    expect('asAllSatisfied' in rule).toBe(false);
    expect('n' in rule).toBe(false);
  });
});
