import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { RuleEditorStore, type Catalog, type EvaluationResult, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { EvaluatePane } from '../../src/panes/EvaluatePane.js';

const evaluation: EvaluationResult = {
  satisfied: true,
  reason: 'customer is active',
  assertions: ['customer is active'],
  values: ['customer is active'],
  justification: 'customer is active',
  explanation: { assertions: ['customer is active'], underlying: [] },
};

const catalog: Catalog = {
  specs: [],
  collections: [],
  metadataTypes: {},
  modelTypes: {
    customer: {
      type: ['object', 'null'],
      properties: {
        age: { type: 'integer' },
        isActive: { type: 'boolean' },
        orderCount: { type: 'integer' },
      },
    },
  },
};

function client(options: { evaluate?: ReturnType<typeof vi.fn>; catalog?: Catalog } = {}): RulesApiClient {
  return {
    evaluate: options.evaluate ?? vi.fn().mockResolvedValue(evaluation),
    getCatalog: vi.fn().mockResolvedValue(options.catalog ?? catalog),
  } as unknown as RulesApiClient;
}

function renderPane(apiClient: RulesApiClient): RuleEditorStore {
  const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
  render(
    <RuleEditorProvider store={store}>
      <EvaluatePane client={apiClient} />
    </RuleEditorProvider>,
  );
  return store;
}

/** Flushes the mocked getCatalog resolution into the pane's state. */
const settleCatalog = () => act(async () => {});

describe('EvaluatePane', () => {
  it('evaluates the current document against the sample model and renders the outcome', async () => {
    const evaluate = vi.fn().mockResolvedValue(evaluation);
    const store = renderPane(client({ evaluate }));
    await settleCatalog();

    fireEvent.click(screen.getByRole('button', { name: 'Evaluate' }));

    await waitFor(() => expect(screen.getByLabelText('outcome').textContent).toContain('Satisfied'));
    expect(evaluate).toHaveBeenCalledWith(
      expect.objectContaining({ modelType: 'customer', document: store.getState().document }),
    );
    expect(screen.getByText('customer is active')).toBeDefined();
  });

  it('blocks evaluation and shows the violations when the model breaks the customer schema', async () => {
    const evaluate = vi.fn();
    renderPane(client({ evaluate }));
    await settleCatalog();

    fireEvent.change(screen.getByLabelText('sample model'), {
      target: { value: '{ "age": "thirty", "isActive": true, "orderCount": 2 }' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Evaluate' }));

    expect(await screen.findByText('$.age: expected integer, got string')).toBeDefined();
    expect(evaluate).not.toHaveBeenCalled();
  });

  it('clears the violations once a corrected model evaluates', async () => {
    const evaluate = vi.fn().mockResolvedValue(evaluation);
    renderPane(client({ evaluate }));
    await settleCatalog();

    const model = screen.getByLabelText('sample model');
    fireEvent.change(model, { target: { value: '{ "age": "thirty" }' } });
    fireEvent.click(screen.getByRole('button', { name: 'Evaluate' }));
    await screen.findByText('$.age: expected integer, got string');

    fireEvent.change(model, { target: { value: '{ "age": 30 }' } });
    fireEvent.click(screen.getByRole('button', { name: 'Evaluate' }));

    await waitFor(() => expect(evaluate).toHaveBeenCalled());
    expect(screen.queryByText('$.age: expected integer, got string')).toBeNull();
  });

  it('does not enforce schemas when the catalog carries no model type map', async () => {
    const evaluate = vi.fn().mockResolvedValue(evaluation);
    renderPane(client({ evaluate, catalog: { specs: [], collections: [] } }));
    await settleCatalog();

    fireEvent.change(screen.getByLabelText('sample model'), {
      target: { value: '{ "age": "thirty" }' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Evaluate' }));

    await waitFor(() => expect(evaluate).toHaveBeenCalled());
  });
});
