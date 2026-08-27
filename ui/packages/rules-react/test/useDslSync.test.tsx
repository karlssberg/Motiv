import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { RuleEditorStore } from '@motiv-rules/core';
import { useDslSync } from '../src/useDslSync.js';

describe('useDslSync', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('starts synced, printing the store document into the buffer', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    expect(result.current.text).toBe('is-active');
    expect(result.current.status).toBe('synced');
  });

  it('commits an edit to the store after the debounce', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-verified'));
    expect(result.current.status).toBe('dirty');

    act(() => { vi.advanceTimersByTime(300); });
    expect(store.getState().document).toEqual({ rule: { spec: 'is-verified' } });
    expect(result.current.status).toBe('synced');
  });

  it('follows external store changes, raising a conflict over a dirty buffer', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));
    expect(result.current.text).toBe('is-verified');
    expect(result.current.conflict).toBe(false);

    act(() => result.current.setText('is-recent'));
    act(() => store.replaceNode('$.rule', { spec: 'is-admin' }));
    expect(result.current.conflict).toBe(true);

    act(() => result.current.reformatFromTree());
    expect(result.current.text).toBe('is-admin');
    expect(result.current.conflict).toBe(false);
  });

  it('stops following the store and drops the pending commit on unmount', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result, unmount } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-recent'));
    unmount();
    act(() => { vi.advanceTimersByTime(300); });

    expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
  });

  it('binds a new controller when the store changes', () => {
    const first = new RuleEditorStore({ rule: { spec: 'a' } });
    const second = new RuleEditorStore({ rule: { spec: 'b' } });
    const { result, rerender } = renderHook(({ store }) => useDslSync(store), {
      initialProps: { store: first },
    });

    expect(result.current.text).toBe('a');
    rerender({ store: second });
    expect(result.current.text).toBe('b');

    act(() => second.replaceNode('$.rule', { spec: 'c' }));
    expect(result.current.text).toBe('c');
  });
});
