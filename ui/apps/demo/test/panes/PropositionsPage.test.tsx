import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore, type PropositionListEntry, type RuleDocument } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { PropositionsPage } from '../../src/panes/PropositionsPage.js';

function entry(overrides: Partial<PropositionListEntry> & { name: string }): PropositionListEntry {
  return {
    modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Authored', version: 1, description: null, quarantine: [],
    ...overrides,
  };
}

/** What GET returns for a name served only by a compiled spec: no document, and version 0. */
const COMPILED = {
  document: null, version: 0, origin: 'Compiled', hasCompiledDefault: true,
};

/** A client stubbed just far enough for the page: only the calls it actually makes. */
function stubClient(overrides: Record<string, unknown> = {}) {
  return {
    listPropositions: vi.fn().mockResolvedValue([
      entry({ name: 'customer.is-active', origin: 'Compiled', version: 0 }),
      entry({ name: 'customer.derived' }),
      entry({ name: 'customer.overridden', origin: 'Overridden', version: 2 }),
    ]),
    getProposition: vi.fn().mockResolvedValue({
      document: { rule: { spec: 'customer.is-active' } }, version: 1,
      origin: 'Authored', hasCompiledDefault: false,
    }),
    getDependents: vi.fn().mockResolvedValue([]),
    createProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 1 }),
    putProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 2 }),
    deleteProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 0 }),
    getCatalog: vi.fn().mockResolvedValue({ specs: [], collections: [], metadataTypes: {}, modelTypes: {} }),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    ...overrides,
  };
}

/** The values a `<select>` currently offers, in the order it offers them. */
function optionsOf(label: string | RegExp): string[] {
  return [...(screen.getByLabelText(label) as HTMLSelectElement).options].map((option) => option.value);
}

function renderPage(
  client: ReturnType<typeof stubClient>,
  selected: string | null = null,
  document: RuleDocument = { rule: { spec: 'customer.is-active' } },
) {
  const onSelect = vi.fn();
  const store = new RuleEditorStore(document);
  const page = (name: string | null) => (
    <RuleEditorProvider store={store}>
      <PropositionsPage
        client={client as never}
        page="propositions"
        selected={name}
        onNavigate={vi.fn()}
        onSelect={onSelect}
      />
    </RuleEditorProvider>
  );
  const { rerender } = render(page(selected));
  return { onSelect, select: (name: string | null) => rerender(page(name)) };
}

