import { describe, it, expect } from 'vitest';
import { analyseLeaf, printLeaf, type LeafAst, type LeafScope } from '../src/expression/index.js';
import type { JsonSchema } from '../src/index.js';
import { CUSTOMER } from './expression/fixture-schema.js';

/**
 * The exact text, range and severity of every diagnostic the leaf checker emits. The corpus only
 * checks message fragments, and `expression-check.test.ts` mostly checks codes, so a mutation
 * that blanks or rewords a message survived both (Stryker run 2026-10-03). These pin the text an
 * author actually reads in the editor.
 */
const model: JsonSchema = {
  ...CUSTOMER,
  properties: {
    ...CUSTOMER.properties,
    address: { type: ['object', 'null'], properties: { city: { type: 'string' } } },
    tier: { type: 'string', enum: ['gold', 'silver'] },
    tags: { type: 'array', items: { type: 'string' } },
    ratio: { type: 'number', format: 'single' },
  },
};

const scope: LeafScope = {
  modelName: 'customer',
  model,
  parameters: { minAge: { type: 'integer' }, vip: { type: 'number' }, region: { type: 'string' }, strict: { type: 'boolean' } },
  vars: {},
};

const check = (text: string) => analyseLeaf(text, scope);
const problems = (text: string) => check(text).problems;
const rootType = (text: string) => { const a = check(text); return a.types.get(a.ast!); };
const facts = (text: string) => check(text).facts.map((f) => [printLeaf(f.node), f.type, f.from]);

const error = (message: string, from: number, to: number, code = 'ExpressionTypeMismatch') => ({ code, message, from, to });
const warning = (message: string, from: number, to: number) => ({ ...error(message, from, to), warning: true });
const defaulted = (kind: string, from: number, to: number) =>
  warning(`no model field fixes the type of this expression; assuming ${kind}`, from, to);

