import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
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
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'is-active' } }, version: 3 }),
    putRule: vi.fn().mockResolvedValue({ outcome: 'updated', version: 4 }),
    ...overrides,
  } as unknown as RulesApiClient;
}

function renderHeader(
  client: RulesApiClient = makeClient(),
  store = new RuleEditorStore({ rule: { spec: 'is-active' } }),
  onLoaded?: (entry: RuleListEntry | null) => void,
) {
  render(
    <RuleEditorProvider store={store}>
      <RuleHeader
        client={client}
        page="rules"
        onNavigate={vi.fn()}
        {...(onLoaded ? { onLoaded } : {})}
      />
    </RuleEditorProvider>,
  );
  return store;
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

describe('RuleHeader', () => {
  it('names the local draft in the breadcrumb until a rule is loaded', async () => {
    renderHeader();

    expect(await screen.findByText('local draft')).toBeTruthy();
  });

  it('lists the local draft and every server rule in the palette', async () => {
    renderHeader();

    await userEvent.click(screen.getByRole('button', { name: 'Open' }));

    const options = await screen.findAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual([
      'local draft', 'can-checkout', 'fraud-screening',
    ]);
  });

  it('loads the picked rule into the store, and wears its name', async () => {
    const client = makeClient();
    const store = renderHeader(client);

    await pickViaPalette('can-checkout');

    await waitFor(() => expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } }));
    expect(screen.getByText(/v3/)).toBeDefined();
    expect(await screen.findByText('can-checkout')).toBeTruthy();
    // Choosing closes it: the palette is transient, and it has just been relabelled.
    expect(screen.queryByRole('dialog', { name: 'Rules' })).toBeNull();
  });

  it('returns to the local draft when that is picked back, keeping the document', async () => {
    const store = renderHeader();
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await pickViaPalette('local draft');

    // Only the server identity is dropped — what is in the editor stays put, and is now a draft
    // again: there is nothing to save it back to.
    expect(await screen.findByText('local draft')).toBeTruthy();
    expect(screen.queryByText(/v3/)).toBeNull();
    expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
  });

  it('shows a code-default note when the server document is null', async () => {
    const client = makeClient({
      getRule: vi.fn().mockResolvedValue({ document: null, version: 1 }),
    });
    renderHeader(client);

    await pickViaPalette('can-checkout');

    expect(await screen.findByText(/code-defined default/i)).toBeDefined();
  });

  it('saves with the loaded version and shows the new one', async () => {
    const client = makeClient();
    renderHeader(client);
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() =>
      expect(client.putRule).toHaveBeenCalledWith('can-checkout', { rule: { spec: 'is-active' } }, 3));
    expect(await screen.findByText(/v4/)).toBeDefined();
  });

  it('reports the loaded rule entry via onLoaded, and null when cleared', async () => {
    const onLoaded = vi.fn();
    const client = makeClient();
    renderHeader(client, undefined, onLoaded);

    await pickViaPalette('fraud-screening');
    await waitFor(() =>
      expect(onLoaded).toHaveBeenCalledWith(expect.objectContaining({ name: 'fraud-screening', isAsync: true })));

    await pickViaPalette('local draft');
    await waitFor(() => expect(onLoaded).toHaveBeenLastCalledWith(null));
  });

  it('pushes validation errors from an invalid save into the store', async () => {
    const errors = [{ path: '$.rule', code: 'PolicyRequired', message: 'the rule must be a policy' }];
    const client = makeClient({
      putRule: vi.fn().mockResolvedValue({ outcome: 'invalid', errors }),
    });
    const store = renderHeader(client);
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(store.getState().errors).toEqual(errors));
  });

  it('shows a conflict banner with a reload action on version conflicts', async () => {
    const client = makeClient({
      putRule: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 9 }),
    });
    renderHeader(client);
    await pickViaPalette('can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    expect(await screen.findByRole('alert')).toBeDefined();
    expect(screen.getByText(/someone else saved version 9/i)).toBeDefined();

    await userEvent.click(screen.getByRole('button', { name: /reload latest/i }));
    await waitFor(() => expect(client.getRule).toHaveBeenCalledTimes(2));
  });

  it('chooses the rule a partial query narrows to', async () => {
    renderHeader();

    await pickViaPalette('fraud');

    expect(await screen.findByText('fraud-screening')).toBeTruthy();
    expect(screen.queryByRole('dialog', { name: 'Rules' })).toBeNull();
  });

  it('offers no authoring actions, because rules are not authored here', async () => {
    // The footer is caller-supplied precisely so this page can omit it. Rules are placeholders
    // for compile-time logic; there is nothing to create, derive or delete.
    renderHeader();
    await userEvent.click(screen.getByRole('button', { name: 'Open' }));
    expect(screen.queryByRole('button', { name: /^new$/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /derive/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /delete/i })).toBeNull();
  });

  it('opens the document viewer from the toolbar', async () => {
    renderHeader();
    await userEvent.click(screen.getByRole('button', { name: 'JSON' }));
    expect(screen.getByRole('dialog', { name: /document/i })).toBeTruthy();
  });
});
