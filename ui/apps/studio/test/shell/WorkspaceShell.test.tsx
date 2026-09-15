import { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RulesApiError, type PropositionListEntry } from '@motiv-rules/core';
import type { Route } from '../../src/routing/useHashRoute.js';
import { WorkspaceShell } from '../../src/shell/WorkspaceShell.js';
import { Workspace, tabIdOf } from '../../src/shell/workspace.js';

// jsdom lays nothing out, so the tab strip would measure a width of zero and show one chip; give
// it room for four, so every open tab is a `tab` rather than an entry in the overflow menu.
Object.defineProperty(HTMLElement.prototype, 'clientWidth', { configurable: true, get: () => 800 });

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

/** A client stubbed just far enough for the shell: only the calls it and its tabs actually make. */
function stubClient(overrides: Record<string, unknown> = {}) {
  return {
    listRules: vi.fn().mockResolvedValue([
      { name: 'can-checkout', modelType: 'customer', metadataType: 'String', isAsync: false, isPolicy: false, version: 1, description: null },
    ]),
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'customer.derived' } }, version: 1 }),
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

/** The strip's tabs, by label — scoped to its tablist, since the editor's surface switch is a tablist too. */
const openTabs = (): string[] =>
  within(screen.getByRole('tablist', { name: 'Open documents' })).getAllByRole('tab').map((tab) => tab.getAttribute('aria-label') ?? '');

/** A tab's panel by name — hidden panels stay mounted, so queries are scoped to the one meant. */
const findPanel = (name: string): Promise<HTMLElement> => screen.findByRole('tabpanel', { name });
const getPanel = (name: string): HTMLElement => screen.getByRole('tabpanel', { name });

/** The values a `<select>` currently offers, in the order it offers them. */
function optionsOf(label: string | RegExp): string[] {
  return [...(screen.getByLabelText(label) as HTMLSelectElement).options].map((option) => option.value);
}

/** Opens the Open palette the way the shell offers it everywhere: ⌘K. The strip's "+" is a new tab. */
async function openPalette(): Promise<void> {
  await userEvent.keyboard('{Meta>}k{/Meta}');
  await screen.findByRole('dialog', { name: 'Open' });
}

/**
 * Opens the propositions explorer: it is one step in from the Open palette now, behind *Manage
 * propositions*, so every test that reaches into it — the tree, New/Derive/Override/Delete —
 * goes through both.
 */
async function openExplorer(): Promise<void> {
  await openPalette();
  await userEvent.click(await screen.findByRole('button', { name: 'Manage propositions' }));
  await screen.findByRole('dialog', { name: 'Propositions' });
}

/**
 * The shell under the route it actually runs in: `App` owns the hash, and the shell reads it and
 * writes it back through `navigate`. This holds that state so a palette pick, a close and a deep
 * link all take the path they take for real.
 */
let setRoute: (route: Route) => void = () => {};

function Harness(props: { client: ReturnType<typeof stubClient>; initial: Route; navigate: (route: Route) => void; workspace: Workspace }) {
  const [route, set] = useState(props.initial);
  setRoute = set;
  return (
    <WorkspaceShell
      client={props.client as never}
      route={route}
      navigate={(next) => { props.navigate(next); set(next); }}
      workspace={props.workspace}
    />
  );
}

function renderShell(
  client: ReturnType<typeof stubClient> = stubClient(),
  selected: string | null = null,
  page: 'rules' | 'propositions' = 'propositions',
) {
  const navigate = vi.fn();
  const workspace = new Workspace();
  render(<Harness client={client} initial={{ page, name: selected }} navigate={navigate} workspace={workspace} />);
  const storeOf = (kind: 'rule' | 'proposition', name: string) => workspace.getState().docs[tabIdOf(kind, name)]!.store;
  return { navigate, workspace, storeOf, select: (route: Route) => act(() => setRoute(route)) };
}

