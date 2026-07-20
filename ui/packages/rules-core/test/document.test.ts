import { describe, it, expect } from 'vitest';
import {
  nodeKind,
  isSpecNode,
  isNotNode,
  isBinaryNode,
  binaryOperator,
  operandsOf,
  type RuleNode,
} from '../src/document.js';

describe('nodeKind', () => {
  it('identifies a spec leaf', () => {
    expect(nodeKind({ spec: 'is-positive' })).toBe('spec');
  });
  it('identifies each binary operator', () => {
    expect(nodeKind({ and: [{ spec: 'a' }, { spec: 'b' }] })).toBe('and');
    expect(nodeKind({ orElse: [{ spec: 'a' }, { spec: 'b' }] })).toBe('orElse');
  });
  it('identifies not and higher-order nodes', () => {
    expect(nodeKind({ not: { spec: 'a' } })).toBe('not');
    expect(nodeKind({ asAllSatisfied: { spec: 'a' }, path: 'items' })).toBe('asAllSatisfied');
  });
});

describe('guards', () => {
  const and: RuleNode = { and: [{ spec: 'a' }, { spec: 'b' }] };
  it('narrows spec, not, and binary nodes', () => {
    expect(isSpecNode({ spec: 'a' })).toBe(true);
    expect(isSpecNode(and)).toBe(false);
    expect(isNotNode({ not: { spec: 'a' } })).toBe(true);
    expect(isBinaryNode(and)).toBe(true);
    expect(isBinaryNode({ spec: 'a' })).toBe(false);
  });
  it('exposes the binary operator and operands', () => {
    expect(binaryOperator(and)).toBe('and');
    expect(operandsOf(and)).toHaveLength(2);
  });
});
