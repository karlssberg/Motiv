import { describe, it, expect, vi } from 'vitest';
import { RuleEditorStore, errorsForNode } from '../src/editor.js';
import { getNode } from '../src/paths.js';
import type { RuleDocument } from '../src/document.js';
import type { RuleError } from '../src/contracts.js';

const initial: RuleDocument = { rule: { spec: 'a' } };

describe('RuleEditorStore edits', () => {
  it('replaces a node and notifies subscribers', () => {
    const store = new RuleEditorStore(initial);
    const listener = vi.fn();
    store.subscribe(listener);

    store.replaceNode('$.rule', { spec: 'b' });

    expect(store.getState().document.rule).toEqual({ spec: 'b' });
    expect(listener).toHaveBeenCalledOnce();
  });

  it('wraps a node in a binary operator with a supplied sibling', () => {
    const store = new RuleEditorStore(initial);
    store.wrapInOperator('$.rule', 'and', { spec: 'b' });
    expect(store.getState().document.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }] });
  });

  it('adds and removes operands, unwrapping when only one remains', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    store.addOperand('$.rule', { spec: 'c' });
    expect(getNode(store.getState().document, '$.rule')).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });

    store.removeOperand('$.rule.and[2]');
    store.removeOperand('$.rule.and[1]');
    expect(store.getState().document.rule).toEqual({ spec: 'a' });
  });

  it('unwraps an operator node to its first operand', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    store.unwrap('$.rule');
    expect(store.getState().document.rule).toEqual({ spec: 'a' });
  });

  it('sets decoration and name', () => {
    const store = new RuleEditorStore(initial);
    store.setDecoration('$.rule', { whenTrue: 'yes', whenFalse: 'no' });
    store.setName('$.rule', 'my check');
    expect(store.getState().document.rule).toEqual({ spec: 'a', whenTrue: 'yes', whenFalse: 'no', name: 'my check' });
  });
});

describe('RuleEditorStore history', () => {
  it('undoes and redoes edits', () => {
    const store = new RuleEditorStore(initial);
    store.replaceNode('$.rule', { spec: 'b' });
    expect(store.getState().canUndo).toBe(true);

    store.undo();
    expect(store.getState().document.rule).toEqual({ spec: 'a' });
    expect(store.getState().canRedo).toBe(true);

    store.redo();
    expect(store.getState().document.rule).toEqual({ spec: 'b' });
  });
});

describe('loadDocument', () => {
  it('replaces the whole document and clears history and errors', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    store.replaceNode('$.rule', { spec: 'b' });
    store.setErrors([{ path: '$.rule', code: 'UnknownSpec', message: 'x' }]);

    store.loadDocument({ rule: { spec: 'c' } });

    const state = store.getState();
    expect(state.document).toEqual({ rule: { spec: 'c' } });
    expect(state.errors).toEqual([]);
    expect(state.canUndo).toBe(false); // a load is a fresh baseline, not an edit
    expect(state.canRedo).toBe(false);
  });

  it('notifies subscribers', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    let notified = 0;
    store.subscribe(() => { notified += 1; });

    store.loadDocument({ rule: { spec: 'b' } });

    expect(notified).toBe(1);
  });
});

describe('errorsForNode', () => {
  const errors: RuleError[] = [
    { path: '$.rule.and[0]', code: 'UnknownSpec', message: 'x' },
    { path: '$.rule.and[0].whenTrue', code: 'MixedWhenTrueFalseKinds', message: 'y' },
    { path: '$.rule.and[1]', code: 'UnknownSpec', message: 'z' },
  ];
  it('matches a node and its decoration sub-paths', () => {
    expect(errorsForNode(errors, '$.rule.and[0]').map((e) => e.code))
      .toEqual(['UnknownSpec', 'MixedWhenTrueFalseKinds']);
  });
  it('stores errors on the state and surfaces them per node', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    store.setErrors(errors);
    expect(store.getState().errors).toHaveLength(3);
    expect(errorsForNode(store.getState().errors, '$.rule.and[1]')).toHaveLength(1);
  });
});
