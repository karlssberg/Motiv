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

describe('BuilderPane accordion (boolean)', () => {
  it('wraps a leaf in AND and shows two operands', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule');
    fireEvent.click(screen.getByRole('button', { name: 'wrap $.rule in AND' }));
    const rule = store.getState().document.rule as { and?: unknown[] };
    expect(rule.and).toHaveLength(2);
  });

  it('toggles NOT on a leaf and back', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule');
    fireEvent.click(screen.getByRole('button', { name: 'toggle NOT at $.rule' }));
    expect(store.getState().document.rule).toEqual({ not: { spec: 'is-active' } });
    fireEvent.click(screen.getByRole('button', { name: 'toggle NOT at $.rule' }));
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
  });

  it('edits whenTrue decoration into the document', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule');
    fireEvent.change(screen.getByLabelText('whenTrue at $.rule'), { target: { value: 'yes' } });
    expect((store.getState().document.rule as { whenTrue?: string }).whenTrue).toBe('yes');
  });

  it('changes a leaf spec via the catalog select', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    const select = await screen.findByLabelText('spec at $.rule');
    fireEvent.change(select, { target: { value: 'is-adult' } });
    expect(store.getState().document.rule).toEqual({ spec: 'is-adult' });
  });

  it('re-expanding a child does not collapse the root subtree', async () => {
    const store = new RuleEditorStore({ rule: { and: [ { or: [ { spec: 'is-active' }, { spec: 'is-adult' } ] }, { spec: 'is-active' } ] } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule.and[1]');
    fireEvent.click(screen.getByRole('button', { name: 'collapse $.rule.and[0]' }));
    fireEvent.click(screen.getByRole('button', { name: 'expand $.rule.and[0]' }));
    // root must still be expanded → its second operand's header still rendered
    expect(screen.getByLabelText('spec at $.rule.and[1]')).toBeDefined();
  });

  it('offers remove only on operand elements, not on a NOT child', async () => {
    const store = new RuleEditorStore({ rule: { not: { spec: 'is-active' } } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule.not');
    expect(screen.queryByRole('button', { name: 'remove $.rule.not' })).toBeNull();
  });
});
