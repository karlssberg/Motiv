import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RulesApiError, type PropositionListEntry } from '@motiv-rules/core';
import { PropositionDocument } from '../../src/panes/PropositionDocument.js';
import { Workspace } from '../../src/shell/workspace.js';

function entry(overrides: Partial<PropositionListEntry> & { name: string }): PropositionListEntry {
  return {
    modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Authored', version: 1, description: null, quarantine: [],
    ...overrides,
  };
}

/** What GET returns for a name served only by a compiled spec: no document, and version 0. */
const COMPILED = { document: null, version: 0, origin: 'Compiled', hasCompiledDefault: true };

function stubClient(overrides: Record<string, unknown> = {}) {
  return {
    listPropositions: vi.fn().mockResolvedValue([
      entry({ name: 'customer.is-active', origin: 'Compiled', version: 0 }),
      entry({ name: 'customer.derived' }),
      entry({ name: 'order.large', modelType: 'order' }),
    ]),
    getProposition: vi.fn().mockResolvedValue({
      document: { rule: { spec: 'customer.is-active' } }, version: 1,
      origin: 'Authored', hasCompiledDefault: false,
    }),
    getDependents: vi.fn().mockResolvedValue([]),
    putProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 2 }),
    getCatalog: vi.fn().mockResolvedValue({ specs: [], collections: [], metadataTypes: {}, modelTypes: {} }),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    ...overrides,
  };
}

function renderDocument(name = 'customer.derived', client: ReturnType<typeof stubClient> = stubClient()) {
  const workspace = new Workspace();
  const tab = workspace.open('proposition', name);
  const onClose = vi.fn();
  render(<PropositionDocument client={client as never} tab={tab} workspace={workspace} onClose={onClose} />);
  return { workspace, tab, store: tab.store, onClose, client };
}

describe('PropositionDocument', () => {
  it('loads the proposition into the tab’s own store', async () => {
    const { client, store } = renderDocument();
    await waitFor(() => expect(client.getProposition).toHaveBeenCalledWith('customer.derived'));
    await waitFor(() => expect(store.getState().document).toEqual({ rule: { spec: 'customer.is-active' } }));
    expect(store.getState().dirty).toBe(false);
  });

  it('shows the name as breadcrumb segments in the editor’s header', async () => {
    renderDocument();
    const editor = await screen.findByRole('region', { name: 'Editor' });
    await waitFor(() => expect(editor.querySelector('.breadcrumb-current')?.textContent).toBe('derived'));
    expect([...editor.querySelectorAll('.breadcrumb-item')].map((node) => node.textContent)).toContain('customer');
  });

  it('validates against the listing’s model type for the name', async () => {
    const { client, store } = renderDocument('order.large');
    await waitFor(() => expect(client.listPropositions).toHaveBeenCalled());
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());
    store.replaceNode('$.rule', { not: { spec: 'order.large' } });
    await waitFor(() => expect(client.validate).toHaveBeenCalledWith(expect.objectContaining({ modelType: 'order' })), { timeout: 2000 });
  });

  it('fetches the blast radius and says how many things an edit would affect', async () => {
    const client = stubClient({
      getDependents: vi.fn().mockResolvedValue([
        { name: 'can-checkout', kind: 'rule' },
        { name: 'customer.other', kind: 'proposition' },
      ]),
    });
    renderDocument('customer.derived', client);

    expect(await screen.findByText(/1 rule and 1 proposition/i)).toBeTruthy();
    expect(await screen.findByRole('button', { name: 'Save (2)' })).toBeTruthy();
  });

  it('saves the edited document with the loaded version, and tells the workspace', async () => {
    const { client, workspace } = renderDocument();
    await screen.findByText('v1');

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => expect(client.putProposition).toHaveBeenCalledWith('customer.derived', expect.anything(), 1));
    await waitFor(() => expect(workspace.getState().latest['customer.derived']).toMatchObject({ version: 2, kind: 'proposition' }));
  });

  it('saves and closes in one step, only once the save landed', async () => {
    const { client, onClose } = renderDocument();
    await screen.findByText('v1');

    await userEvent.click(screen.getByRole('button', { name: 'Save options' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Save & close/ }));

    await waitFor(() => expect(client.putProposition).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
  });

  it('explains why Save is unavailable for a name only served by a compiled spec', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    renderDocument('customer.is-active', client);
    await screen.findByText('v0');

    const save = screen.getByRole('button', { name: 'Save' });
    expect(save.getAttribute('aria-disabled')).toBe('true');
    expect(document.getElementById(save.getAttribute('aria-describedby')!)?.textContent).toMatch(/compiled/i);
  });

  it('surfaces a thrown listing failure', async () => {
    const client = stubClient({
      listPropositions: vi.fn().mockRejectedValue(new RulesApiError(500, 'listing exploded')),
    });
    renderDocument('customer.derived', client);
    expect((await screen.findByRole('alert')).textContent).toContain('listing exploded');
  });

  it('surfaces a thrown load failure', async () => {
    const client = stubClient({
      getProposition: vi.fn().mockRejectedValue(new RulesApiError(404, 'No proposition named customer.gone.')),
    });
    renderDocument('customer.gone', client);
    expect((await screen.findByRole('alert')).textContent).toContain('No proposition named');
  });

  it('surfaces a thrown save failure rather than leaving Save silently dead', async () => {
    const client = stubClient({
      putProposition: vi.fn().mockRejectedValue(new RulesApiError(500, 'save exploded')),
    });
    renderDocument('customer.derived', client);
    await screen.findByText('v1');

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    expect((await screen.findByRole('alert')).textContent).toContain('save exploded');
    expect(screen.getByRole('button', { name: /^save$/i }).getAttribute('aria-disabled')).toBeNull();
  });

  it('surfaces a conflict when the version was stale', async () => {
    const client = stubClient({
      putProposition: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 5 }),
    });
    renderDocument('customer.derived', client);
    await screen.findByText('v1');

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    expect((await screen.findByRole('alert')).textContent).toMatch(/version 5/);
  });

  it('opens the document viewer from the header', async () => {
    renderDocument();
    await screen.findByText('v1');
    await userEvent.click(screen.getByRole('button', { name: 'JSON' }));
    expect(screen.getByRole('dialog', { name: /document/i })).toBeTruthy();
  });
});
