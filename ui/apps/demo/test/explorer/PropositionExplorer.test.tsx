import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PropositionListEntry } from '@motiv/rules-core';
import { PropositionExplorer } from '../../src/explorer/PropositionExplorer.js';

function entry(overrides: Partial<PropositionListEntry> & { name: string }): PropositionListEntry {
  return {
    modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Authored', version: 1, description: null, quarantine: [],
    ...overrides,
  };
}

const ENTRIES = [
  entry({ name: 'customer.eligibility.is-active', origin: 'Compiled', version: 0 }),
  entry({ name: 'customer.eligibility.is-adult' }),
  entry({ name: 'customer.risk.is-fraudulent' }),
  entry({ name: 'order.is-large', modelType: 'order' }),
];

function renderExplorer(overrides: Partial<Parameters<typeof PropositionExplorer>[0]> = {}) {
  const actions = {
    onSelect: vi.fn(), onDerive: vi.fn(), onOverride: vi.fn(), onNew: vi.fn(), onDelete: vi.fn(),
  };
  render(
    <PropositionExplorer entries={ENTRIES} selected={null} actions={actions} {...overrides} />,
  );
  return actions;
}

describe('PropositionExplorer', () => {
  it('renders every proposition as a leaf', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /is-active/ })).toBeTruthy();
    expect(screen.getByRole('treeitem', { name: /is-large/ })).toBeTruthy();
  });

  it('groups leaves under their namespace', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /^customer/ })).toBeTruthy();
    expect(screen.getByRole('treeitem', { name: /^order/ })).toBeTruthy();
  });

  it('filters as you type, matching the full dotted path', async () => {
    renderExplorer();

    await userEvent.type(screen.getByRole('searchbox', { name: /filter/i }), 'fraud');

    expect(screen.queryByRole('treeitem', { name: /is-fraudulent/ })).toBeTruthy();
    expect(screen.queryByRole('treeitem', { name: /is-adult/ })).toBeNull();
  });

  it('reports how many propositions match', async () => {
    renderExplorer();

    await userEvent.type(screen.getByRole('searchbox', { name: /filter/i }), 'eligibility');

    expect(screen.getByText(/2 of 4/)).toBeTruthy();
  });

  it('narrows to one model when a chip is toggled', async () => {
    renderExplorer();

    await userEvent.click(screen.getByRole('button', { name: 'order' }));

    expect(screen.queryByRole('treeitem', { name: /is-large/ })).toBeTruthy();
    expect(screen.queryByRole('treeitem', { name: /is-adult/ })).toBeNull();
  });

  it('selects a proposition when its leaf is clicked', async () => {
    const actions = renderExplorer();

    await userEvent.click(screen.getByRole('treeitem', { name: /is-fraudulent/ }));

    expect(actions.onSelect).toHaveBeenCalledWith('customer.risk.is-fraudulent');
  });

  it('does not select a namespace that holds no proposition', async () => {
    const actions = renderExplorer();

    await userEvent.click(screen.getByRole('treeitem', { name: /^customer/ }));

    expect(actions.onSelect).not.toHaveBeenCalled();
  });

  it('marks the selected leaf', () => {
    renderExplorer({ selected: 'customer.risk.is-fraudulent' });

    expect(screen.getByRole('treeitem', { name: /is-fraudulent/ }).getAttribute('aria-selected'))
      .toBe('true');
  });

  it('badges an origin on each leaf', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /is-active/ }).textContent).toContain('compiled');
    expect(screen.getByRole('treeitem', { name: /is-adult/ }).textContent).toContain('authored');
  });

  it('shows the model type as a pill', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /is-large/ }).textContent).toContain('order');
  });

  it('marks a quarantined proposition and shows why', () => {
    renderExplorer({
      entries: [entry({
        name: 'customer.broken',
        quarantine: [{ path: '$', code: 'UnknownSpec', message: 'unknown spec' }],
      })],
    });

    const leaf = screen.getByRole('treeitem', { name: /broken/ });
    expect(leaf.textContent).toContain('quarantined');
    expect(leaf.getAttribute('title')).toContain('unknown spec');
  });

  it('keeps quarantine distinct from origin', () => {
    // Quarantine is orthogonal, not a fourth origin — both marks must show
    renderExplorer({
      entries: [entry({
        name: 'customer.eligibility.is-active',
        origin: 'Overridden',
        quarantine: [{ path: '$', code: 'UnknownSpec', message: 'gone' }],
      })],
    });

    const leaf = screen.getByRole('treeitem', { name: /is-active/ });
    expect(leaf.textContent).toContain('overridden');
    expect(leaf.textContent).toContain('quarantined');
  });

  it('derives from a leaf', async () => {
    const actions = renderExplorer({ selected: 'customer.risk.is-fraudulent' });

    await userEvent.click(screen.getByRole('button', { name: /derive/i }));

    expect(actions.onDerive).toHaveBeenCalledWith('customer.risk.is-fraudulent');
  });

  it('starts a new proposition', async () => {
    const actions = renderExplorer();

    await userEvent.click(screen.getByRole('button', { name: /^new/i }));

    expect(actions.onNew).toHaveBeenCalled();
  });

  it('says so when nothing matches', async () => {
    renderExplorer();

    await userEvent.type(screen.getByRole('searchbox', { name: /filter/i }), 'zzz');

    expect(screen.getByText(/no propositions match/i)).toBeTruthy();
  });

  it('does not let a click on an unselectable middle row bubble into a selectable ancestor', async () => {
    // customer (dual role: namespace AND proposition) > customer.risk (bare namespace, no entry
    // of its own) > customer.risk.is-fraudulent (leaf). Clicking the *middle* row must not select
    // the ancestor — an unhandled click on a bare namespace must not bubble into whichever
    // ancestor happens to have a handler.
    const actions = renderExplorer({
      entries: [
        entry({ name: 'customer', modelType: 'customer' }),
        entry({ name: 'customer.risk.is-fraudulent' }),
      ],
    });

    await userEvent.click(screen.getByRole('treeitem', { name: /^risk/ }));

    expect(actions.onSelect).not.toHaveBeenCalled();
  });

  it('composes the accessible name from segment, origin and quarantine state', () => {
    // aria-label overrides content in ARIA name computation, so origin/quarantine — the badges
    // that are this task's whole deliverable — must be composed into the name explicitly, or an
    // assistive-tech user gets none of it.
    renderExplorer({
      entries: [entry({
        name: 'customer.eligibility.is-active',
        origin: 'Overridden',
        quarantine: [{ path: '$', code: 'UnknownSpec', message: 'gone' }],
      })],
    });

    const leaf = screen.getByRole('treeitem', { name: /is-active/ });
    expect(leaf.getAttribute('aria-label')).toContain('overridden');
    expect(leaf.getAttribute('aria-label')).toContain('quarantined');
  });

  it('activates a focused leaf via Enter', () => {
    const actions = renderExplorer();
    const leaf = screen.getByRole('treeitem', { name: /is-fraudulent/ });

    leaf.focus();
    fireEvent.keyDown(leaf, { key: 'Enter' });

    expect(actions.onSelect).toHaveBeenCalledWith('customer.risk.is-fraudulent');
  });

  it('activates a focused leaf via Space, and prevents the page from scrolling', () => {
    const actions = renderExplorer();
    const leaf = screen.getByRole('treeitem', { name: /is-fraudulent/ });
    leaf.focus();

    // fireEvent returns false exactly when preventDefault() was called on the dispatched event.
    const notPrevented = fireEvent.keyDown(leaf, { key: ' ' });

    expect(actions.onSelect).toHaveBeenCalledWith('customer.risk.is-fraudulent');
    expect(notPrevented).toBe(false);
  });

  it('keeps a bare namespace out of the tab order', () => {
    renderExplorer();

    const namespaceNode = screen.getByRole('treeitem', { name: /^customer/ });
    expect(namespaceNode.getAttribute('tabindex')).toBeNull();
  });

  it('puts a selectable leaf in the tab order', () => {
    renderExplorer();

    const leaf = screen.getByRole('treeitem', { name: /is-fraudulent/ });
    expect(leaf.getAttribute('tabindex')).toBe('0');
  });

  it('offers Override for a compiled entry whose model has a sibling spec', () => {
    renderExplorer({ selected: 'customer.eligibility.is-active' });

    expect(screen.getByRole('button', { name: /^override/i })).toBeTruthy();
  });

  it('does not offer Override for a compiled entry that is the only spec of its model', () => {
    renderExplorer({
      entries: [entry({ name: 'order.is-large', modelType: 'order', origin: 'Compiled', version: 0 })],
      selected: 'order.is-large',
    });

    expect(screen.queryByRole('button', { name: /^override/i })).toBeNull();
  });

  it('calls onOverride with the selected name', async () => {
    const actions = renderExplorer({ selected: 'customer.eligibility.is-active' });

    await userEvent.click(screen.getByRole('button', { name: /^override/i }));

    expect(actions.onOverride).toHaveBeenCalledWith('customer.eligibility.is-active');
  });
});
