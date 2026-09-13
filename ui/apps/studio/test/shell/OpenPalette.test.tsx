import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PropositionListEntry, RuleListEntry } from '@motiv-rules/core';
import { OpenPalette } from '../../src/shell/OpenPalette.js';
import { tabIdOf } from '../../src/shell/workspace.js';

const rules: RuleListEntry[] = [
  { name: 'can-checkout', modelType: 'customer', metadataType: 'String', isAsync: false, isPolicy: false, version: 1, description: null },
];
const propositions: PropositionListEntry[] = [
  { name: 'customer.is-active', modelType: 'customer', metadataType: 'String', isAsync: false, origin: 'Compiled', version: 0, description: null, quarantine: [] },
  { name: 'customer.derived', modelType: 'customer', metadataType: 'String', isAsync: false, origin: 'Authored', version: 1, description: null, quarantine: [] },
];

function renderPalette(open: string[] = []) {
  const handlers = { onChoose: vi.fn(), onManage: vi.fn(), onClose: vi.fn() };
  render(<OpenPalette rules={rules} propositions={propositions} open={new Set(open)} {...handlers} />);
  return handlers;
}

describe('OpenPalette', () => {
  it('lists rules and propositions together, each row saying its kind', () => {
    renderPalette();
    const dialog = screen.getByRole('dialog', { name: 'Open' });
    const rows = within(dialog).getAllByRole('option');
    expect(rows.map((row) => row.textContent)).toEqual([
      'can-checkoutRule', 'customer.is-activeProposition', 'customer.derivedProposition',
    ]);
  });

  it('marks the ones already open', () => {
    renderPalette([tabIdOf('proposition', 'customer.derived')]);
    const row = screen.getByRole('option', { name: /customer\.derived/ });
    expect(row.textContent).toContain('open');
    expect(screen.getByRole('option', { name: /can-checkout/ }).textContent).not.toContain('open');
  });

  it('filters by name or kind, and chooses on Enter', async () => {
    const { onChoose, onClose } = renderPalette();
    await userEvent.type(screen.getByRole('combobox'), 'prop');
    expect(screen.getAllByRole('option')).toHaveLength(2);
    await userEvent.clear(screen.getByRole('combobox'));
    await userEvent.type(screen.getByRole('combobox'), 'derived');
    await userEvent.keyboard('{Enter}');
    expect(onChoose).toHaveBeenCalledWith('proposition', 'customer.derived');
    expect(onClose).toHaveBeenCalled();
  });

  it('hands over to the propositions explorer from its footer', async () => {
    const { onManage } = renderPalette();
    await userEvent.click(screen.getByRole('button', { name: 'Manage propositions' }));
    expect(onManage).toHaveBeenCalledTimes(1);
  });
});