describe('WorkspaceShell', () => {
  it('lists the propositions in the explorer, and the rules and propositions in the Open palette', async () => {
    renderShell();
    await openPalette();
    const open = screen.getByRole('dialog', { name: 'Open' });
    await waitFor(() => expect(within(open).getAllByRole('option').length).toBe(4));
    expect(within(open).getByRole('option', { name: /can-checkout/ })).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'Manage propositions' }));
    const explorer = await screen.findByRole('dialog', { name: 'Propositions' });
    expect(await within(explorer).findByRole('treeitem', { name: /derived/ })).toBeTruthy();
  });

  it('shows the catalog in one empty tab while nothing is in the route, and the document in that tab once something is', async () => {
    const { select } = renderShell();
    expect(await screen.findByRole('tabpanel', { name: 'Catalog' })).toBeTruthy();
    expect(screen.getByRole('tab', { name: 'Catalog', selected: true })).toBeTruthy();
    expect(screen.queryByRole('region', { name: 'Editor' })).toBeNull();

    // The route's document lands in the empty tab, not beside it.
    select({ page: 'propositions', name: 'customer.derived' });
    expect(await screen.findByRole('tab', { name: 'Proposition customer.derived', selected: true })).toBeTruthy();
    expect(await screen.findByRole('region', { name: 'Editor' })).toBeTruthy();
    expect(openTabs()).toEqual(['Proposition customer.derived']);
  });

  it('lists the rules and propositions in the catalog, and choosing one opens it in this tab', async () => {
    const { navigate } = renderShell();
    const catalog = await findPanel('Catalog');
    expect(await within(catalog).findByRole('button', { name: /can-checkout/ })).toBeTruthy();
    expect(within(catalog).getByRole('button', { name: /customer\.overridden/ })).toBeTruthy();

    await userEvent.type(within(catalog).getByRole('searchbox', { name: 'Filter the catalog' }), 'derived');
    expect(within(catalog).queryByRole('button', { name: /can-checkout/ })).toBeNull();
    await userEvent.click(within(catalog).getByRole('button', { name: /customer\.derived/ }));

    expect(navigate).toHaveBeenCalledWith({ page: 'propositions', name: 'customer.derived' });
    expect(await screen.findByRole('tab', { name: 'Proposition customer.derived', selected: true })).toBeTruthy();
    expect(openTabs()).toEqual(['Proposition customer.derived']);
  });

  it('starts a new proposition from the catalog', async () => {
    renderShell();
    const catalog = await findPanel('Catalog');
    await userEvent.click(within(catalog).getByRole('button', { name: 'New proposition' }));
    expect(await screen.findByRole('dialog', { name: 'New proposition' })).toBeTruthy();
  });

  it('+ opens a second, empty tab, and its catalog opens documents beside the first', async () => {
    const { navigate } = renderShell(stubClient(), 'customer.derived');
    await screen.findByRole('region', { name: 'Editor' });

    await userEvent.click(screen.getByRole('button', { name: 'New tab' }));
    expect(navigate).toHaveBeenLastCalledWith({ page: 'propositions', name: null });
    expect(screen.getByRole('tab', { name: 'Catalog', selected: true })).toBeTruthy();
    const catalog = getPanel('Catalog');
    await userEvent.click(await within(catalog).findByRole('button', { name: /can-checkout/ }));

    expect(await screen.findByRole('tab', { name: 'Rule can-checkout', selected: true })).toBeTruthy();
    expect(openTabs()).toEqual(['Proposition customer.derived', 'Rule can-checkout']);
  });

  it('a tab\'s breadcrumb: Catalog unloads it in place, and the kind menu swaps in a sibling', async () => {
    const { navigate } = renderShell(stubClient(), 'customer.derived');
    const panel = await findPanel('customer.derived');
    const crumbs = within(panel).getByRole('navigation', { name: 'Where this tab is' });

    await userEvent.click(within(crumbs).getByRole('button', { name: 'Propositions' }));
    const menu = await screen.findByRole('menu', { name: 'Other propositions' });
    expect(within(menu).queryByRole('menuitem', { name: /derived/ })).toBeNull();
    await userEvent.click(within(menu).getByRole('menuitem', { name: /overridden/ }));
    expect(navigate).toHaveBeenLastCalledWith({ page: 'propositions', name: 'customer.overridden' });
    expect(await screen.findByRole('tab', { name: 'Proposition customer.overridden', selected: true })).toBeTruthy();
    expect(openTabs()).toEqual(['Proposition customer.overridden']);

    const replaced = await findPanel('customer.overridden');
    await userEvent.click(within(replaced).getByRole('button', { name: 'Catalog' }));
    expect(navigate).toHaveBeenLastCalledWith({ page: 'propositions', name: null });
    expect(await screen.findByRole('tab', { name: 'Catalog', selected: true })).toBeTruthy();
    expect(openTabs()).toEqual(['Catalog']);
  });

  it('asks before unloading a tab with unsaved changes', async () => {
    const client = stubClient();
    const { navigate, storeOf } = renderShell(client, 'customer.derived');
    const panel = await findPanel('customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());
    act(() => storeOf('proposition', 'customer.derived').replaceNode('$.rule', { spec: 'customer.is-adult' }));

    await userEvent.click(within(panel).getByRole('button', { name: 'Catalog' }));
    const dialog = await screen.findByRole('dialog', { name: 'Unsaved changes' });
    expect(navigate).not.toHaveBeenCalled();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Keep editing' }));
    expect(screen.getByRole('tab', { name: 'Proposition customer.derived' })).toBeTruthy();

    await userEvent.click(within(panel).getByRole('button', { name: 'Catalog' }));
    await userEvent.click(within(await screen.findByRole('dialog', { name: 'Unsaved changes' })).getByRole('button', { name: 'Save & unload' }));
    await waitFor(() => expect(client.putProposition).toHaveBeenCalledTimes(1));
    expect(await screen.findByRole('tab', { name: 'Catalog', selected: true })).toBeTruthy();
    expect(screen.queryByRole('tab', { name: 'Proposition customer.derived' })).toBeNull();
  });

  it('choosing from the Open palette navigates to the document, which opens its tab', async () => {
    const { navigate } = renderShell();
    await openPalette();
    const open = screen.getByRole('dialog', { name: 'Open' });
    await userEvent.type(within(open).getByRole('combobox'), 'can-checkout');
    await userEvent.keyboard('{Enter}');
    expect(navigate).toHaveBeenCalledWith({ page: 'rules', name: 'can-checkout' });
    expect(await screen.findByRole('tab', { name: 'Rule can-checkout', selected: true })).toBeTruthy();
  });

  it('opens the Open palette with ⌘K, and leaves it inert while a dialog is open', async () => {
    renderShell();
    await userEvent.keyboard('{Meta>}k{/Meta}');
    expect(screen.getByRole('dialog', { name: 'Open' })).toBeTruthy();
    await userEvent.keyboard('{Escape}');

    await openExplorer();
    await userEvent.click(screen.getByRole('button', { name: 'New' }));
    await screen.findByRole('dialog', { name: 'New proposition' });
    await userEvent.keyboard('{Meta>}k{/Meta}');
    expect(screen.queryByRole('dialog', { name: 'Open' })).toBeNull();
    expect(screen.getByRole('dialog', { name: 'New proposition' })).toBeTruthy();
  });

  it('claims ⌘K from the browser rather than letting both fire', () => {
    renderShell();
    expect(fireEvent.keyDown(window, { key: 'k', metaKey: true })).toBe(false);
  });

  it('opens the explorer fresh, discarding the previous query', async () => {
    renderShell();
    await openExplorer();
    await userEvent.type(screen.getByRole('combobox'), 'derived');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: /close/i }));

    await openExplorer();
    expect(screen.getByRole('combobox')).toHaveProperty('value', '');
  });

  it('closes the explorer once a proposition is chosen, and opens it as a tab', async () => {
    const { navigate } = renderShell();
    await openExplorer();
    await userEvent.type(screen.getByRole('combobox'), 'derived');
    await userEvent.keyboard('{Enter}');
    expect(navigate).toHaveBeenCalledWith({ page: 'propositions', name: 'customer.derived' });
    expect(screen.queryByRole('dialog', { name: 'Propositions' })).toBeNull();
    expect(await screen.findByRole('tab', { name: 'Proposition customer.derived' })).toBeTruthy();
  });

  it('surfaces a thrown listing failure', async () => {
    const client = stubClient({
      listPropositions: vi.fn().mockRejectedValue(new RulesApiError(500, 'listing exploded')),
    });
    renderShell(client);
    expect((await screen.findByRole('alert')).textContent).toContain('listing exploded');
  });

  it('surfaces a thrown delete failure, and keeps the tab', async () => {
    const client = stubClient({
      deleteProposition: vi.fn().mockRejectedValue(new RulesApiError(500, 'delete exploded')),
    });
    renderShell(client, 'customer.derived');
    await screen.findByRole('tab', { name: 'Proposition customer.derived' });

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^delete$/i }));

    expect((await screen.findByRole('alert')).textContent).toContain('delete exploded');
    expect(screen.getByRole('tab', { name: 'Proposition customer.derived' })).toBeTruthy();
  });

  it('surfaces a thrown create failure in the dialog that raised it', async () => {
    const client = stubClient({
      createProposition: vi.fn().mockRejectedValue(new RulesApiError(500, 'create exploded')),
    });
    renderShell(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    expect((await screen.findByRole('alert')).textContent).toContain('create exploded');
    expect(screen.getByRole('dialog')).toBeTruthy();
  });

  it('creates a proposition from the new dialog, starting from the first source offered', async () => {
    const client = stubClient();
    renderShell(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.fresh',
      modelType: 'customer',
      document: { rule: { spec: 'customer.derived' } },
    })));
  });

  it('opens the created proposition as a tab', async () => {
    const client = stubClient();
    const { navigate } = renderShell(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });
    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));
    await waitFor(() => expect(navigate).toHaveBeenCalledWith({ page: 'propositions', name: 'customer.fresh' }));
    expect(await screen.findByRole('tab', { name: 'Proposition customer.fresh', selected: true })).toBeTruthy();
  });

  it('starts a new proposition from whichever source is picked', async () => {
    const client = stubClient();
    renderShell(client);
    await openExplorer();
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
    renderShell(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /derived/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'order.fresh');
    expect(optionsOf(/starts from/i)).toEqual(['customer.derived', 'customer.overridden']);

    await userEvent.selectOptions(screen.getByLabelText(/starts from/i), 'customer.overridden');
    await userEvent.selectOptions(screen.getByLabelText('Model type'), 'order');

    expect(optionsOf(/starts from/i)).toEqual(['order.is-paid']);
    await userEvent.click(screen.getByRole('button', { name: /create/i }));
    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      modelType: 'order',
      document: { rule: { spec: 'order.is-paid' } },
    })));
  });

  it('says why it cannot create when there is nothing to start from, and does not create', async () => {
    const client = stubClient({ listPropositions: vi.fn().mockResolvedValue([]) });
    renderShell(client);
    await waitFor(() => expect(client.listPropositions).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');

    const create = screen.getByRole('button', { name: /create/i });
    expect(create.getAttribute('aria-disabled')).toBe('true');
    expect(create.hasAttribute('disabled')).toBe(false);
    expect(screen.getByText(/nothing to start from/i)).toBeTruthy();

    await userEvent.click(create);
    expect(client.createProposition).not.toHaveBeenCalled();
  });

  it('seeds the dialog from the derived-from node, and creates a document referencing it', async () => {
    const client = stubClient();
    renderShell(client, 'customer.overridden');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /derive/i }));

    expect((screen.getByLabelText('Name') as HTMLInputElement).value).toBe('customer.');
    expect((screen.getByLabelText(/starts from/i) as HTMLSelectElement).value).toBe('customer.overridden');

    await userEvent.type(screen.getByLabelText('Name'), 'onward');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));
    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.onward',
      document: { rule: { spec: 'customer.overridden' } },
    })));
  });

  it('seeds the override dialog with the compiled name, never offering it as its own source', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    renderShell(client, 'customer.is-active');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^override$/i }));

    expect((screen.getByLabelText('Name') as HTMLInputElement).value).toBe('customer.is-active');
    expect(screen.getByRole('dialog').getAttribute('aria-label')).toContain('customer.is-active');
    expect(optionsOf(/starts from/i)).toEqual(['customer.derived', 'customer.overridden']);

    await userEvent.selectOptions(screen.getByLabelText(/starts from/i), 'customer.overridden');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));
    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.is-active',
      document: { rule: { spec: 'customer.overridden' } },
    })));
  });

  it('keeps the whole form off the dialog element itself, where a click would dismiss it', async () => {
    // See PropositionDialog: the padding and gaps live on an inner wrapper that is the dialog's
    // one in-flow child, so no click can land on the element and be read as the backdrop.
    renderShell();
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });
    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));

    const dialog = screen.getByRole('dialog');
    const close = within(dialog).getByRole('button', { name: /close/i });
    const inFlow = [...dialog.children].filter((child) => child !== close);
    expect(inFlow.map((child) => child.className)).toEqual(['dialog-form']);
    expect(inFlow[0]!.contains(screen.getByLabelText('Name'))).toBe(true);
  });

  it('does not offer a quarantined authored proposition as a source', async () => {
    const client = stubClient({
      listPropositions: vi.fn().mockResolvedValue([
        entry({ name: 'customer.sound' }),
        entry({ name: 'customer.broken', quarantine: [{ path: '$', code: 'UnknownSpec', message: 'unknown spec' }] }),
        entry({
          name: 'customer.shadowed', origin: 'Overridden', version: 2,
          quarantine: [{ path: '$', code: 'UnknownSpec', message: 'unknown spec' }],
        }),
      ]),
    });
    renderShell(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /sound/ });
    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    expect(optionsOf(/starts from/i)).toEqual(['customer.shadowed', 'customer.sound']);
  });

  it('reports a name already taken', async () => {
    const client = stubClient({ createProposition: vi.fn().mockResolvedValue({ outcome: 'nameTaken' }) });
    renderShell(client);
    await openExplorer();
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
    renderShell(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());
    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^delete$/i }));
    expect((await screen.findByRole('alert')).textContent).toContain('customer.other');
  });

  it('closes the tab when a delete removed the proposition outright', async () => {
    const client = stubClient();
    renderShell(client, 'customer.derived');
    await screen.findByRole('tab', { name: 'Proposition customer.derived' });

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^delete$/i }));

    await waitFor(() => expect(screen.queryByRole('tab', { name: 'Proposition customer.derived' })).toBeNull());
    expect(await findPanel('Catalog')).toBeTruthy();
  });

  it('reloads the now-compiled proposition in its tab when a delete reverted an override', async () => {
    const client = stubClient();
    renderShell(client, 'customer.overridden');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalledTimes(1));

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /revert/i }));

    // The tab fetches again once the shell asks it to; the delete's own selection fetches too, so
    // the count is "more than the one load", not an exact number.
    await waitFor(() => expect(client.deleteProposition).toHaveBeenCalled());
    await waitFor(() => expect(client.getProposition.mock.calls.length).toBeGreaterThan(2));
    expect(screen.getByRole('tab', { name: 'Proposition customer.overridden' })).toBeTruthy();
  });

  it('refreshes the listing after a successful create', async () => {
    const client = stubClient();
    renderShell(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });
    const before = client.listPropositions.mock.calls.length;

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.listPropositions.mock.calls.length).toBeGreaterThan(before));
  });

  it('closes a clean tab from its ×, navigating to the bare page', async () => {
    const { navigate } = renderShell(stubClient(), 'customer.derived');
    await screen.findByRole('region', { name: 'Editor' });

    await userEvent.click(screen.getByLabelText('Close customer.derived'));

    await waitFor(() => expect(navigate).toHaveBeenCalledWith({ page: 'propositions', name: null }));
    // The last tab closed leaves one empty tab, as a browser window keeps one.
    expect(openTabs()).toEqual(['Catalog']);
  });

  it('asks before closing a tab with unsaved changes, and Save & close saves it first', async () => {
    const client = stubClient();
    const { navigate, storeOf } = renderShell(client, 'customer.derived');
    await screen.findByRole('region', { name: 'Editor' });
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());
    act(() => storeOf('proposition', 'customer.derived').replaceNode('$.rule', { spec: 'customer.is-adult' }));

    await userEvent.click(screen.getByLabelText('Close customer.derived (unsaved changes)'));
    const dialog = await screen.findByRole('dialog', { name: 'Unsaved changes' });
    expect(navigate).not.toHaveBeenCalled();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Save & close' }));
    await waitFor(() => expect(client.putProposition).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.queryByRole('tab', { name: 'Proposition customer.derived' })).toBeNull());
  });

  it('discards on request: the changes go, then the tab closes', async () => {
    const client = stubClient();
    const { storeOf } = renderShell(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());
    const store = storeOf('proposition', 'customer.derived');
    await waitFor(() => expect(store.getState().document).toEqual({ rule: { spec: 'customer.is-active' } }));
    act(() => store.replaceNode('$.rule', { spec: 'customer.is-adult' }));

    await userEvent.keyboard('{Meta>}w{/Meta}');
    const dialog = await screen.findByRole('dialog', { name: 'Unsaved changes' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Discard changes' }));
    await waitFor(() => expect(screen.queryByRole('tab', { name: 'Proposition customer.derived' })).toBeNull());
    expect(store.getState().dirty).toBe(false);
  });

  it('a proposition saved in one tab is marked updated in the rule tab that uses it', async () => {
    const client = stubClient();
    const { select } = renderShell(client, 'can-checkout', 'rules');
    await screen.findByRole('tab', { name: 'Rule can-checkout' });
    await waitFor(() => expect(client.getRule).toHaveBeenCalled());
    expect(await screen.findByRole('button', { name: /customer\.derived/ })).toBeTruthy();

    select({ page: 'propositions', name: 'customer.derived' });
    await screen.findByRole('tab', { name: 'Proposition customer.derived', selected: true });
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());
    // Scoped to the panel in front: the hidden rule panel holds a "v1" of its own.
    const panel = screen.getByRole('tabpanel', { name: 'customer.derived' });
    await within(panel).findByText('v1');
    await userEvent.click(within(panel).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.putProposition).toHaveBeenCalled());

    select({ page: 'rules', name: 'can-checkout' });
    await screen.findByRole('tab', { name: 'Rule can-checkout', selected: true });
    const rulePanel = screen.getByRole('tabpanel', { name: 'can-checkout' });
    expect(await within(rulePanel).findByText('updated')).toBeTruthy();
    expect(within(rulePanel).getByText('v2')).toBeTruthy();
  });
});

