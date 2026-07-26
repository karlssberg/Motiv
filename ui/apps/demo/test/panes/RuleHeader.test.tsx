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

describe('RuleHeader', () => {
  it('lists server rules and loads the picked one into the store', async () => {
    const client = makeClient();
    const store = renderHeader(client);

    await userEvent.selectOptions(await screen.findByLabelText('Rule'), 'can-checkout');

    await waitFor(() => expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } }));
    expect(screen.getByText(/v3/)).toBeDefined();
  });

  it('shows a code-default note when the server document is null', async () => {
    const client = makeClient({
      getRule: vi.fn().mockResolvedValue({ document: null, version: 1 }),
    });
    renderHeader(client);

    await userEvent.selectOptions(await screen.findByLabelText('Rule'), 'can-checkout');

    expect(await screen.findByText(/code-defined default/i)).toBeDefined();
  });

  it('saves with the loaded version and shows the new one', async () => {
    const client = makeClient();
    renderHeader(client);
    await userEvent.selectOptions(await screen.findByLabelText('Rule'), 'can-checkout');
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
    await userEvent.selectOptions(await screen.findByLabelText('Rule'), 'can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    expect(await screen.findByRole('alert')).toBeDefined();
    expect(screen.getByText(/someone else saved version 9/i)).toBeDefined();

    await userEvent.click(screen.getByRole('button', { name: /reload latest/i }));
    await waitFor(() => expect(client.getRule).toHaveBeenCalledTimes(2));
  });
});
