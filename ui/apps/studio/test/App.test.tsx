import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore, RulesApiClient } from '@motiv-rules/core';
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
  const client = testClient();
  render(<App client={client} store={new RuleEditorStore({ rule: { spec: 'is-active' } })} />);
  return client;
}

describe('App', () => {
  beforeEach(() => {
    window.location.hash = '';
  });

  it('renders the editor and evaluate panes, with the document behind the toolbar', async () => {
    renderApp();
    expect(screen.getByRole('region', { name: 'Editor' })).toBeDefined();
    expect(screen.getByRole('region', { name: 'Evaluate' })).toBeDefined();
    // The JSON pane retired in favour of a modal reached from the toolbar — see DocumentModal.
    expect(screen.queryByRole('region', { name: 'Document' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'JSON' }));
    expect(screen.getByRole('dialog', { name: /document/i })).toBeDefined();
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

    // Pick the async rule from the palette: open it from the toolbar, narrow to the one rule,
    // and choose it with Enter — the path the shell now offers in place of the breadcrumb listbox.
    await userEvent.click(screen.getByRole('button', { name: 'Open' }));
    const palette = await screen.findByRole('dialog', { name: 'Rules' });
    await userEvent.type(within(palette).getByRole('combobox'), 'fraud-screening');
    await userEvent.keyboard('{Enter}');
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

    expect(await screen.findByRole('link', { name: 'Rules', current: 'page' })).toBeTruthy();
  });

  it('switches page when a nav link is followed', async () => {
    // Following the link is the whole navigation: the hash it carries is what the router reads
    // back, so nothing in the shell has to arrange for the page to change.
    renderApp();

    await userEvent.click(await screen.findByRole('link', { name: 'Propositions' }));

    expect(window.location.hash).toBe('#/propositions');
    expect(await screen.findByRole('link', { name: 'Propositions', current: 'page' })).toBeTruthy();
  });

  it('opens straight onto the propositions page from a deep link', async () => {
    window.location.hash = '#/propositions/customer.is-active';
    const client = renderApp();

    // Both halves of the hash in one assertion, and deliberately: only the propositions page
    // fetches a proposition, and it fetches the one the name names. What stood here before was the
    // toolbar's Open button — which `RuleHeader` mints identically, so the assertion held on either
    // page. Narrowing the route parser to send every hash to the rules page, or dropping the name
    // from the parse, both left it green.
    await waitFor(() => expect(client.getProposition).toHaveBeenCalledWith('customer.is-active'));
    expect(await screen.findByRole('link', { name: 'Propositions', current: 'page' })).toBeTruthy();
  });
});
