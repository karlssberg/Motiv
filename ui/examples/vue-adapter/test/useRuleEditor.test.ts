import { describe, it, expect } from 'vitest';
import { shallowRef } from 'vue';
import { RuleEditorStore } from '@motiv-rules/core';
import { useRuleEditor } from '../src/useRuleEditor.js';
import { inScope } from './scope.js';

describe('useRuleEditor', () => {
  it('exposes the store state and follows it', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: state, stop } = inScope(() => useRuleEditor(store));

    expect(state.value.document).toEqual({ rule: { spec: 'is-active' } });
    expect(state.value.canUndo).toBe(false);

    store.replaceNode('$.rule', { spec: 'is-verified' });

    expect(state.value.document).toEqual({ rule: { spec: 'is-verified' } });
    expect(state.value.canUndo).toBe(true);
    stop();
  });

  it('stops following when the scope ends, leaving no listener behind', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: state, stop } = inScope(() => useRuleEditor(store));

    stop();
    store.replaceNode('$.rule', { spec: 'is-verified' });

    expect(state.value.document).toEqual({ rule: { spec: 'is-active' } });
  });

  it('follows a new store when the one it was given changes', () => {
    const first = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const second = new RuleEditorStore({ rule: { spec: 'is-admin' } });
    const source = shallowRef(first);
    const { value: state, stop } = inScope(() => useRuleEditor(source));

    source.value = second;
    expect(state.value.document).toEqual({ rule: { spec: 'is-admin' } });

    second.replaceNode('$.rule', { spec: 'is-recent' });
    expect(state.value.document).toEqual({ rule: { spec: 'is-recent' } });

    // The store it let go of is no longer heard.
    first.replaceNode('$.rule', { spec: 'is-verified' });
    expect(state.value.document).toEqual({ rule: { spec: 'is-recent' } });
    stop();
  });
});
