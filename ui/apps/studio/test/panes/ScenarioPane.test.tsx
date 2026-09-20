import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor, act, within } from '@testing-library/react';
import { RuleEditorStore, type Catalog, type EvaluationResult, type RulesApiClient } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { ScenarioPane } from '../../src/panes/ScenarioPane.js';

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

  it('clones beneath the source, deletes, and resets to the seeds', async () => {
    renderPane();
    await settleCatalog();
    const names = () => [...document.querySelectorAll('tr.scenario')].map((r) => r.getAttribute('aria-label'));

    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'clone Minor' }));
    expect(names().slice(0, 3)).toEqual(['Active adult, 3 orders', 'Minor', 'Minor (copy)']);

    fireEvent.click(within(row('Minor (copy)')).getByRole('button', { name: 'delete Minor (copy)' }));
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'delete Minor' }));
    expect(names()).not.toContain('Minor');

    fireEvent.click(screen.getByRole('button', { name: 'Reset' }));
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
