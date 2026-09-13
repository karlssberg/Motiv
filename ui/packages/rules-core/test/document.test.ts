import { describe, it, expect } from 'vitest';
import {
  nodeKind,
  isSpecNode,
  isNotNode,
  isBinaryNode,
  isLocalNode,
  binaryOperator,
  operandsOf,
  higherOrderBody,
  type RuleNode,
} from '../src/document.js';
import { childPaths } from '../src/paths.js';

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
  it('identifies a local node', () => {
    expect(nodeKind({ local: 'x' })).toBe('local');
  });
});

describe('isLocalNode', () => {
  it('is true for a local node', () => {
    expect(isLocalNode({ local: 'x' })).toBe(true);
  });
  it('is false for a non-local node', () => {
    expect(isLocalNode({ spec: 'a' })).toBe(false);
  });
});

describe('childPaths of a local node', () => {
  it('has no children', () => {
    expect(childPaths({ local: 'x' }, '$.rule')).toEqual([]);
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
  it('exposes binary operands and the higher-order body', () => {
    expect(binaryOperator(and)).toBe('and');
    expect(operandsOf(and)).toHaveLength(2);
    expect(higherOrderBody({ asNSatisfied: { spec: 'a' }, n: 2, path: 'items' }))
      .toEqual({ spec: 'a' });
  });
});
