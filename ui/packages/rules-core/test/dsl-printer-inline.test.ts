import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';
import { printInline } from '../src/dsl/printer.js';
import type { RuleNode } from '../src/document.js';

const NODES: Array<{ label: string; node: RuleNode; text: string }> = [
  { label: 'spec', node: { spec: 'is-active' }, text: 'is-active' },
  { label: 'negation', node: { not: { spec: 'is-flagged' } }, text: '!is-flagged' },
  {
    label: 'binary',
    node: { or: [{ spec: 'a' }, { not: { spec: 'b' } }] },
    text: 'a | !b',
  },
  {
    label: 'quantifier on one line',
    node: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: 2, path: 'orders' },
    text: 'atLeast(2) in orders { is-positive }',
  },
  {
    label: 'parameter count',
    node: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: '@minOrders', path: 'orders' },
    text: 'atLeast(@minOrders) in orders { is-positive }',
  },
  {
    label: 'quantifier under an operator stays on one line',
    node: {
      and: [{ spec: 'is-active' }, { asAllSatisfied: { spec: 'is-positive' }, path: 'orders' }],
    },
    text: 'is-active & all in orders { is-positive }',
  },
  {
    label: 'looser child keeps its parentheses',
    node: { and: [{ orElse: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] },
    text: '(a || b) & c',
  },
  {
    label: 'named compound',
    node: { andAlso: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' },
    text: '(a && b) as "pair"',
  },
];

describe('printInline', () => {
  it.each(NODES)('renders $label on a single line', ({ node, text }) => {
    const printed = printInline(node);
    expect(printed).toBe(text);
    expect(printed).not.toContain('\n');
  });

  it.each(NODES)('round-trips $label through the parser', ({ node }) => {
    const result = parse(printInline(node));
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual(node);
  });

  it('preserves consecutive spaces inside a name', () => {
    const node: RuleNode = { spec: 'is-active', name: 'order  total' };
    expect(printInline(node)).toBe('is-active as "order  total"');
    expect(parse(printInline(node)).document?.rule).toEqual(node);
  });
});
