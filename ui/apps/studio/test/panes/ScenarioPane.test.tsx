import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor, act, within } from '@testing-library/react';
import { RuleEditorStore, type Catalog, type EvaluationResult, type RulesApiClient } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { ScenarioPane } from '../../src/panes/ScenarioPane.js';

/** The four seeds, as the host stores and lists them. */
const SEEDS = [
  ['s1', 'Active adult, 3 orders', '{ "customerId": "cust-42", "age": 30, "isActive": true, "orderCount": 3, "orders": [{ "total": 120 }] }'],
  ['s2', 'Minor', '{ "customerId": "cust-7", "age": 16, "isActive": true, "orderCount": 1, "orders": [{ "total": 20 }] }'],
  ['s3', 'Dormant account', '{ "customerId": "cust-9", "age": 41, "isActive": false, "orderCount": 12, "orders": [{ "total": 300 }] }'],
  ['s4', 'New, no orders', '{ "customerId": "cust-1", "age": 25, "isActive": true, "orderCount": 0 }'],
].map(([id, name, model]) => ({
  id, name, model, expectedSatisfied: null, sourceDecisionId: null, version: 1, author: 'system', timestampUtc: '2026-09-20T00:00:00Z',
}));

const result = (satisfied: boolean, ...assertions: string[]): EvaluationResult => ({
  satisfied, reason: assertions.join(' & '), assertions, values: assertions,
  justification: assertions.join('\n'), explanation: { assertions, underlying: [] },
});

const catalog: Catalog = {
  specs: [], collections: [], metadataTypes: {},
  modelTypes: { customer: { type: ['object', 'null'], properties: { age: { type: 'integer' }, isActive: { type: 'boolean' } } } },
};

function client(options: {
  live?: (model: { age: number; isActive: boolean }) => EvaluationResult;
  draft?: (model: { age: number; isActive: boolean }) => EvaluationResult;
  catalog?: Catalog;
} = {}): RulesApiClient {
  const live = options.live ?? ((m) => result(m.isActive, m.isActive ? 'customer is active' : 'customer is inactive'));
  const draft = options.draft ?? live;
  return {
    evaluateRule: vi.fn((_name: string, model: { age: number; isActive: boolean }) => Promise.resolve(live(model))),
    evaluate: vi.fn((request: { model: { age: number; isActive: boolean } }) => Promise.resolve(draft(request.model))),
    getCatalog: vi.fn().mockResolvedValue(options.catalog ?? catalog),
    listScenarios: vi.fn(() => Promise.resolve(SEEDS.map((s) => ({ ...s })))),
    putScenario: vi.fn().mockResolvedValue({ outcome: 'saved', version: 2 }),
    deleteScenario: vi.fn().mockResolvedValue({ outcome: 'saved', version: 1 }),
  } as unknown as RulesApiClient;
}

function renderPane(apiClient: RulesApiClient = client(), modelType = 'customer') {
  const store = new RuleEditorStore({ rule: { spec: 'customer.is-active' } });
  render(
    <RuleEditorProvider store={store}>
      <ScenarioPane client={apiClient} ruleName="can-checkout" version={3} modelType={modelType} />
    </RuleEditorProvider>,
  );
  return store;
}

const settleCatalog = () => act(async () => {});
const row = (name: string) => screen.getByRole('row', { name });
const runAll = async () => {
  fireEvent.click(screen.getByRole('button', { name: 'Run all' }));
  await waitFor(() => expect(screen.queryAllByText('…')).toHaveLength(0));
};

