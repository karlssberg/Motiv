import { describe, it, expect } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { RuleEditorStore } from '@motiv-rules/core';
import { useRuleEditor } from '../src/useRuleEditor.js';

describe('useRuleEditor', () => {
  it('returns the current state and re-renders on edits', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const { result } = renderHook(() => useRuleEditor(store));

    expect(result.current.document.rule).toEqual({ spec: 'a' });
    expect(result.current.canUndo).toBe(false);

    act(() => store.replaceNode('$.rule', { spec: 'b' }));

    expect(result.current.document.rule).toEqual({ spec: 'b' });
    expect(result.current.canUndo).toBe(true);
  });

  it('returns a stable snapshot reference when nothing changed', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const { result, rerender } = renderHook(() => useRuleEditor(store));
    const first = result.current;
    rerender();
    expect(result.current).toBe(first);
  });
});

describe('useRuleEditor and the dirty flag', () => {
  it('re-renders when only the dirty flag changes', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const { result } = renderHook(() => useRuleEditor(store));
    act(() => store.replaceNode('$.rule', { spec: 'b' }));
    expect(result.current.dirty).toBe(true);

    // `markClean` changes nothing but the baseline: the document, errors and history all stand.
    // The snapshot must still be replaced, or the flag it carries would be stale.
    act(() => store.markClean());
    expect(result.current.dirty).toBe(false);
  });
});
