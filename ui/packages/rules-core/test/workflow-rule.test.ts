import { describe, it, expect, vi } from 'vitest';
import { RuleEditorStore } from '../src/index.js';
import type { RuleListEntry, RulesApiClient } from '../src/index.js';
import { RuleWorkflowController, whyRuleSaveUnavailable } from '../src/workflow/index.js';

const listing: RuleListEntry[] = [
  {
    name: 'can-checkout', modelType: 'customer', metadataType: 'String',
    isAsync: false, isPolicy: false, version: 1, description: 'Gate',
  },
  {
    name: 'fraud-screening', modelType: 'customer', metadataType: 'String',
    isAsync: true, isPolicy: false, version: 1, description: 'Screening',
  },
];

/** A deferred the tests resolve by hand, for pinning race behaviour. */
function deferred<T>(): { promise: Promise<T>; resolve: (value: T) => void; reject: (reason?: unknown) => void } {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
}

function makeClient(overrides: Partial<Record<string, unknown>> = {}): RulesApiClient {
  return {
    listRules: vi.fn().mockResolvedValue(listing),
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'is-adult' } }, version: 3 }),
    putRule: vi.fn().mockResolvedValue({ outcome: 'updated', version: 4 }),
    ...overrides,
  } as unknown as RulesApiClient;
}

function makeController(client: RulesApiClient = makeClient()) {
  const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
  const controller = new RuleWorkflowController(client, store);
  return { client, store, controller };
}

describe('RuleWorkflowController', () => {
  it('starts with nothing listed and nothing loaded', () => {
    const { controller } = makeController();
    expect(controller.getState()).toEqual({
      rules: [], loaded: null, loadedEntry: null, conflict: null, saving: false,
    });
  });

  it('refresh adopts the server listing', async () => {
    const { controller } = makeController();
    await controller.refresh();
    expect(controller.getState().rules).toEqual(listing);
  });

  it('a superseded refresh never overwrites a newer one', async () => {
    const first = deferred<RuleListEntry[]>();
    const client = makeClient({
      listRules: vi.fn()
        .mockReturnValueOnce(first.promise)
        .mockResolvedValueOnce([listing[0]]),
    });
    const { controller } = makeController(client);

    const stale = controller.refresh();
    await controller.refresh();
    first.resolve(listing);
    await stale;

    expect(controller.getState().rules).toEqual([listing[0]]);
  });

  it('load takes the rule identity and pushes its document into the store', async () => {
    const { controller, store } = makeController();
    await controller.refresh();

    await controller.load('can-checkout');

    expect(controller.getState().loaded).toEqual({
      name: 'can-checkout', version: 3, isCodeDefault: false,
    });
    expect(controller.getState().loadedEntry).toEqual(listing[0]);
    expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } });
  });

  it('load of a code-defined default leaves the editor document alone', async () => {
    const client = makeClient({
      getRule: vi.fn().mockResolvedValue({ document: null, version: 1 }),
    });
    const { controller, store } = makeController(client);

    await controller.load('can-checkout');

    expect(controller.getState().loaded).toEqual({
      name: 'can-checkout', version: 1, isCodeDefault: true,
    });
    expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
  });

  it('load(null) drops the server identity but keeps the document', async () => {
    const { controller, store } = makeController();
    await controller.refresh();
    await controller.load('can-checkout');

    await controller.load(null);

    expect(controller.getState().loaded).toBeNull();
    expect(controller.getState().loadedEntry).toBeNull();
    expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } });
  });

  it('a superseded load never lands on a newer pick', async () => {
    const stale = deferred<{ document: unknown; version: number }>();
    const client = makeClient({
      getRule: vi.fn()
        .mockReturnValueOnce(stale.promise)
        .mockResolvedValueOnce({ document: { rule: { spec: 'is-vip' } }, version: 7 }),
    });
    const { controller, store } = makeController(client);

    const first = controller.load('can-checkout');
    await controller.load('fraud-screening');
    stale.resolve({ document: { rule: { spec: 'is-adult' } }, version: 3 });
    await first;

    expect(controller.getState().loaded).toEqual({
      name: 'fraud-screening', version: 7, isCodeDefault: false,
    });
    expect(store.getState().document).toEqual({ rule: { spec: 'is-vip' } });
  });

  it('save sends the loaded version and adopts the new one', async () => {
    const { controller, client, store } = makeController();
    await controller.load('can-checkout');

    await controller.save();

    expect(client.putRule).toHaveBeenCalledWith(
      'can-checkout', store.getState().document, 3,
    );
    expect(controller.getState().loaded).toEqual({
      name: 'can-checkout', version: 4, isCodeDefault: false,
    });
  });

  it('save of a code-defined default authors it: isCodeDefault clears', async () => {
    const client = makeClient({
      getRule: vi.fn().mockResolvedValue({ document: null, version: 1 }),
    });
    const { controller } = makeController(client);
    await controller.load('can-checkout');

    await controller.save();

    expect(controller.getState().loaded).toEqual({
      name: 'can-checkout', version: 4, isCodeDefault: false,
    });
  });

  it('save without a loaded rule is a no-op', async () => {
    const { controller, client } = makeController();
    await controller.save();
    expect(client.putRule).not.toHaveBeenCalled();
  });

  it('a version conflict is recorded, and reloading clears it', async () => {
    const client = makeClient({
      putRule: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 9 }),
    });
    const { controller } = makeController(client);
    await controller.load('can-checkout');

    await controller.save();
    expect(controller.getState().conflict).toBe(9);
    // The version it sent is kept: the save did not happen.
    expect(controller.getState().loaded?.version).toBe(3);

    await controller.load('can-checkout');
    expect(controller.getState().conflict).toBeNull();
  });

  it('an invalid save pushes its errors into the editor store', async () => {
    const errors = [{ path: '$.rule', code: 'PolicyRequired' as const, message: 'must be a policy' }];
    const client = makeClient({
      putRule: vi.fn().mockResolvedValue({ outcome: 'invalid', errors }),
    });
    const { controller, store } = makeController(client);
    await controller.load('can-checkout');

    await controller.save();

    expect(store.getState().errors).toEqual(errors);
  });

  it('saving is flagged while the PUT is in flight, and cleared even when it throws', async () => {
    const put = deferred<never>();
    const client = makeClient({ putRule: vi.fn().mockReturnValue(put.promise) });
    const { controller } = makeController(client);
    await controller.load('can-checkout');

    const inFlight = controller.save();
    expect(controller.getState().saving).toBe(true);

    put.reject(new Error('boom'));
    await expect(inFlight).rejects.toThrow('boom');
    expect(controller.getState().saving).toBe(false);
  });

  it('notifies subscribers on every state change, with a stable unchanged snapshot', async () => {
    const { controller } = makeController();
    const listener = vi.fn();
    controller.subscribe(listener);

    const before = controller.getState();
    expect(controller.getState()).toBe(before);

    await controller.refresh();
    expect(listener).toHaveBeenCalled();
    expect(controller.getState()).not.toBe(before);
  });
});

describe('whyRuleSaveUnavailable', () => {
  const loaded = { name: 'can-checkout', version: 3, isCodeDefault: false };

  it('needs something loaded', () => {
    expect(whyRuleSaveUnavailable({ loaded: null, saving: false })).toBe('Nothing loaded yet.');
  });

  it('refuses a second save while one is in flight', () => {
    expect(whyRuleSaveUnavailable({ loaded, saving: true })).toBe('Saving…');
  });

  it('is silent when a save can run', () => {
    expect(whyRuleSaveUnavailable({ loaded, saving: false })).toBeUndefined();
  });
});
