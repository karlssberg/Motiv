import { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore, type RuleListEntry, type RulesApiClient } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { RulesPage } from '../../src/panes/RulesPage.js';
import { recallSelection } from '../../src/shell/lastSelection.js';

const entries: RuleListEntry[] = [
  {
    name: 'can-checkout',
    modelType: 'customer',
    metadataType: 'String',
    isAsync: false,
    isPolicy: false,
    version: 1,
    description: 'Gate',
  },
  {
    name: 'fraud-screening',
    modelType: 'customer',
    metadataType: 'String',
    // Async on purpose: the onLoaded guard below asserts this reaches the shell, which is what
    // lets App switch validation into async mode.
    isAsync: true,
    isPolicy: false,
    version: 1,
    description: 'Screening',
  },
];

function makeClient(overrides: Partial<Record<string, unknown>> = {}): RulesApiClient {
  return {
    listRules: vi.fn().mockResolvedValue(entries),
    // Deliberately *not* the document the harness seeds the store with. With the two the same,
    // "loads the picked rule into the store" held whether or not the load ever ran.
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'is-adult' } }, version: 3 }),
    putRule: vi.fn().mockResolvedValue({ outcome: 'updated', version: 4 }),
    // The page's panes each fetch the catalog on mount; an empty one is enough for them to render.
    getCatalog: vi.fn().mockResolvedValue({ specs: [], collections: [] }),
    ...overrides,
  } as unknown as RulesApiClient;
}

/**
 * The page under the shell it actually runs in: the selection is route state `App` owns, handed
 * down as `selected` and changed through `onSelect`. This holds that state so a palette pick, a
 * Close and a deep link all take the path they take for real.
 */
let setRoute: (name: string | null) => void = () => {};

function Harness(props: {
  client: RulesApiClient;
  initial: string | null;
  onSelect: (name: string | null) => void;
  onLoaded?: (entry: RuleListEntry | null) => void;
}) {
  const [selected, setSelected] = useState(props.initial);
  setRoute = setSelected;
  return (
    <RulesPage
      client={props.client}
      page="rules"
      selected={selected}
      onSelect={(name) => { props.onSelect(name); setSelected(name); }}
      {...(props.onLoaded ? { onLoaded: props.onLoaded } : {})}
    />
  );
}

function renderPage(
  client: RulesApiClient = makeClient(),
  store = new RuleEditorStore({ rule: { spec: 'is-active' } }),
  onLoaded?: (entry: RuleListEntry | null) => void,
  initial: string | null = null,
) {
  const onSelect = vi.fn();
  render(
    <RuleEditorProvider store={store}>
      <Harness client={client} initial={initial} onSelect={onSelect} {...(onLoaded ? { onLoaded } : {})} />
    </RuleEditorProvider>,
  );
  return { store, onSelect, select: (name: string | null) => act(() => setRoute(name)) };
}

/**
 * Opens the palette from the toolbar, types a query that uniquely matches one row, and presses
 * Enter to choose it — the same path a keyboard-only user would take.
 */
async function pickViaPalette(query: string): Promise<void> {
  await userEvent.click(screen.getByRole('button', { name: 'Open' }));
  await userEvent.type(screen.getByRole('combobox'), query);
  await userEvent.keyboard('{Enter}');
}

