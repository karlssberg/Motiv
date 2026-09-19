import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
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
});
