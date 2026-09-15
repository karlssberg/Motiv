import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  Workspace, isEmptyTab, referencesOf, referenceStatuses, tabIdOf, TABS_KEY,
} from '../../src/shell/workspace.js';

describe('Workspace', () => {
  beforeEach(() => { window.sessionStorage.clear(); });

  it('opens a document in a new tab, with a fresh store, and makes it active', () => {
    const workspace = new Workspace();
    workspace.open('rule', 'can-checkout');
    const state = workspace.getState();
    expect(state.tabs).toEqual([tabIdOf('rule', 'can-checkout')]);
    expect(state.active).toBe(tabIdOf('rule', 'can-checkout'));
    // The tab's workflow loads the document; the store starts clean on the seed the builder starts from.
    expect(state.docs[state.active!]!.store.getState().dirty).toBe(false);
  });

  it('activates rather than duplicates a tab that is already open', () => {
    const workspace = new Workspace();
    const first = workspace.open('rule', 'a');
    workspace.open('rule', 'b');
    workspace.open('rule', 'a');
    expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a'), tabIdOf('rule', 'b')]);
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'a'));
    expect(workspace.getState().docs[tabIdOf('rule', 'a')]!.store).toBe(first.store);
  });

  it('gives every tab its own store', async () => {
    const workspace = new Workspace();
    workspace.open('rule', 'a');
    workspace.open('proposition', 'customer.p');
    const { docs } = workspace.getState();
    expect(docs[tabIdOf('rule', 'a')]!.store).not.toBe(docs[tabIdOf('proposition', 'customer.p')]!.store);
  });

  it('closing the active tab activates the neighbour to the right, else the left', async () => {
    const workspace = new Workspace();
    workspace.open('rule', 'a');
    workspace.open('rule', 'b');
    workspace.open('rule', 'c');
    workspace.activate(tabIdOf('rule', 'b'));
    workspace.close(tabIdOf('rule', 'b'));
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'c'));
    workspace.close(tabIdOf('rule', 'c'));
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'a'));
    // Closing the last tab leaves one empty tab, as a browser window keeps one: there is always a
    // tab in front, and it is the catalog.
    workspace.close(tabIdOf('rule', 'a'));
    const { active, tabs } = workspace.getState();
    expect(tabs).toHaveLength(1);
    expect(active).toBe(tabs[0]);
    expect(isEmptyTab(active!)).toBe(true);
  });

  it('a new tab is empty, appended, and made active', () => {
    const workspace = new Workspace();
    workspace.open('rule', 'a');
    const id = workspace.newTab();
    expect(isEmptyTab(id)).toBe(true);
    expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a'), id]);
    expect(workspace.getState().active).toBe(id);
    expect(workspace.getState().docs[id]).toBeUndefined();
    // Two new tabs are two tabs.
    expect(workspace.newTab()).not.toBe(id);
    expect(workspace.getState().tabs).toHaveLength(3);
  });

  it('opening a document lands in the active tab while that tab is empty', () => {
    const workspace = new Workspace();
    workspace.open('rule', 'a');
    const empty = workspace.newTab();
    workspace.open('rule', 'b');
    expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a'), tabIdOf('rule', 'b')]);
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'b'));
    expect(workspace.getState().tabs).not.toContain(empty);
    // A loaded active tab is left alone: the next document is a new tab beside it.
    workspace.open('rule', 'c');
    expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a'), tabIdOf('rule', 'b'), tabIdOf('rule', 'c')]);
  });

  it('opens into a given tab, replacing what it held in place', () => {
    const workspace = new Workspace();
    workspace.open('rule', 'a');
    workspace.open('rule', 'b');
    workspace.open('rule', 'c');
    workspace.open('proposition', 'customer.p', { into: tabIdOf('rule', 'b') });
    expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a'), tabIdOf('proposition', 'customer.p'), tabIdOf('rule', 'c')]);
    expect(workspace.getState().active).toBe(tabIdOf('proposition', 'customer.p'));
    expect(workspace.getState().docs[tabIdOf('rule', 'b')]).toBeUndefined();
    // A document already open elsewhere is activated there rather than opened twice.
    workspace.open('rule', 'a', { into: tabIdOf('rule', 'c') });
    expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a'), tabIdOf('proposition', 'customer.p'), tabIdOf('rule', 'c')]);
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'a'));
  });

  it('unloads a tab in place: the tab stays where it was, empty, and its document goes', () => {
    const workspace = new Workspace();
    workspace.open('rule', 'a');
    workspace.open('rule', 'b');
    workspace.open('rule', 'c');
    workspace.activate(tabIdOf('rule', 'b'));
    workspace.unload(tabIdOf('rule', 'b'));
    const { tabs, active, docs } = workspace.getState();
    expect(tabs).toHaveLength(3);
    expect(isEmptyTab(tabs[1]!)).toBe(true);
    expect(docs[tabIdOf('rule', 'b')]).toBeUndefined();
    // The unloaded tab was the active one, so the empty tab in its place is.
    expect(active).toBe(tabs[1]);
  });

  it('activates an empty tab for a bare route, making one only when there is none', () => {
    const workspace = new Workspace();
    workspace.open('rule', 'a');
    workspace.activateEmpty();
    const first = workspace.getState().active!;
    expect(isEmptyTab(first)).toBe(true);
    expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a'), first]);
    workspace.activate(tabIdOf('rule', 'a'));
    workspace.activateEmpty();
    expect(workspace.getState().active).toBe(first);
    expect(workspace.getState().tabs).toHaveLength(2);
  });

  it('revises on every save, create or delete, so listings can be refreshed on the back of one', () => {
    const workspace = new Workspace();
    expect(workspace.getState().revision).toBe(0);
    workspace.noteSaved('rule', 'a', 2);
    expect(workspace.getState().revision).toBe(1);
    workspace.noteChanged();
    expect(workspace.getState().revision).toBe(2);
  });

  it('asks a tab to reload, once per request', () => {
    const workspace = new Workspace();
    workspace.open('proposition', 'customer.p');
    const id = tabIdOf('proposition', 'customer.p');
    expect(workspace.getState().docs[id]!.reloads).toBe(0);
    workspace.requestReload(id);
    workspace.requestReload(id);
    expect(workspace.getState().docs[id]!.reloads).toBe(2);
  });

  it('closing an inactive tab leaves the active one alone', async () => {
    const workspace = new Workspace();
    workspace.open('rule', 'a');
    workspace.open('rule', 'b');
    workspace.close(tabIdOf('rule', 'a'));
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'b'));
  });

  it('records a save: the latest version moves and the saver re-baselines', () => {
    const workspace = new Workspace();
    workspace.open('proposition', 'customer.p');
    const id = tabIdOf('proposition', 'customer.p');
    const before = workspace.getState().docs[id]!.seenAt;
    workspace.noteSaved('proposition', 'customer.p', 2);
    const state = workspace.getState();
    expect(state.latest['customer.p']).toMatchObject({ version: 2, kind: 'proposition' });
    expect(state.docs[id]!.seenAt).toBeGreaterThanOrEqual(before);
  });

  it('flags a reference as changed only after a save newer than the tab last looked', async () => {
    const workspace = new Workspace();
    workspace.open('rule', 'r');
    const rule = workspace.getState().docs[tabIdOf('rule', 'r')]!;
    const initial = referenceStatuses(workspace.getState(), rule, ['customer.is-active']);
    expect(initial[0]).toMatchObject({ name: 'customer.is-active', changedSinceSeen: false, editingElsewhere: false, openIn: null });

    // A save is stamped with the clock, so the clock has to move for it to count as newer.
    vi.useFakeTimers();
    try {
      vi.setSystemTime(rule.seenAt + 1000);
      workspace.noteSaved('proposition', 'customer.is-active', 5);
      const after = referenceStatuses(workspace.getState(), workspace.getState().docs[rule.id]!, ['customer.is-active']);
      expect(after[0]).toMatchObject({ version: 5, changedSinceSeen: true });

      vi.setSystemTime(rule.seenAt + 2000);
      workspace.acknowledge(rule.id);
      const acked = referenceStatuses(workspace.getState(), workspace.getState().docs[rule.id]!, ['customer.is-active']);
      expect(acked[0]!.changedSinceSeen).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('flags a reference as editing elsewhere while its tab has unsaved changes', async () => {
    const workspace = new Workspace();
    workspace.open('rule', 'r');
    workspace.open('proposition', 'customer.is-active');
    const rule = workspace.getState().docs[tabIdOf('rule', 'r')]!;
    const prop = workspace.getState().docs[tabIdOf('proposition', 'customer.is-active')]!;
    expect(referenceStatuses(workspace.getState(), rule, ['customer.is-active'])[0]).toMatchObject({ editingElsewhere: false, openIn: prop.id });
    prop.store.replaceNode('$.rule', { not: { spec: 'customer.is-adult' } });
    expect(referenceStatuses(workspace.getState(), rule, ['customer.is-active'])[0]!.editingElsewhere).toBe(true);
  });

  it('takes versions and metadata from the listings, keeping what a save recorded', () => {
    const workspace = new Workspace();
    workspace.noteSaved('proposition', 'customer.p', 7);
    workspace.setListings(
      [{ name: 'r', modelType: 'customer', metadataType: 'String', isAsync: true, isPolicy: false, version: 2, description: null }],
      [{ name: 'customer.p', modelType: 'customer', metadataType: 'String', isAsync: false, origin: 'Authored', version: 6, description: null, quarantine: [] }],
    );
    expect(workspace.getState().latest['r']).toMatchObject({ kind: 'rule', version: 2, modelType: 'customer', isAsync: true });
    // The listing is older than the save this workspace made: the save's version stands.
    expect(workspace.getState().latest['customer.p']).toMatchObject({ version: 7, origin: 'Authored' });
  });

  it('remembers the open tabs in session storage and restores them', () => {
    const workspace = new Workspace();
    workspace.open('rule', 'a');
    workspace.open('proposition', 'customer.p');
    // An empty tab is not worth remembering: a bare route makes one on the next visit.
    workspace.newTab();
    expect(JSON.parse(window.sessionStorage.getItem(TABS_KEY)!)).toEqual([
      { kind: 'rule', name: 'a' }, { kind: 'proposition', name: 'customer.p' },
    ]);

    const again = new Workspace();
    again.restore();
    expect(again.getState().tabs).toEqual([tabIdOf('rule', 'a'), tabIdOf('proposition', 'customer.p')]);
    expect(again.getState().active).toBeNull();
  });

  it('still works when storage is denied', () => {
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('denied'); });
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('denied'); });
    try {
      const workspace = new Workspace();
      workspace.restore();
      workspace.open('rule', 'a');
      expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a')]);
    } finally {
      getItem.mockRestore();
      setItem.mockRestore();
    }
  });

});

describe('referencesOf', () => {
  it('walks every node shape and de-duplicates', () => {
    expect(referencesOf({
      rule: {
        and: [
          { spec: 'a' },
          { not: { spec: 'b' } },
          { asAllSatisfied: { or: [{ spec: 'a' }, { expression: 'x > 1' }] }, path: '$.items' },
        ],
      },
    })).toEqual(['a', 'b']);
  });
});
