import { describe, it, expect } from 'vitest';
import { print } from '../src/dsl/printer.js';
import type { RuleDocument } from '../src/document.js';

describe('print', () => {
  it('prints a bare spec', () => {
    expect(print({ rule: { spec: 'is-active' } })).toBe('is-active');
  });

  it('prints an expression node in backticks', () => {
    expect(print({ rule: { expression: 'n > 0' } })).toBe('`n > 0`');
  });

  it('prints negation without a space', () => {
    expect(print({ rule: { not: { spec: 'is-flagged' } } })).toBe('!is-flagged');
  });

  it('prints an n-ary operator with its operands joined', () => {
    expect(print({ rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] } }))
      .toBe('a && b && c');
  });

  it('parenthesises a looser child inside a tighter parent', () => {
    expect(print({ rule: { and: [{ or: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } }))
      .toBe('(a | b) & c');
  });

  it('does not parenthesise a tighter child inside a looser parent', () => {
    expect(print({ rule: { andAlso: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } }))
      .toBe('a & b && c');
  });

  it('prints a name as a trailing as-clause', () => {
    expect(print({ rule: { spec: 'is-active', name: 'activity' } }))
      .toBe('is-active as "activity"');
  });

  it('parenthesises a named compound so the name binds to it', () => {
    expect(print({ rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' } }))
      .toBe('(a && b) as "pair"');
  });

  it('prints an uncounted quantifier with an indented body', () => {
    expect(print({ rule: { asAllSatisfied: { spec: 'is-positive' }, path: 'orders' } }))
      .toBe('all in orders {\n    is-positive\n}');
  });

  it('prints a counted quantifier with a literal n', () => {
    expect(print({ rule: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: 3, path: 'orders' } }))
      .toBe('atLeast(3) in orders {\n    is-positive\n}');
  });

  it('prints a param reference as n', () => {
    expect(print({
      rule: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: '@minOrders', path: 'orders' },
    })).toBe('atLeast(@minOrders) in orders {\n    is-positive\n}');
  });

  it('prints parameter declarations before a blank line', () => {
    const document: RuleDocument = {
      parameters: { minOrders: { type: 'integer', default: 3 } },
      rule: { spec: 'is-active' },
    };
    expect(print(document)).toBe('param minOrders: integer = 3\n\nis-active');
  });

  it('prints a parameter without a default', () => {
    expect(print({ parameters: { label: { type: 'string' } }, rule: { spec: 'is-active' } }))
      .toBe('param label: string\n\nis-active');
  });

  it('quotes a string default and leaves a boolean bare', () => {
    expect(print({
      parameters: { a: { type: 'string', default: 'gold' }, b: { type: 'boolean', default: true } },
      rule: { spec: 'is-active' },
    })).toBe('param a: string = "gold"\nparam b: boolean = true\n\nis-active');
  });

  it('reproduces the reference document verbatim', () => {
    const document: RuleDocument = {
      parameters: { minOrders: { type: 'integer', default: 3 } },
      rule: {
        andAlso: [
          { spec: 'is-active' },
          { or: [{ spec: 'is-verified' }, { not: { spec: 'is-flagged' } }] },
          {
            asAtLeastNSatisfied: { andAlso: [{ spec: 'is-positive' }, { spec: 'is-recent' }] },
            n: '@minOrders',
            path: 'orders',
            name: 'quota',
          },
        ],
      },
    };
    expect(print(document)).toBe(
      'param minOrders: integer = 3\n' +
      '\n' +
      'is-active && (\n' +
      '    is-verified | !is-flagged\n' +
      ') && atLeast(@minOrders) in orders {\n' +
      '    is-positive && is-recent\n' +
      '} as "quota"',
    );
  });
});
