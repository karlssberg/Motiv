import { describe, it, expect, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RuleEditorStore, RulesApiClient } from '@motiv/rules-core';
import { App } from '../src/App.js';

function testClient(): RulesApiClient {
  return {
    getCatalog: vi.fn().mockResolvedValue({ specs: [], collections: [] }),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    evaluate: vi.fn(),
    listRules: vi.fn().mockResolvedValue([]),
  } as unknown as RulesApiClient;
}

describe('App', () => {
  it('renders the three panes', () => {
    render(<App client={testClient()} store={new RuleEditorStore({ rule: { spec: 'is-active' } })} />);
    expect(screen.getByRole('region', { name: 'Editor' })).toBeDefined();
    expect(screen.getByRole('region', { name: 'Document' })).toBeDefined();
    expect(screen.getByRole('region', { name: 'Evaluate' })).toBeDefined();
  });

  it('validates with isAsync after an async rule is loaded', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const client = {
      ...testClient(),
      listRules: vi.fn().mockResolvedValue([{
        name: 'fraud-screening',
        modelType: 'customer',
        metadataType: 'String',
        isAsync: true,
        isPolicy: false,
        version: 1,
        description: 'Screening',
      }]),
      getRule: vi.fn().mockResolvedValue({
        document: { rule: { spec: 'passes-credit-check' } },
        version: 1,
      }),
    } as unknown as RulesApiClient;
    render(<App client={client} store={store} />);

    // Pick the async rule from the breadcrumb's leaf listbox (click leaf, then the option).
    fireEvent.click(await screen.findByRole('combobox', { name: /^rule,/ }));
    fireEvent.click(await screen.findByRole('option', { name: 'fraud-screening' }));
    await waitFor(() =>
      expect(store.getState().document).toEqual({ rule: { spec: 'passes-credit-check' } }));

    store.replaceNode('$.rule', { not: { spec: 'passes-credit-check' } });

    // The 300ms debounce fires with real timers; poll until the async-flagged call lands.
    await waitFor(() => expect(client.validate).toHaveBeenCalledWith({
      modelType: 'customer',
      document: { rule: { not: { spec: 'passes-credit-check' } } },
      isAsync: true,
    }), { timeout: 2000 });
  });
});
