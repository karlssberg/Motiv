import { describe, it, expect } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';
import { RuleEditorStore } from '@motiv/rules-core';
import { RuleEditorProvider } from '../src/context.js';
import { useRuleNode } from '../src/useRuleNode.js';

function wrapper(store: RuleEditorStore) {
  return ({ children }: { children: ReactNode }) =>
    createElement(RuleEditorProvider, { store, children });
}

describe('useRuleNode', () => {
  it('returns the node at a path and its errors, reacting to edits', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    store.setErrors([{ path: '$.rule.and[0]', code: 'UnknownSpec', message: 'nope' }]);

    const { result } = renderHook(() => useRuleNode('$.rule.and[0]'), { wrapper: wrapper(store) });

    expect(result.current.node).toEqual({ spec: 'a' });
    expect(result.current.errors).toHaveLength(1);
    expect(result.current.errors[0]!.code).toBe('UnknownSpec');

    act(() => store.replaceNode('$.rule.and[0]', { spec: 'z' }));
    expect(result.current.node).toEqual({ spec: 'z' });
  });

  it('returns undefined for a missing path', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const { result } = renderHook(() => useRuleNode('$.rule.and[3]'), { wrapper: wrapper(store) });
    expect(result.current.node).toBeUndefined();
    expect(result.current.errors).toEqual([]);
  });
});