describe('leaf checker messages', () => {
  describe('names', () => {
    it('names an unknown parameter with its @', () => {
      expect(problems('@nope > 1')).toEqual([error("unknown parameter '@nope'", 0, 5, 'UnknownField')]);
    });

    it('names the model an unknown field is missing from', () => {
      expect(problems('nope > 1')).toEqual([error("'nope' is not a field of customer", 0, 4, 'UnknownField')]);
    });

    it('describes the non-object a member was read from', () => {
      expect(problems('age.nope > 1')).toEqual([error("'nope' is not a field of int", 4, 8, 'UnknownField')]);
      expect(problems('isActive.nope')).toEqual([error("'nope' is not a field of a condition", 9, 13, 'UnknownField')]);
    });

    it('reports only the unknown name when a member or method hangs off it', () => {
      expect(problems('nope.x > 1')).toEqual([error("'nope' is not a field of customer", 0, 4, 'UnknownField')]);
      expect(problems('nope.any(x => x)')).toEqual([error("'nope' is not a field of customer", 0, 4, 'UnknownField')]);
    });

    it('reads a member of a nested object and lifts its nullability', () => {
      expect(problems('address.city == "x"')).toEqual([]);
    });

    it('lists the collection methods when a member is read off a collection', () => {
      expect(problems('orders.status')[0]).toEqual(error(
        "'orders' is a collection — use .where(…), .any(…), .all(…), .count(), .sum(…), .min(…) or .max(…) on it",
        7, 13, 'UnknownMethod'));
    });
  });

  describe('unary operators', () => {
    it('refuses ! on a non-condition', () => {
      expect(problems('!age')).toEqual([error("'!' needs a condition; this is int", 0, 4)]);
      expect(rootType('!age')).toBe('bool');
    });

    it('refuses - on a non-number', () => {
      expect(problems('-isActive')).toEqual([error("'-' needs a number; this is a condition", 0, 9)]);
    });

    it('keeps the operand type through unary minus', () => {
      expect(problems('-age > 1')).toEqual([]);
      expect(facts('-age > 1')).toEqual([['1', 'int', '-age'], ['-age > 1', 'bool', null]]);
    });
  });

  describe('equalsIgnoreCase', () => {
    it('accepts a string argument on a string', () => {
      expect(problems('country.equalsIgnoreCase("se")')).toEqual([]);
      expect(rootType('country.equalsIgnoreCase("se")')).toBe('bool');
    });

    it('refuses a non-string target at the method name', () => {
      expect(problems('age.equalsIgnoreCase("x")')).toEqual([error("'equalsIgnoreCase' needs a string; this is int", 4, 20, 'UnknownMethod')]);
    });

    it.each([
      ['country.equalsIgnoreCase()'],
      ['country.equalsIgnoreCase(x => x)'],
      ['country.equalsIgnoreCase("a", "b")'],
    ])('refuses %s as not one string argument', (text) => {
      expect(problems(text)).toEqual([error("'equalsIgnoreCase' takes one string argument", 8, 24, 'UnknownMethod')]);
    });

    it('refuses a typed non-string argument by its type', () => {
      expect(problems('country.equalsIgnoreCase(age)')).toEqual([error("'equalsIgnoreCase' expects a string; this is int", 25, 28)]);
    });

    it('refuses an untyped number without also defaulting it', () => {
      expect(problems('country.equalsIgnoreCase(1)')).toEqual([error("'equalsIgnoreCase' expects a string; this is a number", 25, 26)]);
      expect(problems('country.equalsIgnoreCase(@minAge)')).toEqual([error("'equalsIgnoreCase' expects a string; this is a number", 25, 32)]);
    });
  });

  describe('collection methods', () => {
    it('refuses a collection method on a non-collection', () => {
      expect(problems('age.count() > 1')[0]).toEqual(error("'.count()' needs a collection; this is int", 4, 9, 'UnknownMethod'));
    });

    it('lists every collection method for an unknown one', () => {
      expect(problems('orders.frob() > 1')).toEqual([
        error("unknown method 'frob'; the collection methods are where, any, all, count, sum, min, max", 7, 11, 'UnknownMethod'),
      ]);
    });

    it.each(['where', 'any', 'all', 'sum', 'min', 'max'])('knows %s as a collection method', (method) => {
      expect(problems(`orders.${method}()`)[0]).toEqual(error(`'${method}' takes a lambda: ${method}(x => …)`, 7, 7 + method.length, 'UnknownMethod'));
    });

    it('types count() as int and refuses an argument to it', () => {
      expect(problems('orders.count(o => o) > 1')).toEqual([error("'count()' takes no arguments — filter with .where(…) first", 13, 19, 'UnknownMethod')]);
      expect(facts('orders.count() > 1')).toEqual([['1', 'int', 'orders.count()'], ['orders.count() > 1', 'bool', null]]);
    });

    it('refuses a non-lambda argument', () => {
      expect(problems('orders.sum(1) > 1')).toEqual([error("'sum' takes a lambda: sum(x => …)", 7, 10, 'UnknownMethod')]);
    });

    it('refuses a non-condition lambda for where, any and all', () => {
      expect(problems('orders.any(o => o.daysSinceShipped)')).toEqual([error("'any' expects a condition; this is int", 16, 34)]);
      expect(problems('orders.all(o => o.total)')).toEqual([error("'all' expects a condition; this is decimal", 16, 23)]);
    });

    it('refuses a non-number lambda for sum, min and max', () => {
      expect(problems('orders.min(o => o.status) > 1')[0]).toEqual(error("'min' expects a number; this is string", 16, 24));
      expect(problems('orders.max(o => o.total > 1) > 1')[0]).toEqual(error("'max' expects a number; this is a condition", 16, 27));
    });

    it('types any/all over a nullable collection as a nullable condition, and where as the collection', () => {
      expect(rootType('orders.all(o => o.total > 1)')).toBe('bool?');
      expect(rootType('orders.any(o => o.total > 1)')).toBe('bool?');
      expect(rootType('tags.any(t => t == "a")')).toBe('bool');
      expect(facts('orders.where(o => o.total > 1).count() > 0')).toEqual([
        ['1', 'decimal', 'o.total'],
        ['0', 'int', 'orders.where(o => o.total > 1).count()'],
        ['orders.where(o => o.total > 1).count() > 0', 'bool', null],
      ]);
    });

    it('lifts an aggregate over a nullable collection to nullable', () => {
      const analysis = check('orders.min(o => o.daysSinceShipped) > 1');
      expect(analysis.types.get((analysis.ast as unknown as { left: LeafAst }).left)).toBe('int?');
      expect(facts('orders.min(o => o.daysSinceShipped) > 1')).toEqual([
        ['1', 'int', 'orders.min(o => o.daysSinceShipped)'], ['orders.min(o => o.daysSinceShipped) > 1', 'bool', null],
      ]);
    });

    it('defaults an aggregate of a bare literal', () => {
      expect(problems('orders.max(o => 1) > 1')).toEqual([defaulted('int', 16, 17)]);
    });
  });

  describe('logical operators', () => {
    it.each([
      ['isActive && 1', '&&', 12, 13],
      ['1 || isActive', '||', 0, 1],
    ])('refuses an untyped number beside %s', (text, op, from, to) => {
      expect(problems(text)).toEqual([
        error(`'${op}' needs conditions on both sides; this is a number`, from, to),
        defaulted('int', from, to),
      ]);
    });

    it('refuses a typed non-condition by its type', () => {
      expect(problems('age || isActive')).toEqual([error("'||' needs conditions on both sides; this is int", 0, 3)]);
      expect(problems('isActive && country')).toEqual([error("'&&' needs conditions on both sides; this is string", 12, 19)]);
    });

    it('accepts boolean parameters and fields', () => {
      expect(problems('isActive && @strict')).toEqual([]);
    });
  });

  describe('equality', () => {
    it('warns that a non-nullable side is never null', () => {
      expect(problems('age == null')).toEqual([warning("'age' is never null", 0, 3)]);
      expect(problems('null != isActive')).toEqual([warning("'isActive' is never null", 8, 16)]);
      expect(problems('1 == null')).toEqual([warning("'1' is never null", 0, 1), defaulted('int', 0, 1)]);
    });

    it('says nothing when a nullable side is compared with null', () => {
      expect(problems('country == null')).toEqual([]);
      expect(problems('null != country')).toEqual([]);
    });

    it('refuses comparing two different non-numeric types', () => {
      expect(problems('isActive == "x"')).toEqual([error('comparing a condition with string', 0, 15)]);
      expect(problems('country != isActive')).toEqual([error('comparing string with a condition', 0, 19)]);
    });

    it('lists the enum members a string literal is not one of', () => {
      expect(problems('tier == "bronze"')).toEqual([error('not one of "gold", "silver"', 8, 16)]);
      expect(problems('tier == "gold"')).toEqual([]);
    });

    it('refuses comparing a non-numeric type with a number from either side', () => {
      expect(problems('isActive == 1')).toEqual([error('comparing a condition with a number', 0, 13)]);
      expect(problems('1 == isActive')).toEqual([error('comparing a condition with a number', 0, 13)]);
      expect(problems('country == age')).toEqual([error('comparing string with a number', 0, 14)]);
    });

    it('types the boolean literals as conditions', () => {
      expect(problems('isActive == true')).toEqual([]);
      expect(problems('false != isActive')).toEqual([]);
      expect(problems('age == true')).toEqual([error('comparing a condition with a number', 0, 11)]);
    });

    it('types a literal compared for equality from the field', () => {
      expect(facts('age == 1')).toEqual([['1', 'int', 'age'], ['age == 1', 'bool', null]]);
    });
  });

  describe('numeric operators', () => {
    it('refuses a non-number on either side of a comparison', () => {
      expect(problems('age < "x"')).toEqual([error("'<' compares numbers; this is string", 6, 9)]);
      expect(problems('"x" > age')).toEqual([error("'>' compares numbers; this is string", 0, 3)]);
      expect(problems('isActive >= age')).toEqual([error("'>=' compares numbers; this is a condition", 0, 8)]);
    });

    it('refuses a non-number in arithmetic', () => {
      expect(problems('age + "x" > 1')).toEqual([error("'+' needs numbers; this is string", 6, 9)]);
    });

    it('refuses joining two numeric kinds that would lose precision', () => {
      expect(problems('creditLimit + score > 1')).toEqual([
        error('cannot compare decimal with double without losing precision; use a registered spec for this comparison', 0, 19),
      ]);
      expect(problems('ratio > creditLimit')).toEqual([
        error('cannot compare float with decimal without losing precision; use a registered spec for this comparison', 0, 19),
      ]);
    });

    it('refuses a number parameter against an integral field', () => {
      expect(problems('@vip > age')).toEqual([error('cannot use a number parameter with int without losing precision', 0, 10)]);
    });

    it('types a parameter from the field it is compared with', () => {
      expect(facts('age > @minAge')).toEqual([['@minAge', 'int', 'age'], ['age > @minAge', 'bool', null]]);
      expect(facts('ratio > 1')).toEqual([['1', 'float', 'ratio'], ['ratio > 1', 'bool', null]]);
    });

    it('lets an integer parameter take any integral or decimal anchor', () => {
      expect(facts('points > @minAge')).toEqual([['@minAge', 'long', 'points'], ['points > @minAge', 'bool', null]]);
      expect(facts('creditLimit > @minAge')).toEqual([['@minAge', 'decimal', 'creditLimit'], ['creditLimit > @minAge', 'bool', null]]);
    });

    it('lets a number parameter take any fractional anchor', () => {
      expect(facts('score > @vip')).toEqual([['@vip', 'double', 'score'], ['score > @vip', 'bool', null]]);
      expect(facts('ratio > @vip')).toEqual([['@vip', 'float', 'ratio'], ['ratio > @vip', 'bool', null]]);
      expect(facts('creditLimit > @vip')).toEqual([['@vip', 'decimal', 'creditLimit'], ['creditLimit > @vip', 'bool', null]]);
    });

    it('widens a fractional literal against an integral field to decimal', () => {
      expect(facts('points > 1.5')).toEqual([['1.5', 'decimal', 'points'], ['points > 1.5', 'bool', null]]);
    });

    it('joins two typed sides to the wider kind', () => {
      expect(facts('(age + 1) > (points + 1)')).toEqual([
        ['1', 'int', 'age'], ['1', 'long', 'points'], ['age + 1 > points + 1', 'bool', null],
      ]);
      expect(facts('(age + 1) + (score + 1) > 1')).toEqual([
        ['1', 'int', 'age'], ['1', 'double', 'score'], ['1', 'double', 'age + 1 + score + 1'], ['age + 1 + score + 1 > 1', 'bool', null],
      ]);
    });

    it('defaults an unanchored integer to int and a fractional one to decimal', () => {
      expect(problems('@minAge + 1 > 1')).toEqual([defaulted('int', 0, 7)]);
      expect(facts('@minAge + 1 > 1')).toEqual([['@minAge', 'int', null], ['1', 'int', null], ['1', 'int', null], ['@minAge + 1 > 1', 'bool', null]]);
      expect(problems('1.5 + 1 > 1')).toEqual([defaulted('decimal', 0, 3)]);
      expect(problems('@minAge > 1.5')).toEqual([defaulted('decimal', 0, 7)]);
    });

    it('merges an integer and a number parameter to decimal', () => {
      expect(problems('@minAge / @vip > 1')).toEqual([defaulted('decimal', 0, 7)]);
      expect(facts('@minAge / @vip > 1')).toEqual([['@minAge', 'decimal', null], ['@vip', 'decimal', null], ['1', 'decimal', null], ['@minAge / @vip > 1', 'bool', null]]);
    });

    it('warns that integer division truncates, typed or not, and not for fractional division', () => {
      const truncates = 'integer division truncates; compare against a fractional value to keep the remainder';
      expect(problems('age / 2 > 1')).toEqual([warning(truncates, 0, 7)]);
      expect(problems('1 / 2 > 1')).toEqual([warning(truncates, 0, 5), defaulted('int', 0, 1)]);
      expect(problems('score / 2 > 1')).toEqual([]);
      expect(problems('1.0 / 2 > 1')).toEqual([defaulted('decimal', 0, 3)]);
    });

    it('does not treat a non-zero literal divisor as division by zero', () => {
      expect(problems('creditLimit / 0.5 > 1')).toEqual([]);
    });
  });

  describe('the leaf as a whole', () => {
    it.each([
      ['age', 'int', 3],
      ['score', 'double', 5],
      ['country', 'string', 7],
      ['orders', 'a collection of object', 6],
    ])('refuses a leaf that is %s', (text, described, to) => {
      expect(problems(text)).toEqual([error(`a leaf must be a condition; this is ${described}`, 0, to)]);
    });

    it('accepts a boolean parameter and a string parameter compared with a string', () => {
      expect(problems('@strict')).toEqual([]);
      expect(problems('@region == "x"')).toEqual([]);
      expect(rootType('@region == "x"')).toBe('bool');
    });

    it('marks an analysis with only warnings as valid and one with an error as not', () => {
      expect(check('age == null').valid).toBe(true);
      expect(check('!age').valid).toBe(false);
    });

    it('returns the parse problem alone and no types when the text does not parse', () => {
      const analysis = check('age >');
      expect(analysis.valid).toBe(false);
      expect(analysis.ast).toBeUndefined();
      expect(analysis.types.size).toBe(0);
      expect(analysis.facts).toEqual([]);
      expect(analysis.problems).toHaveLength(1);
    });
  });
});
