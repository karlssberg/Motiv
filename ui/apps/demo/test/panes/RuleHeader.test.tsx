import { describe, it, expect, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore, type RuleListEntry, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { RuleHeader } from '../../src/panes/RuleHeader.js';

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
];

function makeClient(overrides: Partial<Record<string, unknown>> = {}): RulesApiClient {
  return {
    listRules: vi.fn().mockResolvedValue(entries),
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'is-active' } }, version: 3 }),
    putRule: vi.fn().mockResolvedValue({ outcome: 'updated', version: 4 }),
    ...overrides,
  } as unknown as RulesApiClient;
}

function renderHeader(client: RulesApiClient, store = new RuleEditorStore({ rule: { spec: 'is-active' } })) {
  render(
    <RuleEditorProvider store={store}>
      <RuleHeader client={client} />
    </RuleEditorProvider>,
  );
  return store;
}

/** Matches the breadcrumb leaf whatever rule it currently names. */
const LEAF = /^rule,/;
const findLeaf = () => screen.findByRole('combobox', { name: LEAF });

/** Opens the leaf's list and picks a rule by name, as a pointer would. */
async function pick(name: string): Promise<void> {
  fireEvent.click(await findLeaf());
  fireEvent.click(await screen.findByRole('option', { name }));
}

describe('RuleHeader', () => {
  it('names the local draft in the breadcrumb until a rule is loaded', async () => {
    renderHeader(makeClient());

    // The badge's text is its value, but a name given to a control replaces that text for
    // assistive tech — so the loaded rule has to be in the name too, or it is lost.
    expect((await screen.findByRole('combobox', { name: 'rule, local draft' })).textContent)
      .toBe('local draft');
  });

  it('lists the local draft and every server rule, marking the one in force', async () => {
    renderHeader(makeClient());
    fireEvent.click(await findLeaf());

    const options = screen.getAllByRole('option');
    expect(options.map((o) => o.textContent)).toEqual(['local draft', 'can-checkout']);
    expect(options.filter((o) => o.getAttribute('aria-selected') === 'true').map((o) => o.textContent))
      .toEqual(['local draft']);
  });

  it('loads the picked rule into the store, and wears its name', async () => {
    const client = makeClient();
    const store = renderHeader(client);

    await pick('can-checkout');

    await waitFor(() => expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } }));
    expect(screen.getByText(/v3/)).toBeDefined();
    expect((await findLeaf()).textContent).toBe('can-checkout');
    // Choosing closes it: the leaf is transient, and it has just been relabelled.
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('returns to the local draft when that is picked back, keeping the document', async () => {
    const store = renderHeader(makeClient());
    await pick('can-checkout');
    await screen.findByText(/v3/);

    await pick('local draft');

    // Only the server identity is dropped — what is in the editor stays put, and is now a draft
    // again: there is nothing to save it back to.
    expect((await findLeaf()).textContent).toBe('local draft');
    expect(screen.queryByText(/v3/)).toBeNull();
    expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
  });

  it('shows a code-default note when the server document is null', async () => {
    const client = makeClient({
      getRule: vi.fn().mockResolvedValue({ document: null, version: 1 }),
    });
    renderHeader(client);

    await pick('can-checkout');

    expect(await screen.findByText(/code-defined default/i)).toBeDefined();
  });

  it('saves with the loaded version and shows the new one', async () => {
    const client = makeClient();
    renderHeader(client);
    await pick('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() =>
      expect(client.putRule).toHaveBeenCalledWith('can-checkout', { rule: { spec: 'is-active' } }, 3));
    expect(await screen.findByText(/v4/)).toBeDefined();
  });

  it('shows a conflict banner with a reload action on version conflicts', async () => {
    const client = makeClient({
      putRule: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 9 }),
    });
    renderHeader(client);
    await pick('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    expect(await screen.findByRole('alert')).toBeDefined();
    expect(screen.getByText(/someone else saved version 9/i)).toBeDefined();

    await userEvent.click(screen.getByRole('button', { name: /reload latest/i }));
    await waitFor(() => expect(client.getRule).toHaveBeenCalledTimes(2));
  });
});