describe('PropositionsPage', () => {
  it('lists propositions in the explorer on mount', async () => {
    const client = stubClient();
    renderPage(client);

    expect(await screen.findByRole('treeitem', { name: /is-active/ })).toBeTruthy();
    expect(client.listPropositions).toHaveBeenCalled();
  });

  it('loads the selected proposition document', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');

    await waitFor(() => expect(client.getProposition).toHaveBeenCalledWith('customer.derived'));
  });

  it('shows the selected name as breadcrumb segments', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');

    // The dotted name renders as a trail, which is the payoff of namespacing by name.
    // Scoped to the banner: "customer" also appears as a model pill in the explorer, so an
    // unscoped findByText would match several nodes and throw.
    const bar = await screen.findByRole('banner');
    await waitFor(() => expect(bar.querySelector('.breadcrumb-current')?.textContent).toBe('derived'));
    expect([...bar.querySelectorAll('.breadcrumb-item')].map((node) => node.textContent))
      .toContain('customer');
  });

  it('fetches the blast radius for the selection', async () => {
    const client = stubClient({
      getDependents: vi.fn().mockResolvedValue([{ name: 'can-checkout', kind: 'rule' }]),
    });
    renderPage(client, 'customer.derived');

    expect(await screen.findByText(/can-checkout/)).toBeTruthy();
  });

  it('says how many things an edit would affect', async () => {
    const client = stubClient({
      getDependents: vi.fn().mockResolvedValue([
        { name: 'can-checkout', kind: 'rule' },
        { name: 'customer.other', kind: 'proposition' },
      ]),
    });
    renderPage(client, 'customer.derived');

    expect(await screen.findByText(/1 rule and 1 proposition/i)).toBeTruthy();
  });

  it('saves the edited document with the loaded version', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => expect(client.putProposition)
      .toHaveBeenCalledWith('customer.derived', expect.anything(), 1));
  });

  it('does not offer Save for a name that is only served by a compiled spec', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    renderPage(client, 'customer.is-active');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    // Version 0 is the contract's "purely compiled": there is no overlay document for a PUT to
    // update, and `baseVersion` is required to be positive, so saving could only ever fail.
    // Override is the affordance that authors one.
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /^save$/i }).hasAttribute('disabled')).toBe(true));
  });

  it('surfaces a conflict when the version was stale', async () => {
    const client = stubClient({
      putProposition: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 5 }),
    });
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    expect(await screen.findByRole('alert')).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toContain('5');
  });

  it('drops the previous selection’s blast radius and failure while the next one loads', async () => {
    const client = stubClient({
      getDependents: vi.fn().mockResolvedValue([{ name: 'can-checkout', kind: 'rule' }]),
      putProposition: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 5 }),
    });
    const { select } = renderPage(client, 'customer.derived');
    await screen.findByText(/can-checkout/);
    await userEvent.click(screen.getByRole('button', { name: /^save/i }));
    await screen.findByRole('alert');

    // Both the strip and the banner are claims about the proposition that was selected. Carried
    // across a change of selection they would be claims about the new one, and false.
    select('customer.overridden');

    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByText(/can-checkout/)).toBeNull();
  });

  it('names the rule an edit would break', async () => {
    const client = stubClient({
      putProposition: vi.fn().mockResolvedValue({
        outcome: 'invalid',
        errors: [],
        brokenDependents: [{
          name: 'can-checkout', kind: 'rule',
          errors: [{ path: '$', code: 'AsyncSpecInSyncLoad', message: 'would not bind' }],
        }],
      }),
    });
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('can-checkout');
    expect(alert.textContent).toContain('would not bind');
  });

  it('creates a proposition from the new dialog', async () => {
    const client = stubClient();
    // The store is shared with the Rules page, and on a freshly-opened Propositions page it still
    // holds that page's draft. A create that copied it would depend on which page was visited
    // first, so the fixture is deliberately a document no source in the listing could produce.
    renderPage(client, null, { rule: { spec: 'customer.has-orders' } });
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    // No source picked, so the create starts from the first one offered — not from the draft.
    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.fresh',
      modelType: 'customer',
      document: { rule: { spec: 'customer.derived' } },
    })));
  });

  it('starts a new proposition from whichever source is picked', async () => {
    const client = stubClient();
    renderPage(client, null, { rule: { spec: 'customer.has-orders' } });
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.selectOptions(screen.getByLabelText(/starts from/i), 'customer.overridden');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      document: { rule: { spec: 'customer.overridden' } },
    })));
  });

  it('offers only the sources belonging to the model type in force', async () => {
    const client = stubClient({
      listPropositions: vi.fn().mockResolvedValue([
        entry({ name: 'customer.derived' }),
        entry({ name: 'customer.overridden', origin: 'Overridden', version: 2 }),
        entry({ name: 'order.is-paid', modelType: 'order' }),
      ]),
    });
    renderPage(client);
    await screen.findByRole('treeitem', { name: /derived/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'order.fresh');
    expect(optionsOf(/starts from/i)).toEqual(['customer.derived', 'customer.overridden']);

    // Deliberately a source that is neither the default nor available under the model chosen next:
    // the picker has to follow the model select, and a choice of another model would not bind, so
    // it is replaced rather than left standing.
    await userEvent.selectOptions(screen.getByLabelText(/starts from/i), 'customer.overridden');
    await userEvent.selectOptions(screen.getByLabelText('Model type'), 'order');

    expect(optionsOf(/starts from/i)).toEqual(['order.is-paid']);
    await userEvent.click(screen.getByRole('button', { name: /create/i }));
    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      modelType: 'order',
      document: { rule: { spec: 'order.is-paid' } },
    })));
  });

  it('says why it cannot create when there is nothing to start from', async () => {
    const client = stubClient({ listPropositions: vi.fn().mockResolvedValue([]) });
    renderPage(client);
    await waitFor(() => expect(client.listPropositions).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');

    // A UI-authored proposition is composition-only, so with nothing registered to compose over
    // there is nothing to create. The button being dead is not an explanation, so it is said.
    expect(screen.getByRole('button', { name: /create/i }).hasAttribute('disabled')).toBe(true);
    expect(screen.getByText(/nothing to start from/i)).toBeTruthy();
  });

  it('seeds the dialog from the derived-from node', async () => {
    const client = stubClient();
    // Not the first source alphabetically, so preselecting it cannot be confused with simply
    // falling back to the head of the list.
    renderPage(client, 'customer.overridden');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(await screen.findByRole('button', { name: /derive/i }));

    // Prefilled to the source's namespace, so derivation lands beside what it came from
    expect((screen.getByLabelText('Name') as HTMLInputElement).value).toBe('customer.');
    // …and the source it derives from is the one already picked out for it.
    expect((screen.getByLabelText(/starts from/i) as HTMLSelectElement).value)
      .toBe('customer.overridden');
  });

  it('creates a derived proposition whose document references its source', async () => {
    const client = stubClient();
    renderPage(client, 'customer.overridden');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(await screen.findByRole('button', { name: /derive/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'onward');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.onward',
      document: { rule: { spec: 'customer.overridden' } },
    })));
  });

  it('seeds the dialog with the compiled name when overriding', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    renderPage(client, 'customer.is-active');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(await screen.findByRole('button', { name: /^override$/i }));

    // An override *takes* the compiled spec's name — it does not derive a new one from it.
    expect((screen.getByLabelText('Name') as HTMLInputElement).value).toBe('customer.is-active');
    expect(screen.getByRole('dialog').getAttribute('aria-label')).toContain('customer.is-active');
  });

  it('never offers the name being overridden as its own source', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    renderPage(client, 'customer.is-active');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(await screen.findByRole('button', { name: /^override$/i }));

    // An override is authored under the very name it overrides, so a reference back to that name
    // is a cycle straight onto itself. The name being defined is excluded, whatever it is.
    expect(optionsOf(/starts from/i)).toEqual(['customer.derived', 'customer.overridden']);
  });

  it('creates the override as a reference to its chosen source', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    // Deliberately *not* something the picker can produce: the create must come from the chosen
    // source, not from whatever the shared editor draft happens to hold.
    renderPage(client, 'customer.is-active', { rule: { spec: 'customer.has-orders' } });
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(await screen.findByRole('button', { name: /^override$/i }));
    await userEvent.selectOptions(screen.getByLabelText(/starts from/i), 'customer.overridden');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.is-active',
      document: { rule: { spec: 'customer.overridden' } },
    })));
  });

  it('reports a name already taken', async () => {
    const client = stubClient({
      createProposition: vi.fn().mockResolvedValue({ outcome: 'nameTaken' }),
    });
    renderPage(client);
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.derived');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    expect(await screen.findByText(/already/i)).toBeTruthy();
  });

  it('reports the referrers blocking a delete', async () => {
    const client = stubClient({
      deleteProposition: vi.fn().mockResolvedValue({ outcome: 'referenced', referrers: ['customer.other'] }),
    });
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(await screen.findByRole('button', { name: /^delete$/i }));

    expect((await screen.findByRole('alert')).textContent).toContain('customer.other');
  });

  it('drops the selection when a delete removed the proposition outright', async () => {
    const client = stubClient();
    const { onSelect } = renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(await screen.findByRole('button', { name: /^delete$/i }));

    // DELETE answers the same `{ version: 0 }` either way, so the authored-vs-overridden
    // distinction has to be read off the entry before the call, not inferred from the response.
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith(null));
  });

  it('reloads the now-compiled proposition when a delete reverted an override', async () => {
    const client = stubClient();
    const { onSelect } = renderPage(client, 'customer.overridden');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalledTimes(1));

    await userEvent.click(await screen.findByRole('button', { name: /revert/i }));

    // The name survives a revert — it is served by the compiled spec now — so it stays selected,
    // and the document behind it has changed, so it is fetched again.
    await waitFor(() => expect(client.getProposition).toHaveBeenCalledTimes(2));
    expect(onSelect).toHaveBeenCalledWith('customer.overridden');
    expect(onSelect).not.toHaveBeenCalledWith(null);
  });

  it('refreshes the listing after a successful create', async () => {
    const client = stubClient();
    renderPage(client);
    await screen.findByRole('treeitem', { name: /is-active/ });
    const before = client.listPropositions.mock.calls.length;

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() =>
      expect(client.listPropositions.mock.calls.length).toBeGreaterThan(before));
  });
});
