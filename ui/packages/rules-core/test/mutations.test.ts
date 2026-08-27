import { describe, it, expect } from 'vitest';
import { RuleEditorStore } from '../src/editor.js';
import {
  N_QUANTIFIER_KINDS, setBinaryOperator, setQuantifierCollection, setQuantifierKind, setQuantifierN,
} from '../src/mutations.js';
import { HIGHER_ORDER_KEYS, type BinaryNode, type HigherOrderNode, type RuleDocument } from '../src/document.js';

function storeWith(rule: RuleDocument['rule']): RuleEditorStore {
  return new RuleEditorStore({ rule });
}

describe('setBinaryOperator', () => {
  it('swaps the operator key while keeping operands and decoration', () => {
    const store = storeWith({
      name: 'pair', and: [{ spec: 'a' }, { spec: 'b' }],
    });
    setBinaryOperator(store, '$.rule', store.getState().document.rule as BinaryNode, 'orElse');
    expect(store.getState().document.rule).toEqual({
      name: 'pair', orElse: [{ spec: 'a' }, { spec: 'b' }],
    });
  });

  it('does nothing when the operator is unchanged', () => {
    const store = storeWith({ and: [{ spec: 'a' }, { spec: 'b' }] });
    const before = store.getState().document;
    setBinaryOperator(store, '$.rule', before.rule as BinaryNode, 'and');
    expect(store.getState().document).toBe(before);
    expect(store.getState().canUndo).toBe(false);
  });
});

describe('setQuantifierKind', () => {
  const quantifier = (): HigherOrderNode => ({
    asAllSatisfied: { spec: 'is-active' }, path: '$.orders', name: 'named',
  });

  it('rekinds while preserving child, collection path and decoration', () => {
    const store = storeWith(quantifier());
    setQuantifierKind(store, '$.rule', store.getState().document.rule as HigherOrderNode, 'asAnySatisfied');
    expect(store.getState().document.rule).toEqual({
      asAnySatisfied: { spec: 'is-active' }, path: '$.orders', name: 'named',
    });
  });

  it('adds n = 1 when rekinding to an N-kind without a count to carry over', () => {
    const store = storeWith(quantifier());
    setQuantifierKind(store, '$.rule', store.getState().document.rule as HigherOrderNode, 'asAtLeastNSatisfied');
    expect(store.getState().document.rule).toEqual({
      asAtLeastNSatisfied: { spec: 'is-active' }, path: '$.orders', name: 'named', n: 1,
    });
  });

  it('carries a literal count across N-kinds and drops it on leaving them', () => {
    const store = storeWith({ asNSatisfied: { spec: 'a' }, n: 3, path: '$.items' });
    setQuantifierKind(store, '$.rule', store.getState().document.rule as HigherOrderNode, 'asAtMostNSatisfied');
    expect(store.getState().document.rule).toEqual({
      asAtMostNSatisfied: { spec: 'a' }, n: 3, path: '$.items',
    });

    setQuantifierKind(store, '$.rule', store.getState().document.rule as HigherOrderNode, 'asAllSatisfied');
    expect(store.getState().document.rule).toEqual({
      asAllSatisfied: { spec: 'a' }, path: '$.items',
    });
  });

  it('replaces a @param count with 1 when rekinding, rather than carrying a reference it cannot verify', () => {
    const store = storeWith({ asNSatisfied: { spec: 'a' }, n: '@minOrders', path: '$.items' });
    setQuantifierKind(store, '$.rule', store.getState().document.rule as HigherOrderNode, 'asAtLeastNSatisfied');
    expect(store.getState().document.rule).toEqual({
      asAtLeastNSatisfied: { spec: 'a' }, n: 1, path: '$.items',
    });
  });
});

describe('setQuantifierCollection', () => {
  it('repoints the collection path and nothing else', () => {
    const store = storeWith({ asAnySatisfied: { spec: 'a' }, path: '$.orders' });
    setQuantifierCollection(store, '$.rule', store.getState().document.rule as HigherOrderNode, '$.items');
    expect(store.getState().document.rule).toEqual({ asAnySatisfied: { spec: 'a' }, path: '$.items' });
  });
});

describe('setQuantifierN', () => {
  it('updates the count', () => {
    const store = storeWith({ asNSatisfied: { spec: 'a' }, n: 1, path: '$.items' });
    setQuantifierN(store, '$.rule', store.getState().document.rule as HigherOrderNode, 5);
    expect(store.getState().document.rule).toEqual({ asNSatisfied: { spec: 'a' }, n: 5, path: '$.items' });
  });

  it('ignores a non-finite count, keeping the one already there', () => {
    const store = storeWith({ asNSatisfied: { spec: 'a' }, n: 4, path: '$.items' });
    setQuantifierN(store, '$.rule', store.getState().document.rule as HigherOrderNode, Number.NaN);
    expect(store.getState().document.rule).toEqual({ asNSatisfied: { spec: 'a' }, n: 4, path: '$.items' });
  });
});

describe('N_QUANTIFIER_KINDS', () => {
  it.each(HIGHER_ORDER_KEYS)('rekinding to %s attaches n exactly when the kind carries a count', (kind) => {
    const store = storeWith({ asNSatisfied: { spec: 'a' }, n: 3, path: '$.items' });
    setQuantifierKind(store, '$.rule', store.getState().document.rule as HigherOrderNode, kind);
    const rebuilt = store.getState().document.rule;
    expect('n' in rebuilt).toBe(N_QUANTIFIER_KINDS.includes(kind));
  });
});
