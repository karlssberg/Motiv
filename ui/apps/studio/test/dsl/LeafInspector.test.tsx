import { describe, it, expect, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { parse, type Catalog, type JsonSchema, type RulesApiClient } from '@motiv-rules/core';
import { scopeAt } from '@motiv-rules/core';
import { LeafInspector } from '../../src/dsl/LeafInspector.js';
import { selectScenario } from '../../src/panes/scenarioSelection.js';

const order: JsonSchema = { type: 'object', properties: { total: { type: 'number', format: 'decimal' } } };
const customer: JsonSchema = { type: 'object', properties: { age: { type: 'integer', format: 'int32' }, orders: { type: 'array', items: order } } };
const catalog: Catalog = { specs: [], collections: [], modelTypes: { customer } };

function renderAt(text: string, caret: number, evaluate = vi.fn()) {
  const result = parse(text);
  const client = { evaluate } as unknown as RulesApiClient;
  return render(
    <LeafInspector text={text} caret={caret} parseResult={result} document={result.document!} client={client} modelType="customer" ruleName="r"
      leafScope={(path) => scopeAt(result.document!, path, 'customer', catalog)} />,
  );
}

describe('LeafInspector', () => {
  it('hints when the caret is outside a leaf', () => {
    renderAt('is-active & `age > 1`', 2);
    expect(screen.getByRole('region', { name: 'expression inspector' }).textContent).toContain('Put the caret inside a backtick');
  });

  it('names the scope and reads the leaf against the selected scenario', async () => {
    selectScenario('r', { name: 'Sample', model: '{"age": 34, "orders": []}' });
    const evaluate = vi.fn().mockResolvedValue({ satisfied: true, assertions: ['age > 1 == true'], explanation: { assertions: ['age > 1 == true'], causes: [] } });
    renderAt('is-active & `age > 1`', 16, evaluate);
    expect(screen.getByText(/model:/).textContent).toContain('customer');
    await waitFor(() => expect(evaluate).toHaveBeenCalledWith({ modelType: 'customer', document: { rule: { expression: 'age > 1' } }, model: { age: 34, orders: [] } }));
    await waitFor(() => expect(screen.getByText('age > 1 == true')).toBeDefined());
  });

  it('scopes a leaf inside a quantifier body to the element', () => {
    renderAt('all in orders { `total > 1` }', 18);
    expect(screen.getByText(/model:/).textContent).toContain('each of orders');
  });

  it('wraps a leaf in its nearest enclosing quantifier, not the operand array it sits in', async () => {
    selectScenario('r', { name: 'Sample', model: '{"orders":[{"total":50}]}' });
    const evaluate = vi.fn().mockResolvedValue({ satisfied: true, assertions: ['total > 1 == true'], explanation: { assertions: ['total > 1 == true'], causes: [] } });
    renderAt('all in orders { `total > 1` & `total < 100` }', 18, evaluate);
    await waitFor(() => expect(evaluate).toHaveBeenCalledWith({
      modelType: 'customer',
      document: { rule: { asAllSatisfied: { expression: 'total > 1' }, path: 'orders' } },
      model: { orders: [{ total: 50 }] },
    }));
  });
});

describe('LeafInspector stale responses', () => {
  it('drops a response whose request was superseded while the next request was still debouncing', async () => {
    vi.useFakeTimers();
    try {
      selectScenario('r', { name: 'Sample', model: '{"age": 34, "orders": []}' });
      let resolveFirst!: (value: unknown) => void;
      const evaluate = vi.fn()
        .mockImplementationOnce(() => new Promise((resolve) => { resolveFirst = resolve; }))
        .mockResolvedValue({ satisfied: false, assertions: ['age > 100 == false'], explanation: { assertions: [], causes: [] } });
      const first = parse('is-active & `age > 1`');
      const client = { evaluate } as unknown as RulesApiClient;
      const props = (result: ReturnType<typeof parse>, text: string) => ({
        text, caret: 16, parseResult: result, document: result.document!, client, modelType: 'customer', ruleName: 'r',
        leafScope: (path: string) => scopeAt(result.document!, path, 'customer', catalog),
      });
      const view = render(<LeafInspector {...props(first, 'is-active & `age > 1`')} />);
      await act(async () => { vi.advanceTimersByTime(300); });
      expect(evaluate).toHaveBeenCalledTimes(1);

      // The leaf changes while the first request is in flight and before the second debounce fires.
      const second = parse('is-active & `age > 100`');
      view.rerender(<LeafInspector {...props(second, 'is-active & `age > 100`')} />);
      await act(async () => { resolveFirst({ satisfied: true, assertions: ['age > 1 == true'], explanation: { assertions: [], causes: [] } }); });
      expect(screen.queryByText('age > 1 == true')).toBeNull();

      await act(async () => { vi.advanceTimersByTime(300); });
      expect(screen.getByText('age > 100 == false')).toBeDefined();
    } finally {
      vi.useRealTimers();
    }
  });

  it('re-reads when the document changes outside the leaf', async () => {
    selectScenario('r', { name: 'Sample', model: '{"age": 34, "orders": []}' });
    const evaluate = vi.fn().mockResolvedValue({ satisfied: true, assertions: ['age > 1 == true'], explanation: { assertions: [], causes: [] } });
    const client = { evaluate } as unknown as RulesApiClient;
    const props = (text: string) => {
      const result = parse(text);
      return {
        text, caret: text.indexOf('age'), parseResult: result, document: result.document!, client, modelType: 'customer', ruleName: 'r',
        leafScope: (path: string) => scopeAt(result.document!, path, 'customer', catalog),
      };
    };
    const view = render(<LeafInspector {...props('is-active & `age > 1`')} />);
    await waitFor(() => expect(evaluate).toHaveBeenCalledTimes(1));
    view.rerender(<LeafInspector {...props('!is-active & `age > 1`')} />);
    await waitFor(() => expect(evaluate).toHaveBeenCalledTimes(2));
  });
});
