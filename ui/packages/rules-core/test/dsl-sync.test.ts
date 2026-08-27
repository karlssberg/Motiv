import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { RuleEditorStore } from '../src/editor.js';
import { DslSyncController } from '../src/dslSync.js';

describe('DslSyncController', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  function syncOver(rule: Parameters<typeof RuleEditorStore.prototype.replaceNode>[1]) {
    const store = new RuleEditorStore({ rule });
    const sync = new DslSyncController(store);
    const disconnect = sync.connect();
    return { store, sync, disconnect };
  }

  it('starts synced, printing the store document into the buffer', () => {
    const { sync } = syncOver({ spec: 'is-active' });
    expect(sync.getState().text).toBe('is-active');
    expect(sync.getState().status).toBe('synced');
  });

  it('marks the buffer dirty as soon as the text changes, and notifies', () => {
    const { sync } = syncOver({ spec: 'is-active' });
    const listener = vi.fn();
    sync.subscribe(listener);

    sync.setText('is-verified');

    expect(sync.getState().status).toBe('dirty');
    expect(listener).toHaveBeenCalled();
  });

  it('commits a clean parse to the store after the debounce', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-verified');
    vi.advanceTimersByTime(300);

    expect(store.getState().document).toEqual({ rule: { spec: 'is-verified' } });
    expect(sync.getState().status).toBe('synced');
  });

  it('does not commit unparseable text and reports an error status', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('(is-active');
    vi.advanceTimersByTime(300);

    expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
    expect(sync.getState().status).toBe('error');
  });

  it('preserves payloads from the store across a text edit', () => {
    const { store, sync } = syncOver({ spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' });

    sync.setText('is-active as "activity"');
    vi.advanceTimersByTime(300);

    expect(store.getState().document.rule).toMatchObject({
      spec: 'is-active', name: 'activity', whenTrue: 'yes', whenFalse: 'no',
    });
  });

  it('reprints silently when the store changes and the buffer is clean', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    store.replaceNode('$.rule', { spec: 'is-verified' });

    expect(sync.getState().text).toBe('is-verified');
    expect(sync.getState().conflict).toBe(false);
    expect(sync.getState().status).toBe('synced');
  });

  it('raises a conflict when the store changes while the buffer is dirty', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-recent');
    store.replaceNode('$.rule', { spec: 'is-verified' });

    expect(sync.getState().conflict).toBe(true);
    expect(sync.getState().text).toBe('is-recent');
  });

  it('cancels the pending commit when a conflict is raised, so neither version wins yet', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-recent');
    store.replaceNode('$.rule', { spec: 'is-verified' });
    vi.advanceTimersByTime(300);

    expect(store.getState().document).toEqual({ rule: { spec: 'is-verified' } });
    expect(sync.getState().conflict).toBe(true);
    expect(sync.getState().text).toBe('is-recent');
  });

  it('keep editing re-arms the commit, so the local text lands on the next debounce', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-recent');
    store.replaceNode('$.rule', { spec: 'is-verified' });
    sync.keepEditing();
    vi.advanceTimersByTime(300);

    expect(store.getState().document).toEqual({ rule: { spec: 'is-recent' } });
    expect(sync.getState().status).toBe('synced');
  });

  it('reformat from tree kills the in-flight commit of the discarded text', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-recent');
    store.replaceNode('$.rule', { spec: 'is-verified' });
    sync.reformatFromTree();
    vi.advanceTimersByTime(300);

    expect(store.getState().document).toEqual({ rule: { spec: 'is-verified' } });
    expect(sync.getState().text).toBe('is-verified');
  });

  it('reformat from tree discards local text and clears the conflict', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-recent');
    store.replaceNode('$.rule', { spec: 'is-verified' });
    sync.reformatFromTree();

    expect(sync.getState().text).toBe('is-verified');
    expect(sync.getState().conflict).toBe(false);
    expect(sync.getState().status).toBe('synced');
  });

  it('keep editing dismisses the conflict but keeps the local text', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-recent');
    store.replaceNode('$.rule', { spec: 'is-verified' });
    sync.keepEditing();

    expect(sync.getState().conflict).toBe(false);
    expect(sync.getState().text).toBe('is-recent');
  });

  it('format reprints the current buffer canonically', () => {
    const { sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-active   &&   is-recent');
    vi.advanceTimersByTime(300);
    sync.format();

    expect(sync.getState().text).toBe('is-active && is-recent');
  });

  it('does not treat its own commit as an external change', () => {
    const { sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-active && is-recent');
    vi.advanceTimersByTime(300);

    expect(sync.getState().conflict).toBe(false);
    expect(sync.getState().text).toBe('is-active && is-recent');
  });

  it('ignores setErrors notifications, which are not document changes', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-recent');
    store.setErrors([{ path: '$.rule', code: 'UnknownSpec', message: 'x' }]);

    expect(sync.getState().conflict).toBe(false);
  });

  it('coalesces a burst of edits into a single commit', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });

    sync.setText('is-a');
    vi.advanceTimersByTime(100);
    sync.setText('is-ac');
    vi.advanceTimersByTime(100);
    sync.setText('is-recent');
    vi.advanceTimersByTime(300);

    expect(store.getState().document).toEqual({ rule: { spec: 'is-recent' } });
  });

  it('parses the buffer once per text, handing back the same parse to every reader', () => {
    const { sync } = syncOver({ spec: 'is-active' });
    sync.setText('is-recent');
    expect(sync.getState().parseResult).toBe(sync.getState().parseResult);
    expect(sync.getState().parseResult.document).toEqual({ rule: { spec: 'is-recent' } });
  });

  it('returns the identical state snapshot while nothing has changed', () => {
    const { sync } = syncOver({ spec: 'is-active' });
    expect(sync.getState()).toBe(sync.getState());
    sync.setText('is-recent');
    expect(sync.getState()).toBe(sync.getState());
  });

  it('disconnecting stops following the store and cancels the pending commit', () => {
    const { store, sync, disconnect } = syncOver({ spec: 'is-active' });

    sync.setText('is-recent');
    disconnect();
    vi.advanceTimersByTime(300);
    store.replaceNode('$.rule', { spec: 'is-verified' });

    expect(store.getState().document).toEqual({ rule: { spec: 'is-verified' } });
    expect(sync.getState().text).toBe('is-recent');
    expect(sync.getState().conflict).toBe(false);
  });

  it('can reconnect after a disconnect and resume following the store', () => {
    const { store, sync, disconnect } = syncOver({ spec: 'is-active' });

    disconnect();
    sync.connect();
    store.replaceNode('$.rule', { spec: 'is-verified' });

    expect(sync.getState().text).toBe('is-verified');
  });

  it('refuses a second connect while one is live', () => {
    const { sync } = syncOver({ spec: 'is-active' });
    expect(() => sync.connect()).toThrowError(/already connected/);
  });

  it('does not commit text set after a disconnect, even on a later timer', () => {
    const { store, sync, disconnect } = syncOver({ spec: 'is-active' });

    disconnect();
    sync.setText('is-recent');
    vi.advanceTimersByTime(300);

    expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
    expect(sync.getState().text).toBe('is-recent');
    expect(sync.getState().status).toBe('dirty');
  });

  it('a dirty buffer left from a disconnected edit commits once reconnected and re-armed', () => {
    const { store, sync, disconnect } = syncOver({ spec: 'is-active' });

    disconnect();
    sync.setText('is-recent');
    sync.connect();
    sync.keepEditing();
    vi.advanceTimersByTime(300);

    expect(store.getState().document).toEqual({ rule: { spec: 'is-recent' } });
  });

  it('keeps following external changes even after another store subscriber throws mid-commit', () => {
    const { store, sync } = syncOver({ spec: 'is-active' });
    const unsubscribe = store.subscribe(() => {
      throw new Error('a broken subscriber elsewhere');
    });

    sync.setText('is-recent');
    expect(() => vi.advanceTimersByTime(300)).toThrowError(/broken subscriber/);
    unsubscribe();

    // The interrupted commit left the buffer dirty; what must survive the throw is the
    // *following* itself — the next external change is reconciled (held apart as a conflict),
    // not silently swallowed by a self-commit guard that never reset.
    store.replaceNode('$.rule', { spec: 'is-verified' });
    expect(sync.getState().conflict).toBe(true);
    expect(sync.getState().text).toBe('is-recent');
  });
});
