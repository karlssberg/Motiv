import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { RulesApiClient } from '@motiv-rules/core';
import {
  Workspace, referencesOf, referenceStatuses, tabIdOf, TABS_KEY,
} from '../../src/shell/workspace.js';

function makeClient(): RulesApiClient {
  return {
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'customer.is-active' } }, version: 3 }),
    getProposition: vi.fn().mockResolvedValue({
      document: { rule: { spec: 'customer.is-adult' } }, version: 1, origin: 'Authored', hasCompiledDefault: false,
    }),
  } as unknown as RulesApiClient;
}

describe('Workspace', () => {
  beforeEach(() => { window.sessionStorage.clear(); });

  it('opens a document in a new tab and makes it active', async () => {
    const workspace = new Workspace(makeClient());
    await workspace.open('rule', 'can-checkout');
    const state = workspace.getState();
    expect(state.tabs).toEqual([tabIdOf('rule', 'can-checkout')]);
    expect(state.active).toBe(tabIdOf('rule', 'can-checkout'));
    expect(state.docs[state.active!]!.store.getState().document).toEqual({ rule: { spec: 'customer.is-active' } });
    expect(state.docs[state.active!]!.version).toBe(3);
  });

  it('activates rather than duplicates a tab that is already open', async () => {
    const client = makeClient();
    const workspace = new Workspace(client);
    await workspace.open('rule', 'a');
    await workspace.open('rule', 'b');
    await workspace.open('rule', 'a');
    expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a'), tabIdOf('rule', 'b')]);
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'a'));
    expect(client.getRule).toHaveBeenCalledTimes(2);
  });

  it('gives every tab its own store', async () => {
    const workspace = new Workspace(makeClient());
    await workspace.open('rule', 'a');
    await workspace.open('proposition', 'customer.p');
    const { docs } = workspace.getState();
    expect(docs[tabIdOf('rule', 'a')]!.store).not.toBe(docs[tabIdOf('proposition', 'customer.p')]!.store);
  });

  it('closing the active tab activates the neighbour to the right, else the left', async () => {
    const workspace = new Workspace(makeClient());
    await workspace.open('rule', 'a');
    await workspace.open('rule', 'b');
    await workspace.open('rule', 'c');
    workspace.activate(tabIdOf('rule', 'b'));
    workspace.close(tabIdOf('rule', 'b'));
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'c'));
    workspace.close(tabIdOf('rule', 'c'));
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'a'));
    workspace.close(tabIdOf('rule', 'a'));
    expect(workspace.getState().active).toBeNull();
    expect(workspace.getState().tabs).toEqual([]);
  });

  it('closing an inactive tab leaves the active one alone', async () => {
    const workspace = new Workspace(makeClient());
    await workspace.open('rule', 'a');
    await workspace.open('rule', 'b');
    workspace.close(tabIdOf('rule', 'a'));
    expect(workspace.getState().active).toBe(tabIdOf('rule', 'b'));
  });

  it('records a save: the latest version moves and the saver re-baselines', async () => {
    const workspace = new Workspace(makeClient());
    await workspace.open('proposition', 'customer.p');
    const id = tabIdOf('proposition', 'customer.p');
    const before = workspace.getState().docs[id]!.seenAt;
    workspace.noteSaved('proposition', 'customer.p', 2);
    const state = workspace.getState();
    expect(state.latest['customer.p']).toMatchObject({ version: 2, kind: 'proposition' });
    expect(state.docs[id]!.version).toBe(2);
    expect(state.docs[id]!.seenAt).toBeGreaterThanOrEqual(before);
  });

  it('flags a reference as changed only after a save newer than the tab last looked', async () => {
    const workspace = new Workspace(makeClient());
    await workspace.open('rule', 'r');
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
    const workspace = new Workspace(makeClient());
    await workspace.open('rule', 'r');
    await workspace.open('proposition', 'customer.is-active');
    const rule = workspace.getState().docs[tabIdOf('rule', 'r')]!;
    const prop = workspace.getState().docs[tabIdOf('proposition', 'customer.is-active')]!;
    expect(referenceStatuses(workspace.getState(), rule, ['customer.is-active'])[0]).toMatchObject({ editingElsewhere: false, openIn: prop.id });
    prop.store.replaceNode('$.rule', { not: { spec: 'customer.is-adult' } });
    expect(referenceStatuses(workspace.getState(), rule, ['customer.is-active'])[0]!.editingElsewhere).toBe(true);
  });

  it('takes the latest versions from the listings without disturbing what a save recorded', async () => {
    const workspace = new Workspace(makeClient());
    workspace.noteSaved('proposition', 'customer.p', 7);
    workspace.setLatest([{ kind: 'rule', name: 'r', version: 2 }, { kind: 'proposition', name: 'customer.p', version: 7 }]);
    expect(workspace.getState().latest['r']).toMatchObject({ version: 2 });
    expect(workspace.getState().latest['customer.p']).toMatchObject({ version: 7 });
  });

  it('remembers the open tabs in session storage and restores them', async () => {
    const workspace = new Workspace(makeClient());
    await workspace.open('rule', 'a');
    await workspace.open('proposition', 'customer.p');
    expect(JSON.parse(window.sessionStorage.getItem(TABS_KEY)!)).toEqual([
      { kind: 'rule', name: 'a' }, { kind: 'proposition', name: 'customer.p' },
    ]);

    const again = new Workspace(makeClient());
    await again.restore();
    expect(again.getState().tabs).toEqual([tabIdOf('rule', 'a'), tabIdOf('proposition', 'customer.p')]);
    expect(again.getState().active).toBeNull();
  });

  it('still works when storage is denied', async () => {
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('denied'); });
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('denied'); });
    try {
      const workspace = new Workspace(makeClient());
      await workspace.restore();
      await workspace.open('rule', 'a');
      expect(workspace.getState().tabs).toEqual([tabIdOf('rule', 'a')]);
    } finally {
      getItem.mockRestore();
      setItem.mockRestore();
    }
  });

  it('reports a failed load on the tab rather than throwing', async () => {
    const client = { ...makeClient(), getRule: vi.fn().mockRejectedValue(new Error('boom')) } as unknown as RulesApiClient;
    const workspace = new Workspace(client);
    await workspace.open('rule', 'a');
    expect(workspace.getState().docs[tabIdOf('rule', 'a')]).toMatchObject({ status: 'failed', error: 'boom' });
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
