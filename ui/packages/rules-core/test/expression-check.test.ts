import { describe, it, expect } from 'vitest';
import { analyseLeaf, printLeaf, type LeafAst, type LeafScope } from '../src/expression/index.js';
import type { JsonSchema } from '../src/index.js';

const order: JsonSchema = { type: 'object', properties: {
  status: { type: 'string', enum: ['paid', 'pending', 'refunded'] }, total: { type: 'number', format: 'decimal' }, daysSinceShipped: { type: 'integer', format: 'int32' },
} };
const customer: JsonSchema = { type: 'object', properties: {
  age: { type: 'integer', format: 'int32' }, points: { type: 'integer', format: 'int64' }, score: { type: 'number', format: 'double' },
  isActive: { type: 'boolean' }, creditLimit: { type: 'number', format: 'decimal' }, country: { type: ['string', 'null'] },
  orders: { type: ['array', 'null'], items: order }, shippedAt: { type: ['string', 'null'], format: 'date-time' },
} };
const scope: LeafScope = { modelName: 'customer', model: customer, parameters: { minAge: { type: 'integer' }, vip: { type: 'number' } }, vars: {} };

const check = (text: string) => analyseLeaf(text, scope);
const factFor = (text: string, literal: string) => check(text).facts.find((f) => printLeaf(f.node) === literal)!;

describe('analyseLeaf', () => {
  it('types a literal from the field across the comparison', () => {
    expect(factFor('creditLimit > 1000', '1000')).toMatchObject({ type: 'decimal', from: 'creditLimit' });
    expect(check('creditLimit > 1000').valid).toBe(true);
  });

  it('types a whole untyped subtree from the other side', () => {
    const analysis = check('@vip * 2 > orders.sum(o => o.total)');
    expect(analysis.valid).toBe(true);
    expect(factFor('@vip * 2 > orders.sum(o => o.total)', '@vip').type).toBe('decimal');
    expect(factFor('@vip * 2 > orders.sum(o => o.total)', '2')).toMatchObject({ type: 'decimal', from: 'orders.sum(o => o.total)' });
  });

  it('widens an integer field when a fractional literal meets it', () => {
    expect(factFor('age * 1.5 > 40', '40').type).toBe('decimal');
  });

  it('refuses decimal against double', () => {
    const [problem] = check('creditLimit > score').problems;
    expect(problem).toMatchObject({ code: 'ExpressionTypeMismatch' });
    expect(problem!.message).toContain('decimal');
    expect(problem!.message).toContain('double');
  });

  it('refuses a number parameter against an integer anchor', () => {
    expect(check('orders.count() >= @vip').problems[0]!.code).toBe('ExpressionTypeMismatch');
  });

  it('defaults an unanchored subtree with a warning', () => {
    const analysis = check('1 + 1 > 1');
    expect(analysis.valid).toBe(true);
    expect(analysis.problems.every((p) => p.warning)).toBe(true);
    expect(factFor('1 + 1 > 1', '1')).toMatchObject({ type: 'int', from: null });
  });

  it('warns on integer division', () => {
    expect(check('age / 2 > 10').problems[0]!.message).toContain('truncates');
  });

  it.each([
    ['nope > 1', 'UnknownField', 'nope'],
    ['orders.total > 1', 'UnknownMethod', 'collection'],
    ['orders.sum(o => o.nope) > 1', 'UnknownField', 'nope'],
    ['orders.first() != null', 'UnknownMethod', 'first'],
    ['age.count() > 1', 'UnknownMethod', 'collection'],
    ['orders.where(o => o.total) > 1', 'ExpressionTypeMismatch', 'condition'],
    ['orders.sum(o => o.status) > 1', 'ExpressionTypeMismatch', 'number'],
    ['country == 3', 'ExpressionTypeMismatch', 'string'],
    ['age && isActive', 'ExpressionTypeMismatch', 'condition'],
    ['age + 1', 'ExpressionTypeMismatch', 'leaf must be a condition'],
    ['@nope > 1', 'UnknownField', 'parameter'],
    ['country == "SE"', 'ok', ''],
  ])('%s → %s', (text, code, fragment) => {
    const analysis = check(text);
    if (code === 'ok') { expect(analysis.valid).toBe(true); return; }
    const problem = analysis.problems.find((p) => !p.warning)!;
    expect(problem.code).toBe(code);
    expect(problem.message).toContain(fragment);
  });

  it('ranges an unknown lambda field at the name', () => {
    expect(check('orders.sum(o => o.nope) > 1').problems[0]).toMatchObject({ from: 18, to: 22 });
  });

  it('flags a string not in the enum', () => {
    const analysis = check('orders.any(o => o.status == "shipped")');
    expect(analysis.problems[0]!.message).toContain('"paid"');
  });

  it('lifts types through a nullable path', () => {
    const analysis = check('orders.sum(o => o.total) > 1');
    const root = analysis.ast!;
    expect(analysis.types.get(root)).toBe('bool');
    expect(analysis.types.get((root as unknown as { left: LeafAst }).left)).toBe('decimal?');
  });

  // Rulings fixed on the C# side after the brief was written (LeafChecker.cs).

  it('merges differing parameter kinds to decimal with a warning', () => {
    const analysis = check('@minAge / @vip > 1');
    expect(analysis.valid).toBe(true);
    expect(analysis.problems.length).toBeGreaterThan(0);
    expect(analysis.problems.every((p) => p.warning)).toBe(true);
    expect(factFor('@minAge / @vip > 1', '@minAge').type).toBe('decimal');
    expect(factFor('@minAge / @vip > 1', '@vip').type).toBe('decimal');
  });

  it('reports exactly one problem when a where lambda body is not boolean', () => {
    const analysis = check('orders.where(o => o.total) > 1');
    const nonWarning = analysis.problems.filter((p) => !p.warning);
    expect(nonWarning).toHaveLength(1);
    expect(nonWarning[0]!.code).toBe('ExpressionTypeMismatch');
  });
});