describe('ScenarioPane', () => {
  it('is the Evaluate region, seeded with named unevaluated scenarios under Live and Draft columns', async () => {
    renderPane();
    await settleCatalog();
    expect(screen.getByRole('region', { name: 'Evaluate' })).toBeDefined();
    expect(screen.getByRole('columnheader', { name: 'Live v3' })).toBeDefined();
    expect(screen.getByRole('columnheader', { name: 'Draft' })).toBeDefined();
    const minor = row('Minor');
    expect(within(minor).getAllByText('–')).toHaveLength(2);
    expect(within(minor).getByRole('button', { name: 'details of Minor' }).getAttribute('aria-expanded')).toBe('false');
  });

  it('runs every scenario against the live rule and the draft, and counts the ones that change', async () => {
    // The draft flips the dormant account only: live says inactive → no, draft says yes.
    const api = client({ draft: (m) => result(true, m.isActive ? 'customer is active' : 'inactive but forgiven') });
    const store = renderPane(api);
    await settleCatalog();

    await runAll();

    expect(api.evaluateRule).toHaveBeenCalledWith('can-checkout', expect.objectContaining({ customerId: 'cust-9' }));
    expect(api.evaluate).toHaveBeenCalledWith(expect.objectContaining({ modelType: 'customer', document: store.getState().document }));
    const dormant = row('Dormant account');
    expect(within(dormant).getByRole('cell', { name: /live/i }).textContent).toContain('no');
    expect(within(dormant).getByRole('cell', { name: /draft/i }).textContent).toContain('yes');
    expect(within(dormant).getByText('flips')).toBeDefined();
    expect(within(row('Minor')).queryByText('flips')).toBeNull();
    expect(screen.getByText('1 of 4 scenarios change')).toBeDefined();
  });

  it('says so when nothing changes', async () => {
    renderPane();
    await settleCatalog();
    await runAll();
    expect(screen.getByText('No scenario changes')).toBeDefined();
  });

  it('reveals the editor and, once run, both explanations beneath it; the chevron points at the detail only while open', async () => {
    renderPane();
    await settleCatalog();
    await runAll();

    const chevron = within(row('Minor')).getByRole('button', { name: 'details of Minor' });
    expect(chevron.getAttribute('aria-controls')).toBeNull();
    fireEvent.click(chevron);

    expect(chevron.getAttribute('aria-expanded')).toBe('true');
    const detail = document.getElementById(chevron.getAttribute('aria-controls')!)!;
    expect(detail).not.toBeNull();
    expect(within(detail).getByLabelText('scenario name')).toBeDefined();
    expect(within(detail).getByLabelText('scenario model')).toBeDefined();
    expect(within(detail).getByRole('group', { name: 'why the live rule was satisfied' })).toBeDefined();
    expect(within(detail).getByRole('group', { name: 'why the draft was satisfied' })).toBeDefined();
  });

  it('drops a row back to unevaluated when its model is edited, but not when it is renamed', async () => {
    renderPane();
    await settleCatalog();
    await runAll();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));

    fireEvent.change(screen.getByLabelText('scenario name'), { target: { value: 'Teenager' } });
    expect(within(row('Teenager')).getAllByText('yes')).toHaveLength(2);

    fireEvent.change(screen.getByLabelText('scenario model'), { target: { value: '{ "age": 17, "isActive": true }' } });
    expect(within(row('Teenager')).getAllByText('–')).toHaveLength(2);
  });

  it('clones beneath the source, deletes, and Reset reloads from the store', async () => {
    renderPane();
    await settleCatalog();
    const names = () => [...document.querySelectorAll('tr.scenario')].map((r) => r.getAttribute('aria-label'));

    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'clone Minor' }));
    expect(names().slice(0, 3)).toEqual(['Active adult, 3 orders', 'Minor', 'Minor (copy)']);

    fireEvent.click(within(row('Minor (copy)')).getByRole('button', { name: 'delete Minor (copy)' }));
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'delete Minor' }));
    expect(names()).not.toContain('Minor');

    fireEvent.click(screen.getByRole('button', { name: 'Reset' }));
    await settleCatalog();
    expect(names()).toEqual(['Active adult, 3 orders', 'Minor', 'Dormant account', 'New, no orders']);
  });

  it('adds a scenario, opened for editing', async () => {
    renderPane();
    await settleCatalog();
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    const added = row('Scenario 5');
    expect(within(added).getByRole('button', { name: 'details of Scenario 5' }).getAttribute('aria-expanded')).toBe('true');
    expect(screen.getByLabelText('scenario name')).toBeDefined();
  });

  it('loads the rule’s scenarios from the store', async () => {
    const api = client();
    renderPane(api);
    await settleCatalog();
    expect(api.listScenarios).toHaveBeenCalledWith('can-checkout');
    expect(row('Active adult, 3 orders')).toBeDefined();
  });

  it('renders an empty table with a hint when the host has no scenario store', async () => {
    const api = client();
    (api.listScenarios as ReturnType<typeof vi.fn>).mockResolvedValue([]);
    renderPane(api);
    await settleCatalog();
    expect(screen.getByText('No scenarios. Add one, or reset to reload.')).toBeDefined();
    fireEvent.click(screen.getByRole('button', { name: 'Run all' }));
    expect(api.evaluate).not.toHaveBeenCalled();
  });

  it('saves a renamed scenario when its editor loses focus, at its version', async () => {
    const api = client();
    renderPane(api);
    await settleCatalog();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));
    const name = screen.getByLabelText('scenario name');
    fireEvent.change(name, { target: { value: 'Teenager' } });
    expect(api.putScenario).not.toHaveBeenCalled();
    fireEvent.blur(name);
    await waitFor(() => expect(api.putScenario).toHaveBeenCalledWith('can-checkout', 's2',
      expect.objectContaining({ name: 'Teenager', baseVersion: 1 })));
  });

  it('adds and deletes through the store', async () => {
    const api = client();
    renderPane(api);
    await settleCatalog();
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(api.putScenario).toHaveBeenCalledWith('can-checkout', expect.any(String),
      expect.objectContaining({ name: 'Scenario 5', baseVersion: 0 })));
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'delete Minor' }));
    await waitFor(() => expect(api.deleteScenario).toHaveBeenCalledWith('can-checkout', 's2', 1));
  });

  it('flags a row the store says changed elsewhere, and Reset reloads it', async () => {
    const api = client();
    (api.putScenario as ReturnType<typeof vi.fn>).mockResolvedValue({ outcome: 'conflict', currentVersion: 3 });
    renderPane(api);
    await settleCatalog();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));
    const name = screen.getByLabelText('scenario name');
    fireEvent.change(name, { target: { value: 'Teenager' } });
    fireEvent.blur(name);
    await waitFor(() => expect(screen.getByText(/changed elsewhere/)).toBeDefined());
    fireEvent.click(screen.getByRole('button', { name: 'Reset' }));
    await waitFor(() => expect(api.listScenarios).toHaveBeenCalledTimes(2));
  });

  it('sends a stored row’s expected verdict and source decision back when it is renamed', async () => {
    const api = client();
    (api.listScenarios as ReturnType<typeof vi.fn>).mockResolvedValue(
      [{ ...SEEDS[1], expectedSatisfied: false, sourceDecisionId: 'd-9' }]);
    renderPane(api);
    await settleCatalog();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));
    const name = screen.getByLabelText('scenario name');
    fireEvent.change(name, { target: { value: 'Teenager' } });
    fireEvent.blur(name);
    await waitFor(() => expect(api.putScenario).toHaveBeenCalledWith('can-checkout', 's2',
      expect.objectContaining({ name: 'Teenager', expectedSatisfied: false, sourceDecisionId: 'd-9' })));
  });

  it('writes an edit made while the row was still being created, once the create has landed', async () => {
    // The create PUT is held open; the user names the row and tabs out before it answers.
    const api = client();
    let land: (value: unknown) => void = () => undefined;
    (api.putScenario as ReturnType<typeof vi.fn>)
      .mockImplementationOnce(() => new Promise((resolve) => { land = resolve; }))
      .mockResolvedValue({ outcome: 'saved', version: 2 });
    renderPane(api);
    await settleCatalog();
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(api.putScenario).toHaveBeenCalledTimes(1));
    const name = screen.getByLabelText('scenario name');
    fireEvent.change(name, { target: { value: 'Named early' } });
    fireEvent.blur(name);
    expect(api.putScenario).toHaveBeenCalledTimes(1);
    await act(async () => { land({ outcome: 'saved', version: 1 }); });
    await waitFor(() => expect(api.putScenario).toHaveBeenCalledWith('can-checkout', expect.any(String),
      expect.objectContaining({ name: 'Named early', baseVersion: 1 })));
  });

  it('deletes a row removed while it was still being created, once the create has landed', async () => {
    const api = client();
    let land: (value: unknown) => void = () => undefined;
    (api.putScenario as ReturnType<typeof vi.fn>)
      .mockImplementationOnce(() => new Promise((resolve) => { land = resolve; }));
    renderPane(api);
    await settleCatalog();
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(api.putScenario).toHaveBeenCalledTimes(1));
    const id = (api.putScenario as ReturnType<typeof vi.fn>).mock.calls[0]![1] as string;
    fireEvent.click(within(row('Scenario 5')).getByRole('button', { name: 'delete Scenario 5' }));
    expect(screen.queryByRole('row', { name: 'Scenario 5' })).toBeNull();
    expect(api.deleteScenario).not.toHaveBeenCalled();
    await act(async () => { land({ outcome: 'saved', version: 1 }); });
    await waitFor(() => expect(api.deleteScenario).toHaveBeenCalledWith('can-checkout', id, 1));
  });

  it('adds two scenarios from two quick clicks', async () => {
    renderPane();
    await settleCatalog();
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(row('Scenario 5')).toBeDefined();
    expect(row('Scenario 6')).toBeDefined();
  });

  it('clears a row’s violations when its model is edited, and drops them with a deleted row', async () => {
    renderPane();
    await settleCatalog();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));
    const model = () => within(row('Minor: details')).getByLabelText('scenario model');
    fireEvent.change(model(), { target: { value: '{ "age": "sixteen", "isActive": true }' } });
    await runAll();
    expect(screen.getByText('$.age: expected integer, got string')).toBeDefined();

    fireEvent.change(model(), { target: { value: '{ "age": 16, "isActive": true }' } });
    expect(screen.queryByText('$.age: expected integer, got string')).toBeNull();

    fireEvent.change(model(), { target: { value: '{ "age": "sixteen", "isActive": true }' } });
    await runAll();
    expect(screen.getByText('$.age: expected integer, got string')).toBeDefined();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'delete Minor' }));
    expect(screen.queryByText('$.age: expected integer, got string')).toBeNull();
  });

  it('blocks a scenario that breaks the model schema, shows its violations, and still runs the others', async () => {
    const api = client();
    renderPane(api);
    await settleCatalog();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));
    fireEvent.change(screen.getByLabelText('scenario model'), { target: { value: '{ "age": "sixteen", "isActive": true }' } });

    await runAll();

    expect(screen.getByText('$.age: expected integer, got string')).toBeDefined();
    expect(api.evaluate).not.toHaveBeenCalledWith(expect.objectContaining({ model: { age: 'sixteen', isActive: true } }));
    expect(within(row('Dormant account')).getAllByText('no')).toHaveLength(2);
  });

  it('shows a side’s failure in place of its explanation, without hiding the other side', async () => {
    const api = client();
    (api.evaluateRule as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('Request failed (403).'));
    renderPane(api);
    await settleCatalog();
    await runAll();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));
    expect(screen.getAllByText('Request failed (403).').length).toBeGreaterThan(0);
    expect(screen.getByRole('group', { name: 'why the draft was satisfied' })).toBeDefined();
  });
});

