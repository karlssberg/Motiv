import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { RuleEditorStore } from '@motiv-rules/core';
import { useDslSync } from '../src/useDslSync.js';
import { inScope } from './scope.js';

describe('useDslSync', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('starts synced, printing the store document into the buffer', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: sync, stop } = inScope(() => useDslSync(store));

    expect(sync.state.value.text).toBe('is-active');
    expect(sync.state.value.status).toBe('synced');
    stop();
  });

  it('commits an edit to the store after the debounce', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: sync, stop } = inScope(() => useDslSync(store));

    sync.setText('is-verified');
    expect(sync.state.value.status).toBe('dirty');

    vi.advanceTimersByTime(300);
    expect(store.getState().document).toEqual({ rule: { spec: 'is-verified' } });
    expect(sync.state.value.status).toBe('synced');
    stop();
  });

  it('follows external store changes, raising a conflict over a dirty buffer', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: sync, stop } = inScope(() => useDslSync(store));

    store.replaceNode('$.rule', { spec: 'is-verified' });
    expect(sync.state.value.text).toBe('is-verified');
    expect(sync.state.value.conflict).toBe(false);

    sync.setText('is-recent');
    store.replaceNode('$.rule', { spec: 'is-admin' });
    expect(sync.state.value.conflict).toBe(true);

    sync.reformatFromTree();
    expect(sync.state.value.text).toBe('is-admin');
    expect(sync.state.value.conflict).toBe(false);
    stop();
  });

  it('disconnects the controller when the scope ends, so no commit lands afterwards', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: sync, stop } = inScope(() => useDslSync(store));

    sync.setText('is-verified');
    stop();
    vi.advanceTimersByTime(300);

    expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
  });
});