/**
 * The two ways an author moves scope outward: a node of the tree, or a definition of the
 * document, becomes a catalog proposition and the place it stood becomes a reference to it
 * (#234). Both run the ordinary create dialog, seeded with a document rather than with a source
 * to start from.
 */
describe('WorkspaceShell — Extract to catalog and Promote (#234)', () => {
  /** A proposition whose body has both a definition to promote and a node to extract. */
  const SCOPED = {
    document: {
      rule: {
        and: [
          { local: 'activity' },
          { spec: 'customer.is-adult', name: 'adulthood' },
        ],
      },
      definitions: {
        activity: { rule: { spec: 'customer.is-active' }, whenTrue: 'active' },
      },
    },
    version: 1,
    origin: 'Authored',
    hasCompiledDefault: false,
  };

  /** The shell with `customer.derived` open on the scoped document above, loaded. */
  async function openScoped(client: ReturnType<typeof stubClient>) {
    const shell = renderShell(client, 'customer.derived');
    const store = shell.storeOf('proposition', 'customer.derived');
    await waitFor(() => expect(store.getState().document.definitions).toBeDefined());
    return { ...shell, store };
  }

  const scopedClient = (overrides: Record<string, unknown> = {}) =>
    stubClient({ getProposition: vi.fn().mockResolvedValue(SCOPED), ...overrides });

  it('extracts a node: the subtree is the created document, and the node becomes a reference', async () => {
    const client = scopedClient();
    const { store } = await openScoped(client);

    await userEvent.click(await screen.findByRole('button', { name: 'actions for $.rule.and[1]' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Extract to catalog…' }));

    await screen.findByRole('dialog', { name: 'Extract to catalog' });
    await userEvent.type(screen.getByLabelText('Name'), 'customer.adulthood');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.adulthood',
      modelType: 'customer',
      document: { rule: { spec: 'customer.is-adult', name: 'adulthood' } },
    })));
    await waitFor(() => expect(store.getState().document.rule).toEqual({
      and: [{ local: 'activity' }, { spec: 'customer.adulthood' }],
    }));
  });

  it('asks nothing about what to start from once the flow brings its own document', async () => {
    const client = scopedClient();
    await openScoped(client);

    await userEvent.click(await screen.findByRole('button', { name: 'actions for $.rule.and[1]' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Extract to catalog…' }));

    const dialog = await screen.findByRole('dialog', { name: 'Extract to catalog' });
    expect(within(dialog).queryByLabelText(/starts from/i)).toBeNull();
    expect((within(dialog).getByLabelText('Name') as HTMLInputElement).value).toBe('');
  });

  it('promotes a definition: its body is the created document, and both references are rewritten', async () => {
    const client = scopedClient({
      getProposition: vi.fn().mockResolvedValue({
        ...SCOPED,
        document: {
          rule: { and: [{ local: 'activity' }, { local: 'activity' }] },
          definitions: { activity: { rule: { spec: 'customer.is-active' }, whenTrue: 'active' } },
        },
      }),
    });
    const { store } = await openScoped(client);

    await userEvent.click(await screen.findByRole('button', { name: 'actions for definition activity' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Promote to catalog…' }));

    const dialog = await screen.findByRole('dialog', { name: 'Promote to catalog' });
    const name = within(dialog).getByLabelText('Name') as HTMLInputElement;
    expect(name.value).toBe('activity');
    await userEvent.clear(name);
    await userEvent.type(name, 'customer.activity');
    await userEvent.click(within(dialog).getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.activity',
      document: { rule: { spec: 'customer.is-active', name: 'activity', whenTrue: 'active' } },
    })));
    await waitFor(() => expect(store.getState().document).toEqual({
      rule: { and: [{ spec: 'customer.activity' }, { spec: 'customer.activity' }] },
    }));
  });

  it('leaves the document alone when the create is refused, and says so in the dialog', async () => {
    const client = scopedClient({ createProposition: vi.fn().mockResolvedValue({ outcome: 'nameTaken' }) });
    const { store } = await openScoped(client);
    const before = store.getState().document;

    await userEvent.click(await screen.findByRole('button', { name: 'actions for $.rule.and[1]' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Extract to catalog…' }));
    await userEvent.type(await screen.findByLabelText('Name'), 'customer.derived');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    expect(await screen.findByText(/already/i)).toBeTruthy();
    expect(screen.getByRole('dialog', { name: 'Extract to catalog' })).toBeTruthy();
    expect(store.getState().document).toEqual(before);
  });
});
