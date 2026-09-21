import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { RulesApiClient } from '@motiv-rules/core';
import { App } from '../src/App.js';

// jsdom lays nothing out, so the tab strip would measure a width of zero and show one chip; give
// it room for four, so every open tab is a `tab` rather than an entry in the overflow menu.
Object.defineProperty(HTMLElement.prototype, 'clientWidth', { configurable: true, get: () => 800 });

function testClient(): RulesApiClient {
  return {
    getCatalog: vi.fn().mockResolvedValue({ specs: [], collections: [] }),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    evaluate: vi.fn(),
    listRules: vi.fn().mockResolvedValue([{
      name: 'can-checkout', modelType: 'customer', metadataType: 'String',
      isAsync: false, isPolicy: false, version: 1, description: null,
    }]),
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'is-adult' } }, version: 1 }),
    listPropositions: vi.fn().mockResolvedValue([{
      name: 'customer.is-active', modelType: 'customer', metadataType: 'String',
      isAsync: false, origin: 'Compiled', version: 0, description: null, quarantine: [],
    }]),
    getProposition: vi.fn().mockResolvedValue({
      document: null, version: 0, origin: 'Compiled', hasCompiledDefault: true,
    }),
    getDependents: vi.fn().mockResolvedValue([]),
    listScenarios: vi.fn().mockResolvedValue([]),
  } as unknown as RulesApiClient;
}

function renderApp() {
  const client = testClient();
  render(<App client={client} />);
  return client;
}

describe('App', () => {
  beforeEach(() => {
    window.location.hash = '';
    window.sessionStorage.clear();
  });

  it('opens the rule in the route as a tab, with the editor and evaluate panes and the document behind JSON', async () => {
    window.location.hash = '#/rules/can-checkout';
    renderApp();
    expect(await screen.findByRole('tab', { name: 'Rule can-checkout' })).toBeTruthy();
    expect(await screen.findByRole('region', { name: 'Editor' })).toBeDefined();
    expect(screen.getByRole('region', { name: 'Evaluate' })).toBeDefined();
    expect(screen.queryByRole('region', { name: 'Document' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'JSON' }));
    expect(screen.getByRole('dialog', { name: /document/i })).toBeDefined();
  });

  it('lands on one empty tab, showing the catalog, with no document in the route', async () => {
    renderApp();
    expect(await screen.findByRole('tabpanel', { name: 'Catalog' })).toBeTruthy();
    const list = screen.getByRole('tablist', { name: 'Open documents' });
    expect(within(list).getAllByRole('tab').map((tab) => tab.getAttribute('aria-label'))).toEqual(['Catalog']);
    expect(within(list).getByRole('tab', { name: 'Catalog' }).getAttribute('aria-selected')).toBe('true');
  });

  it('opens a proposition from a deep link, and a second route is a second tab', async () => {
    window.location.hash = '#/propositions/customer.is-active';
    const client = renderApp();
    await waitFor(() => expect(client.getProposition).toHaveBeenCalledWith('customer.is-active'));
    expect(await screen.findByRole('tab', { name: 'Proposition customer.is-active', selected: true })).toBeTruthy();

    window.location.hash = '#/rules/can-checkout';
    expect(await screen.findByRole('tab', { name: 'Rule can-checkout', selected: true })).toBeTruthy();
    expect(screen.getByRole('tab', { name: 'Proposition customer.is-active', selected: false })).toBeTruthy();
  });

  it('activating a tab writes its route, and closing the active tab moves to its neighbour', async () => {
    window.location.hash = '#/propositions/customer.is-active';
    renderApp();
    await screen.findByRole('tab', { name: 'Proposition customer.is-active' });
    window.location.hash = '#/rules/can-checkout';
    await screen.findByRole('tab', { name: 'Rule can-checkout', selected: true });

    await userEvent.click(screen.getByRole('tab', { name: 'Proposition customer.is-active' }));
    await waitFor(() => expect(window.location.hash).toBe('#/propositions/customer.is-active'));

    await userEvent.click(screen.getByLabelText('Close customer.is-active'));
    await waitFor(() => expect(window.location.hash).toBe('#/rules/can-checkout'));
    expect(screen.queryByRole('tab', { name: 'Proposition customer.is-active' })).toBeNull();
  });

  it('closing the last tab lands on the bare page', async () => {
    window.location.hash = '#/rules/can-checkout';
    renderApp();
    await screen.findByRole('tab', { name: 'Rule can-checkout' });
    await userEvent.click(screen.getByLabelText('Close can-checkout'));
    await waitFor(() => expect(window.location.hash).toBe('#/rules'));
    expect(await screen.findByRole('tabpanel', { name: 'Catalog' })).toBeTruthy();
  });

  it('keeps the tabs across a reload, with the route saying which is in front', async () => {
    window.location.hash = '#/rules/can-checkout';
    const { unmount } = render(<App client={testClient()} />);
    await screen.findByRole('tab', { name: 'Rule can-checkout' });
    window.location.hash = '#/propositions/customer.is-active';
    await screen.findByRole('tab', { name: 'Proposition customer.is-active', selected: true });
    unmount();

    renderApp();
    const list = await screen.findByRole('tablist', { name: 'Open documents' });
    expect(within(list).getAllByRole('tab').map((tab) => tab.getAttribute('aria-label')))
      .toEqual(['Rule can-checkout', 'Proposition customer.is-active']);
    expect(within(list).getByRole('tab', { name: 'Proposition customer.is-active' }).getAttribute('aria-selected')).toBe('true');
  });

  it('shows the admin page on its route', async () => {
    window.location.hash = '#/admin';
    renderApp();
    expect(await screen.findByRole('link', { name: 'Documents' })).toBeTruthy();
    expect(screen.queryByRole('tablist')).toBeNull();
  });
});
