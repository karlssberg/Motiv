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

const AND = { rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } };

describe('operator picker', () => {
  it('offers every binary operator, with the node\'s own selected', async () => {
    renderWith(new RuleEditorStore(AND));
    const picker = await screen.findByLabelText('operator at $.rule') as HTMLSelectElement;
    expect(Array.from(picker.options).map((o) => o.value))
      .toEqual(['and', 'or', 'xor', 'andAlso', 'orElse']);
    expect(picker.value).toBe('and');
  });

  it('rewrites the node under the chosen operator, keeping its operands in order', async () => {
    const store = new RuleEditorStore(AND);
    renderWith(store);
    fireEvent.change(await screen.findByLabelText('operator at $.rule'), { target: { value: 'orElse' } });

    const rule = store.getState().document.rule as unknown as Record<string, unknown>;
    expect(rule).toEqual({ orElse: [{ spec: 'is-active' }, { spec: 'is-adult' }] });
    expect('and' in rule).toBe(false);
  });

  it('preserves decoration across the change', async () => {
    const store = new RuleEditorStore({
      rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }], name: 'pair', whenTrue: 'yes' },
    });
    renderWith(store);
    fireEvent.change(await screen.findByLabelText('operator at $.rule'), { target: { value: 'xor' } });

    expect(store.getState().document.rule).toEqual({
      xor: [{ spec: 'is-active' }, { spec: 'is-adult' }], name: 'pair', whenTrue: 'yes',
    });
  });

  it('restates the gloss for the operator now in force', async () => {
    renderWith(new RuleEditorStore(AND));
    expect(await screen.findByText('all must hold')).toBeDefined();
    fireEvent.change(screen.getByLabelText('operator at $.rule'), { target: { value: 'or' } });
    expect(screen.getByText('any may hold')).toBeDefined();
    expect(screen.queryByText('all must hold')).toBeNull();
  });

  it('is offered only by binary nodes', async () => {
    renderWith(new RuleEditorStore({
      rule: { not: { asAllSatisfied: { spec: 'is-active' }, path: 'orders' } },
    }));
    await screen.findByRole('button', { name: 'collapse $.rule' });
    expect(screen.queryByLabelText('operator at $.rule')).toBeNull();
    expect(screen.queryByLabelText('operator at $.rule.not')).toBeNull();
  });

  it('gives way to the text once the subtree is collapsed', async () => {
    renderWith(new RuleEditorStore(AND));
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    expect(screen.queryByLabelText('operator at $.rule')).toBeNull();
    expect(screen.getByRole('button', { name: 'edit expression at $.rule' }).textContent)
      .toBe('is-active & is-adult');
  });
});