/** The explanation's disclosure semantics, carried over from the single-sample pane. */
describe('ScenarioPane justification', () => {
  const composed = (): EvaluationResult => ({
    ...result(true, 'customer is active'),
    explanation: { assertions: ['customer is active'], underlying: [{ assertions: ['account is open'], underlying: [] }] },
  });

  const opened = async () => {
    renderPane(client({ live: composed, draft: composed }));
    await settleCatalog();
    await runAll();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));
    const live = screen.getByRole('group', { name: 'why the live rule was satisfied' });
    return within(live);
  };

  it('names the disclosure by the assertion whose causes it hides, and points at them only while showing', async () => {
    const live = await opened();
    const toggle = live.getByRole('button', { name: 'causes of customer is active' });
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(document.getElementById(toggle.getAttribute('aria-controls')!)).toBe(live.getByRole('group', { name: 'customer is active' }));
    fireEvent.click(toggle);
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(toggle.getAttribute('aria-controls')).toBeNull();
  });
});

describe('ScenarioPane model type', () => {
  it('runs every scenario against the model type it is given', async () => {
    const apiClient = client({
      catalog: { ...catalog, modelTypes: { ...catalog.modelTypes, order: catalog.modelTypes!.customer! } },
    });
    renderPane(apiClient, 'order');
    await settleCatalog();
    await runAll();
    const evaluate = apiClient.evaluate as ReturnType<typeof vi.fn>;
    expect(evaluate).toHaveBeenCalled();
    for (const call of evaluate.mock.calls) expect(call[0]).toEqual(expect.objectContaining({ modelType: 'order' }));
  });
});
