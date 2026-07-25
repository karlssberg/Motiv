import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';
import { print } from '../src/dsl/printer.js';
import type { RuleDocument } from '../src/document.js';

/** One document per node kind in rule.v1.json, plus the reference composition. */
const DOCUMENTS: Array<{ label: string; document: RuleDocument }> = [
  { label: 'spec', document: { rule: { spec: 'is-active' } } },
  { label: 'named spec', document: { rule: { spec: 'is-active', name: 'activity' } } },
  { label: 'expression', document: { rule: { expression: 'n > 0' } } },
  { label: 'not', document: { rule: { not: { spec: 'is-flagged' } } } },
  { label: 'and', document: { rule: { and: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'or', document: { rule: { or: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'xor', document: { rule: { xor: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'andAlso', document: { rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'orElse', document: { rule: { orElse: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'n-ary', document: { rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] } } },
  {
    label: 'mixed precedence',
    document: { rule: { orElse: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } },
  },
  {
    label: 'looser child parenthesised',
    document: { rule: { and: [{ orElse: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } },
  },
  {
    label: 'asAllSatisfied',
    document: { rule: { asAllSatisfied: { spec: 'is-positive' }, path: 'orders' } },
  },
  {
    label: 'asAnySatisfied',
    document: { rule: { asAnySatisfied: { spec: 'is-positive' }, path: 'orders' } },
  },
  {
    label: 'asNSatisfied',
    document: { rule: { asNSatisfied: { spec: 'is-positive' }, n: 2, path: 'orders' } },
  },
  {
    label: 'asAtLeastNSatisfied with param',
    document: {
      rule: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: '@minOrders', path: 'orders' },
    },
  },
  {
    label: 'asAtMostNSatisfied',
    document: { rule: { asAtMostNSatisfied: { spec: 'is-positive' }, n: 1, path: 'orders' } },
  },
  {
    label: 'named quantifier',
    document: {
      rule: { asAllSatisfied: { spec: 'is-positive' }, path: 'orders', name: 'quota' },
    },
  },
  {
    label: 'named compound',
    document: { rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' } },
  },
  {
    label: 'named negation',
    document: { rule: { not: { spec: 'is-flagged' }, name: 'unflagged' } },
  },
  {
    label: 'negated compound',
    document: { rule: { not: { or: [{ spec: 'a' }, { spec: 'b' }] } } },
  },
  {
    label: 'same-operator nesting',
    document: { rule: { and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } },
  },
  {
    label: 'expressions under an operator',
    document: { rule: { andAlso: [{ expression: 'n > 0' }, { not: { expression: 'm < 1' } }] } },
  },
  {
    label: 'quantifier inside a group inside a quantifier',
    document: {
      rule: {
        asAllSatisfied: {
          and: [
            { orElse: [{ spec: 'a' }, { asAnySatisfied: { spec: 'b' }, path: 'tags' }] },
            { spec: 'c' },
          ],
        },
        path: 'orders',
      },
    },
  },
  {
    label: 'named compound wrapping a quantifier',
    document: {
      rule: {
        andAlso: [{ spec: 'a' }, { asAllSatisfied: { spec: 'b' }, path: 'orders' }],
        name: 'both',
      },
    },
  },
  {
    label: 'parameters',
    document: {
      parameters: {
        minOrders: { type: 'integer', default: 3 },
        label: { type: 'string', default: 'gold' },
        strict: { type: 'boolean', default: false },
        ratio: { type: 'number' },
        offset: { type: 'integer', default: -2 },
      },
      rule: { spec: 'is-active' },
    },
  },
  {
    label: 'reference composition',
    document: {
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
    },
  },
];

describe('DSL round-trip', () => {
  it.each(DOCUMENTS)('parse(print(doc)) preserves $label', ({ document }) => {
    const text = print(document);
    const result = parse(text);
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual(document);
  });

  it.each(DOCUMENTS)('print is idempotent for $label', ({ document }) => {
    const once = print(document);
    const twice = print(parse(once).document!);
    expect(twice).toBe(once);
  });

  it('every parsed node has a span', () => {
    for (const { document } of DOCUMENTS) {
      const result = parse(print(document));
      expect(result.spans.some((span) => span.path === '$.rule')).toBe(true);
    }
  });
});
