import { describe, it, expect } from 'vitest';
import { completeLeaf, elementVar, type LeafScope } from '../src/expression/index.js';
import type { JsonSchema } from '../src/index.js';

const order: JsonSchema = { type: 'object', properties: { status: { type: 'string' }, total: { type: 'number', format: 'decimal' } } };
const customer: JsonSchema = { type: 'object', properties: { age: { type: 'integer', format: 'int32' }, orders: { type: 'array', items: order } } };
const scope: LeafScope = { modelName: 'customer', model: customer, parameters: { vip: { type: 'number' } }, vars: {} };
const atEnd = (text: string) => completeLeaf(text, text.length, scope);

describe('completeLeaf', () => {
  it('offers root fields, variables and parameters at a bare word', () => {
    expect(atEnd('a')!.options.map((o) => o.label)).toEqual(['age']);
    expect(atEnd('')!.options.map((o) => o.label)).toEqual(['age', 'orders', '@vip']);
  });

  it('offers methods with insert templates on a collection', () => {
    const result = atEnd('orders.')!;
    expect(result.from).toBe(7);
    expect(result.options.map((o) => o.label)).toEqual(['where', 'any', 'all', 'count', 'sum', 'min', 'max']);
    expect(result.options[0]).toMatchObject({ kind: 'method', insert: 'where(o => o.)', caretOffset: 13 });
    const count = result.options.find((o) => o.label === 'count')!;
    expect(count).toMatchObject({ insert: 'count()', caretOffset: 7 });
  });

  it('offers element fields after a lambda variable', () => {
    const result = atEnd('orders.where(o => o.')!;
    expect(result.options.map((o) => o.label)).toEqual(['status', 'total']);
    expect(result.options[1]!.detail).toBe('decimal');
  });

  it('keeps the parent in scope inside a lambda', () => {
    expect(atEnd('orders.where(o => o.total > a')!.options.map((o) => o.label)).toEqual(['age']);
    expect(atEnd('orders.where(o => o.total > ')!.options.map((o) => o.label)).toEqual(['o', 'age', 'orders', '@vip']);
  });

  it('types the receiver through a where', () => {
    const result = atEnd('orders.where(o => o.total > 1).')!;
    expect(result.options.map((o) => o.label)).toContain('sum');
    expect(result.options.find((o) => o.label === 'sum')).toMatchObject({ insert: 'sum(o => o.)' });
  });

  it('offers parameters after @', () => {
    expect(atEnd('age > @')!.options.map((o) => o.label)).toEqual(['@vip']);
  });

  it('returns null with nothing to offer', () => {
    expect(atEnd('age.')).toBeNull();
  });
});

describe('elementVar', () => {
  it('names the leading collection, not a segment inside a lambda or method chain', () => {
    expect(elementVar('orders')).toBe('o');
    expect(elementVar('orders.where(o => o.total > 1)')).toBe('o');
  });

  it('falls back to the last segment of a plain member chain with no call', () => {
    expect(elementVar('customer.orders')).toBe('o');
  });
});
