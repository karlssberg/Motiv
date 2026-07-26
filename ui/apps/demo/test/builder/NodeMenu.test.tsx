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
  collections: [],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

const COMPOSITE = { rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } };

const openMenu = async (path: string) => {
  fireEvent.click(await screen.findByRole('button', { name: `actions for ${path}` }));
};

describe('NodeMenu', () => {
  it('opens from the row, not from inside the detail panel', async () => {
    const { container } = renderWith(new RuleEditorStore(COMPOSITE));
    const trigger = await screen.findByRole('button', { name: 'actions for $.rule' });
    expect(trigger.closest('.node-row')).not.toBeNull();
    expect(trigger.closest('.node-detail')).toBeNull();
    expect(container.querySelector('.node-menu')).toBeNull();
  });

  it('offers the same items on a leaf and on a parent', async () => {
    renderWith(new RuleEditorStore(COMPOSITE));
    await openMenu('$.rule.and[0]');
    expect(screen.getByRole('menuitem', { name: 'Details' })).toBeDefined();
    expect(screen.getByRole('menuitem', { name: 'Remove' })).toBeDefined();

    fireEvent.keyDown(document, { key: 'Escape' });
    await openMenu('$.rule');
    expect(screen.getByRole('menuitem', { name: 'Details' })).toBeDefined();
  });

  it('Details opens the node\'s metadata panel', async () => {
    renderWith(new RuleEditorStore(COMPOSITE));
    await openMenu('$.rule');
    fireEvent.click(screen.getByRole('menuitem', { name: 'Details' }));
    expect(screen.getByLabelText('name at $.rule')).toBeDefined();
    // Acting closes the menu — it is transient, unlike the panel it opens.
    expect(screen.queryByRole('menuitem', { name: 'Details' })).toBeNull();
  });

  it('Remove deletes the operand', async () => {
    const store = new RuleEditorStore({
      rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }, { spec: 'is-active' }] },
    });
    renderWith(store);
    await openMenu('$.rule.and[1]');
    fireEvent.click(screen.getByRole('menuitem', { name: 'Remove' }));
    expect((store.getState().document.rule as { and: unknown[] }).and).toHaveLength(2);
  });

  it('Remove collapses a two-operand parent to its survivor', async () => {
    const store = new RuleEditorStore(COMPOSITE);
    renderWith(store);
    await openMenu('$.rule.and[1]');
    fireEvent.click(screen.getByRole('menuitem', { name: 'Remove' }));
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
  });

  it('omits Remove where there is no operand to remove', async () => {
    renderWith(new RuleEditorStore({ rule: { not: { spec: 'is-active' } } }));
    await openMenu('$.rule.not');
    expect(screen.getByRole('menuitem', { name: 'Details' })).toBeDefined();
    expect(screen.queryByRole('menuitem', { name: 'Remove' })).toBeNull();
  });

  it('closes on Escape and hands focus back to its trigger', async () => {
    renderWith(new RuleEditorStore(COMPOSITE));
    await openMenu('$.rule');
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('menu')).toBeNull();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'actions for $.rule' }));
  });

  it('closes when a click lands outside it', async () => {
    renderWith(new RuleEditorStore(COMPOSITE));
    await openMenu('$.rule');
    fireEvent.mouseDown(document.body);
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('opens only one menu at a time', async () => {
    renderWith(new RuleEditorStore(COMPOSITE));
    await openMenu('$.rule');
    await openMenu('$.rule.and[0]');
    expect(screen.getAllByRole('menu')).toHaveLength(1);
  });
});
