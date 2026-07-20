import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = {
  specs: [{ name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: null }],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

describe('extension points', () => {
  it('shows expression and parameters as disabled affordances', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-adult' } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule');

    const expr = screen.getByRole('button', { name: /expression .*coming/i }) as HTMLButtonElement;
    expect(expr.disabled).toBe(true);

    const params = screen.getByRole('button', { name: /parameters .*coming/i }) as HTMLButtonElement;
    expect(params.disabled).toBe(true);
  });
});
