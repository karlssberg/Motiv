import { describe, it, expect, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { RuleListEntry, RulesApiClient } from '@motiv-rules/core';
import { RuleDocument } from '../../src/panes/RuleDocument.js';
import { Workspace, tabIdOf } from '../../src/shell/workspace.js';

const entries: RuleListEntry[] = [
  {
    name: 'can-checkout', modelType: 'customer', metadataType: 'String',
    isAsync: false, isPolicy: false, version: 1, description: 'Gate',
  },
  {
    name: 'fraud-screening', modelType: 'customer', metadataType: 'String',
    // Async on purpose: validation for this tab has to switch into async mode.
    isAsync: true, isPolicy: false, version: 1, description: 'Screening',
  },
];

function makeClient(overrides: Partial<Record<string, unknown>> = {}): RulesApiClient {
  return {
    listRules: vi.fn().mockResolvedValue(entries),
    // Deliberately *not* the seed the store starts on, so "loads into the store" cannot pass vacuously.
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'is-adult' } }, version: 3 }),
    putRule: vi.fn().mockResolvedValue({ outcome: 'updated', version: 4 }),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    // The panes each fetch the catalog on mount; an empty one is enough for them to render.
    getCatalog: vi.fn().mockResolvedValue({ specs: [], collections: [] }),
    ...overrides,
  } as unknown as RulesApiClient;
}

function renderDocument(name = 'can-checkout', client: RulesApiClient = makeClient()) {
  const workspace = new Workspace();
  const tab = workspace.open('rule', name);
  const onClose = vi.fn();
  const onOpenProposition = vi.fn();
  render(
    <RuleDocument client={client} tab={tab} workspace={workspace} onClose={onClose} onOpenProposition={onOpenProposition} />,
  );
  return { workspace, tab, store: tab.store, onClose, onOpenProposition, client };
}

