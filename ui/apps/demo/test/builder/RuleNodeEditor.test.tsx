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
  const openDetail = async (path: string) => {
    fireEvent.click(await screen.findByRole('button', { name: `details for ${path}` }));
  };

  it('starts with every detail panel closed', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await screen.findByRole('button', { name: 'details for $.rule' });
    expect(screen.queryByLabelText('name at $.rule')).toBeNull();
    expect(screen.queryByRole('button', { name: 'toggle NOT at $.rule' })).toBeNull();
  });

  it('starts with every subtree expanded', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    expect(await screen.findByRole('button', { name: 'details for $.rule.and[1]' })).toBeDefined();
  });

  it('holds nothing that can change the node it belongs to', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    const { container } = renderWith(store);
    await openDetail('$.rule');

    // A control here that re-kinded the node would re-render the panel into a different thing —
    // the reveal invalidating its own trigger. Structure is authored in the row instead.
    for (const name of ['toggle NOT at $.rule', 'wrap $.rule in AND', 'add operand to $.rule']) {
      expect(screen.queryByRole('button', { name })).toBeNull();
    }
    expect(container.querySelector('.node-detail .decoration')).not.toBeNull();
  });

  it('offers no remove, which would delete the panel you opened', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule.and[1]');
    expect(screen.queryByRole('button', { name: 'remove $.rule.and[1]' })).toBeNull();
  });

  it('edits whenTrue decoration into the document', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.change(screen.getByLabelText('whenTrue at $.rule'), { target: { value: 'yes' } });
    expect((store.getState().document.rule as { whenTrue?: string }).whenTrue).toBe('yes');
  });

  it('opening one panel closes the previously open one', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule.and[0]');
    expect(screen.getByLabelText('name at $.rule.and[0]')).toBeDefined();
    await openDetail('$.rule.and[1]');
    expect(screen.queryByLabelText('name at $.rule.and[0]')).toBeNull();
    expect(screen.getByLabelText('name at $.rule.and[1]')).toBeDefined();
  });

  it('collapsing a subtree hides its children but not its detail panel', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.click(screen.getByRole('button', { name: 'collapse $.rule' }));
    expect(screen.queryByRole('button', { name: 'details for $.rule.and[1]' })).toBeNull();
    expect(screen.getByLabelText('name at $.rule')).toBeDefined();
  });

  it('re-expanding a child does not collapse the root subtree', async () => {
    const store = new RuleEditorStore({ rule: { and: [ { or: [ { spec: 'is-active' }, { spec: 'is-adult' } ] }, { spec: 'is-active' } ] } });
    renderWith(store);
    await screen.findByRole('button', { name: 'details for $.rule.and[1]' });
    fireEvent.click(screen.getByRole('button', { name: 'collapse $.rule.and[0]' }));
    fireEvent.click(screen.getByRole('button', { name: 'expand $.rule.and[0]' }));
    expect(screen.getByRole('button', { name: 'details for $.rule.and[1]' })).toBeDefined();
  });

  it('no longer offers a spec select — the row is the way to change a spec', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openDetail('$.rule');
    expect(screen.queryByLabelText('spec at $.rule')).toBeNull();
  });

  it('no longer offers an add-quantifier button', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule');
    expect(screen.queryByRole('button', { name: 'add quantifier to $.rule' })).toBeNull();
  });

  it('opens a leaf\'s metadata from its caret, which is the leaf\'s only disclosure control', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    const caret = await screen.findByRole('button', { name: 'details for $.rule' });

    // The caret slot is the disclosure — a leaf has nothing else to reveal, so it carries no
    // separate `⋯` toggle.
    expect(caret.className).toContain('node-chev');
    expect(caret.textContent).toBe('▸');
    expect(container.querySelector('.node-detail-toggle')).toBeNull();

    fireEvent.click(caret);
    expect(caret.textContent).toBe('▾');
    expect(caret.getAttribute('aria-expanded')).toBe('true');
    expect(screen.getByLabelText('name at $.rule')).toBeDefined();
  });

  it('keeps the caret structural on a parent, which discloses metadata separately', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    const { container } = renderWith(store);
    await screen.findByRole('button', { name: 'collapse $.rule' });

    // A parent's caret is taken by its subtree, so it keeps a `⋯` of its own.
    expect(container.querySelector('.node-detail-toggle')).not.toBeNull();
    expect(screen.getByRole('button', { name: 'details for $.rule' }).className)
      .toContain('node-detail-toggle');
  });

});
