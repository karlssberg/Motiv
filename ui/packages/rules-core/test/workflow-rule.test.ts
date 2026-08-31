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
      rules: [], loaded: null, loadedEntry: null, conflict: null, failure: null, saving: false,
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

  it('refresh re-derives the loaded entry from the listing it adopts', async () => {
    const { controller } = makeController();
    // Loaded before the listing ever arrived: nothing to derive the entry from yet.
    await controller.load('fraud-screening');
    expect(controller.getState().loadedEntry).toBeNull();

    await controller.refresh();

    expect(controller.getState().loadedEntry).toEqual(listing[1]);
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

  it('a save outcome never lands on a newer load', async () => {
    const put = deferred<{ outcome: string; version: number }>();
    const client = makeClient({
      putRule: vi.fn().mockReturnValue(put.promise),
      getRule: vi.fn()
        .mockResolvedValueOnce({ document: { rule: { spec: 'is-adult' } }, version: 3 })
        .mockResolvedValueOnce({ document: { rule: { spec: 'is-vip' } }, version: 7 }),
    });
    const { controller } = makeController(client);
    await controller.load('can-checkout');

    const inFlight = controller.save();
    await controller.load('fraud-screening');
    put.resolve({ outcome: 'updated', version: 4 });
    await inFlight;

    // The save was aimed at can-checkout; fraud-screening keeps its own identity.
    expect(controller.getState().loaded).toEqual({
      name: 'fraud-screening', version: 7, isCodeDefault: false,
    });
    expect(controller.getState().saving).toBe(false);
  });

  it('a superseded save conflict never mislabels a newer load', async () => {
    const put = deferred<{ outcome: string; currentVersion: number }>();
    const client = makeClient({ putRule: vi.fn().mockReturnValue(put.promise) });
    const { controller } = makeController(client);
    await controller.load('can-checkout');

    const inFlight = controller.save();
    await controller.load('fraud-screening');
    put.resolve({ outcome: 'conflict', currentVersion: 9 });
    await inFlight;

    // The conflict belongs to can-checkout, which is no longer on screen.
    expect(controller.getState().conflict).toBeNull();
  });

  it('refuses a concurrent save, so the saving flag never lies', async () => {
    const put = deferred<{ outcome: string; version: number }>();
    const client = makeClient({ putRule: vi.fn().mockReturnValue(put.promise) });
    const { controller } = makeController(client);
    await controller.load('can-checkout');

    const first = controller.save();
    // The second call lands while the first PUT is in flight. Issuing it would put two saves in
    // the air, and the earlier completion would clear `saving` under the one still running.
    await controller.save();

    expect(client.putRule).toHaveBeenCalledTimes(1);
    expect(controller.getState().saving).toBe(true);

    put.resolve({ outcome: 'updated', version: 4 });
    await first;
    expect(controller.getState().saving).toBe(false);
  });

  it('saving is flagged while the PUT is in flight, and cleared even when it throws', async () => {
    const put = deferred<never>();
    const client = makeClient({ putRule: vi.fn().mockReturnValue(put.promise) });
    const { controller } = makeController(client);
    await controller.load('can-checkout');

    const inFlight = controller.save();
    expect(controller.getState().saving).toBe(true);

    put.reject(new Error('boom'));
    // Reported, not rethrown: a consumer writing `void save()` would otherwise get an unhandled
    // rejection and a surface showing nothing — indistinguishable from never having saved.
    await expect(inFlight).resolves.toBeUndefined();
    expect(controller.getState().saving).toBe(false);
    expect(controller.getState().failure).toBe('boom');
  });

  it('a thrown listing refresh is reported rather than escaping the caller', async () => {
    const client = makeClient({ listRules: vi.fn().mockRejectedValue(new Error('listing failed')) });
    const { controller } = makeController(client);

    await expect(controller.refresh()).resolves.toBeUndefined();

    expect(controller.getState().failure).toBe('listing failed');
    expect(controller.getState().rules).toEqual([]);
  });

  it('a superseded refresh failure never lands over a newer listing', async () => {
    const stale = deferred<RuleListEntry[]>();
    const client = makeClient({
      listRules: vi.fn()
        .mockReturnValueOnce(stale.promise)
        .mockResolvedValueOnce(listing),
    });
    const { controller } = makeController(client);

    const first = controller.refresh();
    await controller.refresh();
    stale.reject(new Error('listing failed'));
    await first;

    // The failed fetch describes a world the newer one has already replaced.
    expect(controller.getState().failure).toBeNull();
    expect(controller.getState().rules).toEqual(listing);
  });

  it('a refresh that succeeds clears the failure the one before it raised', async () => {
    const client = makeClient({
      listRules: vi.fn()
        .mockRejectedValueOnce(new Error('listing failed'))
        .mockResolvedValueOnce(listing),
    });
    const { controller } = makeController(client);
    await controller.refresh();
    expect(controller.getState().failure).toBe('listing failed');

    await controller.refresh();

    // The listing it described has been replaced; a banner still claiming it failed is a report
    // about a world that is no longer on screen.
    expect(controller.getState().failure).toBeNull();
    expect(controller.getState().rules).toEqual(listing);
  });

  it('one channel, so whichever operation runs next clears what the last one reported', async () => {
    const client = makeClient({ putRule: vi.fn().mockRejectedValue(new Error('boom')) });
    const { controller } = makeController(client);
    await controller.load('can-checkout');
    await controller.save();
    expect(controller.getState().failure).toBe('boom');

    // Not a save, and not about the same subject — but there is one banner, and the newest report
    // owns it. Keeping a save's failure across an act that succeeded would leave the page saying
    // something is wrong while nothing is.
    await controller.refresh();

    expect(controller.getState().failure).toBeNull();
  });

  it('a thrown load is reported, and leaves the identity it could not replace', async () => {
    const client = makeClient({
      getRule: vi.fn()
        .mockResolvedValueOnce({ document: { rule: { spec: 'is-adult' } }, version: 3 })
        .mockRejectedValueOnce(new Error('rule unreachable')),
    });
    const { controller, store } = makeController(client);
    await controller.load('can-checkout');

    await expect(controller.load('fraud-screening')).resolves.toBeUndefined();

    expect(controller.getState().failure).toBe('rule unreachable');
    // Unlike the proposition controller, `loaded` is only ever written *after* a load lands, so
    // the identity still standing is the one whose document is in the store. Dropping it would
    // demote a loaded rule to a local draft over a transient 500.
    expect(controller.getState().loaded).toEqual({
      name: 'can-checkout', version: 3, isCodeDefault: false,
    });
    expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } });
  });

  it('a new load clears the previous failure before its own fetch lands', async () => {
    const arriving = deferred<{ document: unknown; version: number }>();
    const client = makeClient({
      getRule: vi.fn()
        .mockRejectedValueOnce(new Error('rule unreachable'))
        .mockReturnValueOnce(arriving.promise),
    });
    const { controller } = makeController(client);
    await controller.load('can-checkout');
    expect(controller.getState().failure).toBe('rule unreachable');

    const retry = controller.load('can-checkout');
    // Cleared on the way out, not on the way back: a banner that outlives the retry it triggered
    // reads as the retry having failed too.
    expect(controller.getState().failure).toBeNull();

    arriving.resolve({ document: { rule: { spec: 'is-adult' } }, version: 3 });
    await retry;
    expect(controller.getState().failure).toBeNull();
  });

  it('a superseded load failure never lands on a newer pick', async () => {
    const stale = deferred<{ document: unknown; version: number }>();
    const client = makeClient({
      getRule: vi.fn()
        .mockReturnValueOnce(stale.promise)
        .mockResolvedValueOnce({ document: { rule: { spec: 'is-vip' } }, version: 7 }),
    });
    const { controller } = makeController(client);

    const first = controller.load('can-checkout');
    await controller.load('fraud-screening');
    stale.reject(new Error('rule unreachable'));
    await first;

    expect(controller.getState().failure).toBeNull();
    expect(controller.getState().loaded).toEqual({
      name: 'fraud-screening', version: 7, isCodeDefault: false,
    });
  });

  it('a retried save clears the failure the one before it reported, before the PUT lands', async () => {
    const retry = deferred<{ outcome: string; version: number }>();
    const client = makeClient({
      putRule: vi.fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockReturnValueOnce(retry.promise),
    });
    const { controller } = makeController(client);
    await controller.load('can-checkout');
    await controller.save();
    expect(controller.getState().failure).toBe('boom');

    const second = controller.save();
    // On the way out, not on the way back: a banner still reading 'boom' over a save in flight
    // says the retry failed before it has answered.
    expect(controller.getState().failure).toBeNull();

    retry.resolve({ outcome: 'updated', version: 4 });
    await second;
    expect(controller.getState().failure).toBeNull();
    expect(controller.getState().loaded?.version).toBe(4);
  });

  it('a version conflict is not an unexpected failure', async () => {
    const client = makeClient({
      putRule: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 9 }),
    });
    const { controller } = makeController(client);
    await controller.load('can-checkout');

    await controller.save();

    // A 409 is a typed outcome with its own channel and its own recovery; reporting it twice
    // would put the same event in two banners saying different things.
    expect(controller.getState().conflict).toBe(9);
    expect(controller.getState().failure).toBeNull();
  });

  it('a superseded save failure never mislabels a newer load', async () => {
    const put = deferred<never>();
    const client = makeClient({ putRule: vi.fn().mockReturnValue(put.promise) });
    const { controller } = makeController(client);
    await controller.load('can-checkout');

    const inFlight = controller.save();
    await controller.load('fraud-screening');
    put.reject(new Error('boom'));
    await inFlight;

    // The failure belongs to can-checkout, which is no longer on screen.
    expect(controller.getState().failure).toBeNull();
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