describe('RuleDocument', () => {
  it('loads the rule into the tab’s own store, and wears its name and version', async () => {
    const { store, client } = renderDocument();
    await waitFor(() => expect(client.getRule).toHaveBeenCalledWith('can-checkout'));
    await waitFor(() => expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } }));
    expect(store.getState().dirty).toBe(false);
    const editor = screen.getByRole('region', { name: 'Editor' });
    expect(editor.textContent).toContain('can-checkout');
    expect(await screen.findByText(/^v3\b/)).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Evaluate' })).toBeTruthy();
    expect(screen.queryByRole('region', { name: 'Checkout' })).toBeNull();
  });

  it('draws JSON and Save in the editor’s header, after the surface tabs', async () => {
    renderDocument();
    await screen.findByText(/^v3\b/);
    const editor = screen.getByRole('region', { name: 'Editor' });
    const header = editor.querySelector('.pane-header')!;
    const order = [...header.querySelectorAll('[role="tab"], button')].map((el) => el.textContent || el.getAttribute('aria-label'));
    expect(order.indexOf('DSL')).toBeLessThan(order.indexOf('JSON'));
    expect(order.indexOf('JSON')).toBeLessThan(order.findIndex((label) => label?.startsWith('Save')));
    expect(screen.queryByRole('button', { name: 'Open' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Close' })).toBeNull();
  });

  it('shows a code-default note when the server document is null', async () => {
    const client = makeClient({ getRule: vi.fn().mockResolvedValue({ document: null, version: 1 }) });
    renderDocument('can-checkout', client);
    expect(await screen.findByText(/code-defined default/i)).toBeDefined();
  });

  it('saves with the loaded version, shows the new one, and tells the workspace', async () => {
    const { workspace, client } = renderDocument();
    await screen.findByText(/^v3\b/);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() =>
      expect(client.putRule).toHaveBeenCalledWith('can-checkout', { rule: { spec: 'is-adult' } }, 3));
    expect(await screen.findByText(/^v4\b/)).toBeDefined();
    await waitFor(() => expect(workspace.getState().latest['can-checkout']).toMatchObject({ version: 4, kind: 'rule' }));
  });

  it('does not report the initial load as a save', async () => {
    const { workspace } = renderDocument();
    await screen.findByText(/^v3\b/);
    expect(workspace.getState().latest['can-checkout']).toBeUndefined();
  });

  it('saves and closes in one step from the split button, only after the save landed', async () => {
    const { onClose, client } = renderDocument();
    await screen.findByText(/^v3\b/);

    await userEvent.click(screen.getByRole('button', { name: 'Save options' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Save & close/ }));

    await waitFor(() => expect(client.putRule).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
  });

  it('validates with isAsync once the loaded rule is async', async () => {
    const client = makeClient({
      getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'passes-credit-check' } }, version: 1 }),
    });
    const { store } = renderDocument('fraud-screening', client);
    await waitFor(() => expect(store.getState().document).toEqual({ rule: { spec: 'passes-credit-check' } }));
    // The listing has to have landed too, since that is where `isAsync` comes from.
    await waitFor(() => expect(client.listRules).toHaveBeenCalled());

    act(() => store.replaceNode('$.rule', { not: { spec: 'passes-credit-check' } }));

    // The 300ms debounce fires with real timers; poll until the async-flagged call lands.
    await waitFor(() => expect(client.validate).toHaveBeenCalledWith({
      modelType: 'customer',
      document: { rule: { not: { spec: 'passes-credit-check' } } },
      isAsync: true,
    }), { timeout: 2000 });
  });

  it('pushes validation errors from an invalid save into the store', async () => {
    const errors = [{ path: '$.rule', code: 'PolicyRequired', message: 'the rule must be a policy' }];
    const client = makeClient({ putRule: vi.fn().mockResolvedValue({ outcome: 'invalid', errors }) });
    const { store } = renderDocument('can-checkout', client);
    await screen.findByText(/^v3\b/);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(store.getState().errors).toEqual(errors));
  });

  it('shows a conflict banner with a reload action on version conflicts', async () => {
    const client = makeClient({ putRule: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 9 }) });
    renderDocument('can-checkout', client);
    await screen.findByText(/^v3\b/);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toBeDefined();
    expect(screen.getByText(/someone else saved version 9/i)).toBeDefined();

    await userEvent.click(screen.getByRole('button', { name: /reload latest/i }));
    await waitFor(() => expect(client.getRule).toHaveBeenCalledTimes(2));
  });

  it('reports a failed listing fetch, which nothing else would say', async () => {
    const client = makeClient({ listRules: vi.fn().mockRejectedValue(new Error('Rules service unavailable (503)')) });
    renderDocument('can-checkout', client);

    expect(await screen.findByRole('alert')).toBeDefined();
    expect(screen.getByText(/rules service unavailable \(503\)/i)).toBeDefined();
  });

  it('reports a thrown save, and offers the loaded rule as the way back', async () => {
    const client = makeClient({ putRule: vi.fn().mockRejectedValue(new Error('Rules service unavailable (503)')) });
    renderDocument('can-checkout', client);
    await screen.findByText(/^v3\b/);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText(/rules service unavailable \(503\)/i)).toBeDefined();
    await userEvent.click(screen.getByRole('button', { name: /reload latest/i }));
    await waitFor(() => expect(client.getRule).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
  });

  it('opens the document viewer from the header', async () => {
    renderDocument();
    await screen.findByText(/^v3\b/);
    await userEvent.click(screen.getByRole('button', { name: 'JSON' }));
    expect(screen.getByRole('dialog', { name: /document/i })).toBeTruthy();
  });

  it('lists the propositions the rule uses, and opens one on request', async () => {
    const { onOpenProposition } = renderDocument();
    await screen.findByText(/^v3\b/);
    const uses = screen.getByRole('group', { name: 'Uses' });
    await userEvent.click(screen.getByRole('button', { name: /is-adult/ }));
    expect(uses).toBeTruthy();
    expect(onOpenProposition).toHaveBeenCalledWith('is-adult');
  });

  it('marks a used proposition as editing while another tab holds it dirty, and updated once saved', async () => {
    const { workspace, tab } = renderDocument();
    await screen.findByText(/^v3\b/);
    const other = workspace.open('proposition', 'is-adult', { activate: false });
    expect(screen.queryByText('editing')).toBeNull();

    act(() => other.store.replaceNode('$.rule', { spec: 'changed' }));
    expect(await screen.findByText('editing')).toBeTruthy();

    act(() => { other.store.markClean(); });
    // Stamped one tick after the tab opened: newer than the tab looked, older than the click below.
    const now = vi.spyOn(Date, 'now').mockReturnValue(tab.seenAt + 1);
    try {
      act(() => workspace.noteSaved('proposition', 'is-adult', 2));
    } finally {
      now.mockRestore();
    }
    expect(await screen.findByText('updated')).toBeTruthy();
    expect(screen.getByText('v2')).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'Got it' }));
    await waitFor(() => expect(screen.queryByText('updated')).toBeNull());
    expect(workspace.getState().docs[tabIdOf('rule', 'can-checkout')]).toBeTruthy();
  });
});
