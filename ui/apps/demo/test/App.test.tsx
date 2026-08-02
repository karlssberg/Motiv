import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore, RulesApiClient } from '@motiv/rules-core';
import { App } from '../src/App.js';

function testClient(): RulesApiClient {
  return {
    getCatalog: vi.fn().mockResolvedValue({ specs: [], collections: [] }),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    evaluate: vi.fn(),
    listRules: vi.fn().mockResolvedValue([]),
    listPropositions: vi.fn().mockResolvedValue([]),
    getProposition: vi.fn().mockResolvedValue({
      document: null, version: 0, origin: 'Compiled', hasCompiledDefault: true,
    }),
    getDependents: vi.fn().mockResolvedValue([]),
  } as unknown as RulesApiClient;
}

function renderApp() {
  return render(
    <App client={testClient()} store={new RuleEditorStore({ rule: { spec: 'is-active' } })} />,
  );
}

describe('App', () => {
  beforeEach(() => {
    window.location.hash = '';
  });

  it('renders the three panes', () => {
    renderApp();
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

  it('shows the rules page by default', async () => {
    renderApp();

    expect(await screen.findByRole('tab', { name: 'Rules', selected: true })).toBeTruthy();
  });

  it('switches page when a tab is clicked', async () => {
    renderApp();

    await userEvent.click(await screen.findByRole('tab', { name: 'Propositions' }));

    expect(window.location.hash).toBe('#/propositions');
    expect(await screen.findByRole('tab', { name: 'Propositions', selected: true })).toBeTruthy();
  });

  it('opens straight onto the propositions page from a deep link', async () => {
    window.location.hash = '#/propositions/customer.is-active';
    renderApp();

    expect(await screen.findByRole('complementary', { name: 'Propositions' })).toBeTruthy();
  });
});
