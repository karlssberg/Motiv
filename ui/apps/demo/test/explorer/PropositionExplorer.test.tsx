import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
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
  const actions = { onSelect: vi.fn(), onDerive: vi.fn(), onNew: vi.fn(), onDelete: vi.fn() };
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
});
