import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { RuleEditorStore, type CatalogEntry, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../src/BuilderPane.js';

const catalog: CatalogEntry[] = [
  { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'active' },
  { name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'adult' },
];

function client(): RulesApiClient {
  return { getCatalog: vi.fn().mockResolvedValue(catalog) } as unknown as RulesApiClient;
}

function renderWith(store: RuleEditorStore) {
  return render(
    <RuleEditorProvider store={store}>
      <BuilderPane client={client()} />
    </RuleEditorProvider>,
  );
}

describe('BuilderPane', () => {
  it('changes a leaf spec via the catalog select', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);

    const select = await screen.findByLabelText('spec at $.rule');
    fireEvent.change(select, { target: { value: 'is-adult' } });

    expect(store.getState().document.rule).toEqual({ spec: 'is-adult' });
  });

  it('wraps the root leaf in an AND operator', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);

    await screen.findByLabelText('spec at $.rule');
    fireEvent.click(screen.getByRole('button', { name: 'AND at $.rule' }));

    await waitFor(() => {
      const rule = store.getState().document.rule as { and?: unknown[] };
      expect(rule.and).toHaveLength(2);
    });
  });
});
