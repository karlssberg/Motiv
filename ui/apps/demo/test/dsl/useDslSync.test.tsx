import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { RuleEditorStore } from '@motiv/rules-core';
import { useDslSync } from '../../src/dsl/useDslSync.js';

describe('useDslSync', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('starts synced, printing the store document into the buffer', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    expect(result.current.text).toBe('is-active');
    expect(result.current.status).toBe('synced');
  });

  it('marks the buffer dirty as soon as the text changes', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-verified'));

    expect(result.current.status).toBe('dirty');
  });

  it('commits a clean parse to the store after the debounce', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-verified'));
    act(() => { vi.advanceTimersByTime(300); });

    expect(store.getState().document).toEqual({ rule: { spec: 'is-verified' } });
    expect(result.current.status).toBe('synced');
  });

  it('does not commit unparseable text and reports an error status', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('(is-active'));
    act(() => { vi.advanceTimersByTime(300); });

    expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
    expect(result.current.status).toBe('error');
  });

  it('preserves payloads from the store across a text edit', () => {
    const store = new RuleEditorStore({
      rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' },
    });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-active as "activity"'));
    act(() => { vi.advanceTimersByTime(300); });

    expect(store.getState().document.rule).toMatchObject({
      spec: 'is-active', name: 'activity', whenTrue: 'yes', whenFalse: 'no',
    });
  });

  it('reprints silently when the store changes and the buffer is clean', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));

    expect(result.current.text).toBe('is-verified');
    expect(result.current.conflict).toBe(false);
    expect(result.current.status).toBe('synced');
  });

  it('raises a conflict when the store changes while the buffer is dirty', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-recent'));
    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));

    expect(result.current.conflict).toBe(true);
    expect(result.current.text).toBe('is-recent');
  });

  it('reformat from tree discards local text and clears the conflict', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-recent'));
    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));
    act(() => result.current.reformatFromTree());

    expect(result.current.text).toBe('is-verified');
    expect(result.current.conflict).toBe(false);
    expect(result.current.status).toBe('synced');
  });

  it('keep editing dismisses the conflict but keeps the local text', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-recent'));
    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));
    act(() => result.current.keepEditing());

    expect(result.current.conflict).toBe(false);
    expect(result.current.text).toBe('is-recent');
  });

  it('format reprints the current buffer canonically', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-active   &&   is-recent'));
    act(() => { vi.advanceTimersByTime(300); });
    act(() => result.current.format());

    expect(result.current.text).toBe('is-active && is-recent');
  });

  it('does not treat its own commit as an external change', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-active && is-recent'));
    act(() => { vi.advanceTimersByTime(300); });

    expect(result.current.conflict).toBe(false);
    expect(result.current.text).toBe('is-active && is-recent');
  });

  it('ignores setErrors notifications, which are not document changes', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-recent'));
    act(() => store.setErrors([{ path: '$.rule', code: 'UnknownSpec', message: 'x' }]));

    expect(result.current.conflict).toBe(false);
  });

  it('coalesces a burst of edits into a single commit', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-a'));
    act(() => { vi.advanceTimersByTime(100); });
    act(() => result.current.setText('is-ac'));
    act(() => { vi.advanceTimersByTime(100); });
    act(() => result.current.setText('is-recent'));
    act(() => { vi.advanceTimersByTime(300); });

    expect(store.getState().document).toEqual({ rule: { spec: 'is-recent' } });
  });
});
