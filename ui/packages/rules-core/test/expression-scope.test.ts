import { describe, it, expect } from 'vitest';
import { scopeAt, typeName, withVar } from '../src/expression/index.js';
import type { Catalog, JsonSchema, RuleDocument } from '../src/index.js';

const order: JsonSchema = { type: 'object', properties: { status: { type: 'string' }, total: { type: 'number', format: 'decimal' } } };
const customer: JsonSchema = {
  type: 'object',
  properties: { age: { type: 'integer', format: 'int32' }, country: { type: ['string', 'null'] }, orders: { type: ['array', 'null'], items: order } },
};
const catalog: Catalog = { specs: [], collections: [], modelTypes: { customer } };

const document: RuleDocument = {
  parameters: { vip: { type: 'number', default: 1000 } },
  rule: { and: [{ expression: 'age > 1' }, { asAllSatisfied: { expression: 'total > 1' }, path: 'orders' }] },
};

describe('scopeAt', () => {
  it('gives the root model at the root', () => {
    const scope = scopeAt(document, '$.rule.and[0]', 'customer', catalog)!;
    expect(scope.modelName).toBe('customer');
    expect(scope.model).toBe(customer);
    expect(scope.parameters).toEqual({ vip: { type: 'number' } });
  });

  it('gives the element inside a quantifier body', () => {
    const scope = scopeAt(document, '$.rule.and[1].asAllSatisfied', 'customer', catalog)!;
    expect(scope.modelName).toBe('each of orders');
    expect(scope.model).toBe(order);
  });

  it('returns null without a schema', () => {
    expect(scopeAt(document, '$.rule.and[0]', 'customer', { specs: [], collections: [] })).toBeNull();
  });

  it('adds a lambda variable without mutating the parent', () => {
    const root = scopeAt(document, '$.rule.and[0]', 'customer', catalog)!;
    const inner = withVar(root, 'o', order, 'orders');
    expect(inner.vars.o).toEqual({ schema: order, of: 'orders' });
    expect(root.vars).toEqual({});
  });
});

describe('typeName', () => {
  it.each([
    [{ type: 'integer', format: 'int32' }, 'int'],
    [{ type: 'number', format: 'decimal' }, 'decimal'],
    [{ type: 'number' }, 'number'],
    [{ type: ['string', 'null'] }, 'string'],
    [{ type: 'array', items: order }, 'collection of object'],
  ])('%j → %s', (schema, name) => {
    expect(typeName(schema as JsonSchema)).toBe(name);
  });
});