describe('RulesPage', () => {
  it('shows an empty state instead of the panes while no rule is open', async () => {
    // There is no local draft any more: a document with nothing to save it back to was the source
    // of the stale-document confusion on the propositions page, and nothing could be done with it.
    renderPage();

    expect(screen.queryByRole('region', { name: 'Editor' })).toBeNull();
    expect(screen.queryByRole('region', { name: 'Checkout' })).toBeNull();
    const empty = screen.getByRole('region', { name: 'No rule open' });
    // Rules are not authored here, so the one way forward is to choose one.
    expect(within(empty).queryByRole('button', { name: /new/i })).toBeNull();
    for (const name of ['JSON', 'Close']) {
      expect(screen.getByRole('button', { name }).getAttribute('aria-disabled')).toBe('true');
    }

    await userEvent.click(within(empty).getByRole('button', { name: 'Choose a rule' }));
    expect(await screen.findByRole('dialog', { name: 'Rules' })).toBeTruthy();
  });

  it('lists every server rule in the palette, and nothing else', async () => {
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Open' }));

    const options = await screen.findAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual(['can-checkout', 'fraud-screening']);
  });

  it('follows the route: a deep link loads the rule, and choosing one writes the route', async () => {
    const client = makeClient();
    const { onSelect } = renderPage(client, undefined, undefined, 'fraud-screening');
    await waitFor(() => expect(client.getRule).toHaveBeenCalledWith('fraud-screening'));
    expect(recallSelection('rules')).toBe('fraud-screening');

    await pickViaPalette('can-checkout');
    expect(onSelect).toHaveBeenCalledWith('can-checkout');
    await waitFor(() => expect(client.getRule).toHaveBeenCalledWith('can-checkout'));
  });

  it('loads the picked rule into the store, and wears its name', async () => {
    const client = makeClient();
    const { store } = renderPage(client);

    await pickViaPalette('can-checkout');

    await waitFor(() => expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } }));
    const editor = screen.getByRole('region', { name: 'Editor' });
    expect(within(editor).getByText(/v3/)).toBeDefined();
    expect(await within(editor).findByText('can-checkout')).toBeTruthy();
    // The model type the rule is validated and evaluated against sits beside the name.
    expect(within(editor).getByText('customer')).toBeDefined();
    // Choosing closes it: the palette is transient, and it has just been relabelled.
    expect(screen.queryByRole('dialog', { name: 'Rules' })).toBeNull();
  });

  it('closes back to the empty state, and forgets the selection', async () => {
    const { onSelect } = renderPage();
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    expect(onSelect).toHaveBeenLastCalledWith(null);
    expect(await screen.findByRole('region', { name: 'No rule open' })).toBeTruthy();
    expect(screen.queryByText(/v3/)).toBeNull();
    expect(recallSelection('rules')).toBeNull();
  });

  it('asks before closing a rule with unsaved changes', async () => {
    const { store, onSelect } = renderPage();
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);
    act(() => store.replaceNode('$.rule', { spec: 'edited' }));

    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    const dialog = await screen.findByRole('dialog', { name: 'Unsaved changes' });
    expect(onSelect).not.toHaveBeenCalledWith(null);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Discard changes' }));
    expect(onSelect).toHaveBeenLastCalledWith(null);
    expect(await screen.findByRole('region', { name: 'No rule open' })).toBeTruthy();
    // Discarded means gone: the store is back at what was loaded, not merely hidden. This is what
    // keeps a code-defined default — whose load fetches no document — from resurfacing the edit.
    expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } });
    expect(store.getState().dirty).toBe(false);
  });

  it('saves and closes in one step from the split button', async () => {
    const client = makeClient();
    const { onSelect } = renderPage(client);
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: 'Save options' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Save & close/ }));

    await waitFor(() => expect(client.putRule).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(onSelect).toHaveBeenLastCalledWith(null));
    expect(await screen.findByRole('region', { name: 'No rule open' })).toBeTruthy();
  });

  it('shows a code-default note when the server document is null', async () => {
    const client = makeClient({
      getRule: vi.fn().mockResolvedValue({ document: null, version: 1 }),
    });
    renderPage(client);

    await pickViaPalette('can-checkout');

    expect(await screen.findByText(/code-defined default/i)).toBeDefined();
  });

  it('saves with the loaded version and shows the new one', async () => {
    const client = makeClient();
    renderPage(client);
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() =>
      expect(client.putRule).toHaveBeenCalledWith('can-checkout', { rule: { spec: 'is-adult' } }, 3));
    expect(await screen.findByText(/v4/)).toBeDefined();
  });

  it('reports the loaded rule entry via onLoaded, and null when cleared', async () => {
    const onLoaded = vi.fn();
    const client = makeClient();
    renderPage(client, undefined, onLoaded);

    await pickViaPalette('fraud-screening');
    await waitFor(() =>
      expect(onLoaded).toHaveBeenCalledWith(expect.objectContaining({ name: 'fraud-screening', isAsync: true })));

    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(onLoaded).toHaveBeenLastCalledWith(null));
  });

  it('pushes validation errors from an invalid save into the store', async () => {
    const errors = [{ path: '$.rule', code: 'PolicyRequired', message: 'the rule must be a policy' }];
    const client = makeClient({
      putRule: vi.fn().mockResolvedValue({ outcome: 'invalid', errors }),
    });
    const { store } = renderPage(client);
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(store.getState().errors).toEqual(errors));
  });

  it('shows a conflict banner with a reload action on version conflicts', async () => {
    const client = makeClient({
      putRule: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 9 }),
    });
    renderPage(client);
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toBeDefined();
    expect(screen.getByText(/someone else saved version 9/i)).toBeDefined();

    await userEvent.click(screen.getByRole('button', { name: /reload latest/i }));
    await waitFor(() => expect(client.getRule).toHaveBeenCalledTimes(2));
  });

  it('reports a failed listing fetch, which nothing else on the page would say', async () => {
    const client = makeClient({
      listRules: vi.fn().mockRejectedValue(new Error('Rules service unavailable (503)')),
    });
    renderPage(client);

    expect(await screen.findByRole('alert')).toBeDefined();
    expect(screen.getByText(/rules service unavailable \(503\)/i)).toBeDefined();
    // Nothing is loaded, so there is nothing to reload: an offer to recover an identity the page
    // has not got would be a button that does nothing.
    expect(screen.queryByRole('button', { name: /reload latest/i })).toBeNull();
  });

  it('reports a thrown save, and offers the loaded rule as the way back', async () => {
    const client = makeClient({
      putRule: vi.fn().mockRejectedValue(new Error('Rules service unavailable (503)')),
    });
    renderPage(client);
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText(/rules service unavailable \(503\)/i)).toBeDefined();
    await userEvent.click(screen.getByRole('button', { name: /reload latest/i }));
    await waitFor(() => expect(client.getRule).toHaveBeenCalledTimes(2));
    // The reload is the recovery, so the banner goes with it rather than outliving the act that
    // answered it.
    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
  });

  it('chooses the rule a partial query narrows to', async () => {
    renderPage();

    await pickViaPalette('fraud');

    expect(await screen.findByText('fraud-screening')).toBeTruthy();
    expect(screen.queryByRole('dialog', { name: 'Rules' })).toBeNull();
  });

  it('offers no authoring actions, because rules are not authored here', async () => {
    // The footer is caller-supplied precisely so this page can omit it. Rules are placeholders
    // for compile-time logic; there is nothing to create, derive or delete.
    renderPage();
    await userEvent.click(screen.getByRole('button', { name: 'Open' }));
    expect(screen.queryByRole('button', { name: /^new$/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /derive/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /delete/i })).toBeNull();
  });

  it('opens the palette with ⌘K', async () => {
    // The same chord the propositions page uses, through the same hook — which is the whole point
    // of the hook. Without this the binding was proven on one page only, and deleting
    // the page's `useCommandKey` call passed the entire suite.
    renderPage();

    await userEvent.keyboard('{Meta>}k{/Meta}');

    expect(screen.getByRole('dialog', { name: 'Rules' })).toBeTruthy();
  });

  it('opens the document viewer from the toolbar once a rule is open', async () => {
    renderPage();
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);
    await userEvent.click(screen.getByRole('button', { name: 'JSON' }));
    expect(screen.getByRole('dialog', { name: /document/i })).toBeTruthy();
  });

  it('leaves ⌘K inert while the document viewer is open, rather than stacking a palette over it', async () => {
    // Both pages bind the chord through the one hook, so the guard belongs in the hook and not in
    // either page — which is what this asserts from the second page. The propositions page proves
    // the same rule against its authoring dialog.
    renderPage();
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);
    await userEvent.click(screen.getByRole('button', { name: 'JSON' }));
    await screen.findByRole('dialog', { name: /document/i });

    await userEvent.keyboard('{Meta>}k{/Meta}');

    expect(screen.queryByRole('dialog', { name: 'Rules' })).toBeNull();
  });
});
