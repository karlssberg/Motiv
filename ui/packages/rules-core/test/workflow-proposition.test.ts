import { describe, it, expect, vi } from 'vitest';
import { RuleEditorStore } from '../src/index.js';
import type {
  PropositionGetResponse, PropositionListEntry, RulesApiClient,
} from '../src/index.js';
import {
  PropositionWorkflowController,
  describePropositionFailure, describeUnexpectedFailure, whyPropositionSaveUnavailable,
} from '../src/workflow/index.js';

const entries: PropositionListEntry[] = [
  {
    name: 'pricing.is-vip', modelType: 'customer', metadataType: 'String',
    isAsync: false, origin: 'Authored', version: 2, description: null, quarantine: [],
  },
  {
    name: 'is-adult', modelType: 'customer', metadataType: 'String',
    isAsync: false, origin: 'Overridden', version: 1, description: null, quarantine: [],
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
    listPropositions: vi.fn().mockResolvedValue(entries),
    getProposition: vi.fn().mockResolvedValue({
      document: { rule: { spec: 'is-adult' } }, version: 2, origin: 'Authored', hasCompiledDefault: false,
    } satisfies PropositionGetResponse),
    getDependents: vi.fn().mockResolvedValue([{ name: 'can-checkout', kind: 'rule' }]),
    putProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 3 }),
    deleteProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 0 }),
    createProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 1 }),
    ...overrides,
  } as unknown as RulesApiClient;
}

function makeController(client: RulesApiClient = makeClient(), onSelect = vi.fn()) {
  const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
  const controller = new PropositionWorkflowController(client, store, { onSelect });
  return { client, store, controller, onSelect };
}

describe('PropositionWorkflowController', () => {
  it('starts empty, with nothing selected and nothing to report', () => {
    const { controller } = makeController();
    expect(controller.getState()).toEqual({
      entries: [], loaded: null, dependents: [], failure: null, saving: false,
    });
  });

  it('refreshEntries adopts the listing, and reports a failed reload in the banner', async () => {
    const { controller } = makeController();
    await controller.refreshEntries();
    expect(controller.getState().entries).toEqual(entries);

    const failing = makeClient({
      listPropositions: vi.fn().mockRejectedValue(new Error('listing down')),
    });
    const { controller: broken } = makeController(failing);
    await broken.refreshEntries();
    expect(broken.getState().failure).toBe('listing down');
  });

  describe('select', () => {
    it('fetches the proposition and its blast radius together', async () => {
      const { controller, store, client } = makeController();

      await controller.select('pricing.is-vip');

      expect(client.getProposition).toHaveBeenCalledWith('pricing.is-vip');
      expect(client.getDependents).toHaveBeenCalledWith('pricing.is-vip');
      expect(controller.getState().loaded).toEqual({ name: 'pricing.is-vip', version: 2 });
      expect(controller.getState().dependents).toEqual([{ name: 'can-checkout', kind: 'rule' }]);
      expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } });
    });

    it('clears the previous selection claims before the fetch, not when it lands', async () => {
      const gate = deferred<PropositionGetResponse>();
      const client = makeClient({
        getProposition: vi.fn()
          .mockResolvedValueOnce({ document: null, version: 2, origin: 'Authored', hasCompiledDefault: false })
          .mockReturnValueOnce(gate.promise),
      });
      const { controller } = makeController(client);
      await controller.select('pricing.is-vip');

      const inFlight = controller.select('is-adult');
      // Dependents and failure are claims about the *previous* selection; loaded survives so the
      // breadcrumb is not blanked for a round trip.
      expect(controller.getState().dependents).toEqual([]);
      expect(controller.getState().failure).toBeNull();
      expect(controller.getState().loaded).toEqual({ name: 'pricing.is-vip', version: 2 });

      gate.resolve({ document: null, version: 1, origin: 'Overridden', hasCompiledDefault: true });
      await inFlight;
      expect(controller.getState().loaded).toEqual({ name: 'is-adult', version: 1 });
    });

    it('select(null) unloads', async () => {
      const { controller } = makeController();
      await controller.select('pricing.is-vip');
      await controller.select(null);
      expect(controller.getState().loaded).toBeNull();
      expect(controller.getState().dependents).toEqual([]);
    });

    it('a compiled proposition leaves the editor document alone', async () => {
      const client = makeClient({
        getProposition: vi.fn().mockResolvedValue({
          document: null, version: 0, origin: 'Compiled', hasCompiledDefault: true,
        }),
      });
      const { controller, store } = makeController(client);

      await controller.select('is-adult');

      expect(controller.getState().loaded).toEqual({ name: 'is-adult', version: 0 });
      expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
    });

    it('a superseded select never lands on a newer one', async () => {
      const stale = deferred<PropositionGetResponse>();
      const client = makeClient({
        getProposition: vi.fn()
          .mockReturnValueOnce(stale.promise)
          .mockResolvedValueOnce({ document: null, version: 1, origin: 'Overridden', hasCompiledDefault: true }),
      });
      const { controller } = makeController(client);

      const first = controller.select('pricing.is-vip');
      await controller.select('is-adult');
      stale.resolve({ document: { rule: { spec: 'is-vip' } }, version: 9, origin: 'Authored', hasCompiledDefault: false });
      await first;

      expect(controller.getState().loaded).toEqual({ name: 'is-adult', version: 1 });
    });

    it('reports a failed load and drops the stale identity', async () => {
      const client = makeClient({
        getProposition: vi.fn().mockRejectedValue(new Error('gone')),
      });
      const { controller } = makeController(client);

      await controller.select('pricing.is-vip');

      expect(controller.getState().failure).toBe('gone');
      expect(controller.getState().loaded).toBeNull();
    });

    it('reload refetches behind the same name', async () => {
      const { controller, client } = makeController();
      await controller.select('pricing.is-vip');

      await controller.reload();

      expect(client.getProposition).toHaveBeenCalledTimes(2);
      expect(controller.getState().loaded).toEqual({ name: 'pricing.is-vip', version: 2 });
    });
  });

  describe('save', () => {
    it('sends the loaded version, adopts the new one, and refreshes the listing', async () => {
      const { controller, client, store } = makeController();
      await controller.select('pricing.is-vip');

      await controller.save();

      expect(client.putProposition).toHaveBeenCalledWith(
        'pricing.is-vip', store.getState().document, 2,
      );
      expect(controller.getState().loaded).toEqual({ name: 'pricing.is-vip', version: 3 });
      expect(controller.getState().entries).toEqual(entries);
      expect(controller.getState().failure).toBeNull();
    });

    it('renders a conflict as banner text', async () => {
      const client = makeClient({
        putProposition: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 5 }),
      });
      const { controller } = makeController(client);
      await controller.select('pricing.is-vip');

      await controller.save();

      expect(controller.getState().failure).toBe(
        'Someone else saved version 5. Reload before saving again.',
      );
      expect(controller.getState().loaded?.version).toBe(2);
    });

    it('an outcome never lands on a newer selection', async () => {
      const put = deferred<{ outcome: string; version: number }>();
      const client = makeClient({
        putProposition: vi.fn().mockReturnValue(put.promise),
      });
      const { controller } = makeController(client);
      await controller.select('pricing.is-vip');

      const inFlight = controller.save();
      await controller.select('is-adult');
      put.resolve({ outcome: 'saved', version: 3 });
      await inFlight;

      // The save was aimed at pricing.is-vip; is-adult keeps its own version and no banner.
      expect(controller.getState().loaded).toEqual({ name: 'is-adult', version: 2 });
      expect(controller.getState().failure).toBeNull();
    });

    it('reports a thrown save while its selection is still current, and only then', async () => {
      const client = makeClient({
        putProposition: vi.fn().mockRejectedValue(new Error('boom')),
      });
      const { controller } = makeController(client);
      await controller.select('pricing.is-vip');

      await controller.save();
      expect(controller.getState().failure).toBe('boom');
      expect(controller.getState().saving).toBe(false);
    });

    it('does nothing with no selection', async () => {
      const { controller, client } = makeController();
      await controller.save();
      expect(client.putProposition).not.toHaveBeenCalled();
    });
  });

  describe('remove', () => {
    it('a delete clears the selection', async () => {
      const { controller, client, onSelect } = makeController();
      await controller.refreshEntries();
      await controller.select('pricing.is-vip');

      await controller.remove(entries[0]!);

      expect(client.deleteProposition).toHaveBeenCalledWith('pricing.is-vip', 2);
      expect(onSelect).toHaveBeenCalledWith(null);
    });

    it('a revert keeps the selection and refetches behind it', async () => {
      const client = makeClient();
      const { controller, onSelect } = makeController(client);
      await controller.select('is-adult');

      await controller.remove(entries[1]!);

      // Overridden origin means the DELETE reverts to the compiled spec: the name survives.
      expect(onSelect).toHaveBeenCalledWith('is-adult');
      expect(client.getProposition).toHaveBeenCalledTimes(2);
    });

    it('a refusal is rendered as banner text', async () => {
      const client = makeClient({
        deleteProposition: vi.fn().mockResolvedValue({
          outcome: 'referenced', referrers: ['can-checkout', 'pricing.is-vip'],
        }),
      });
      const { controller, onSelect } = makeController(client);
      await controller.select('is-adult');

      await controller.remove(entries[1]!);

      expect(controller.getState().failure).toBe(
        'Still referenced by can-checkout, pricing.is-vip. Change those first.',
      );
      expect(onSelect).not.toHaveBeenCalled();
    });

    it('navigation is dropped too when the selection moves during the listing refresh', async () => {
      const listGate = deferred<PropositionListEntry[]>();
      const client = makeClient({
        listPropositions: vi.fn().mockReturnValue(listGate.promise),
      });
      const { controller, onSelect } = makeController(client);
      await controller.select('pricing.is-vip');

      const inFlight = controller.remove(entries[0]!);
      // Wait until the DELETE has landed and the listing refresh is in flight — the selection
      // was still the removed entry's at that point, so the outcome guard has already passed.
      await vi.waitFor(() => expect(client.listPropositions).toHaveBeenCalled());
      await controller.select('is-adult');
      listGate.resolve(entries);
      await inFlight;

      // The DELETE landed while its entry was still selected, but by the time the listing came
      // back the user had moved on — handing the selection over now would drag them off it.
      expect(onSelect).not.toHaveBeenCalled();
    });

    it("an outcome for an entry that is no longer selected is dropped", async () => {
      const { controller, client, onSelect } = makeController();
      await controller.select('is-adult');

      await controller.remove(entries[0]!);

      expect(client.deleteProposition).toHaveBeenCalled();
      expect(controller.getState().failure).toBeNull();
      expect(onSelect).not.toHaveBeenCalled();
    });
  });

  describe('create', () => {
    const request = {
      name: 'pricing.is-gold', modelType: 'customer',
      document: { rule: { spec: 'is-adult' } }, description: null,
    };

    it('returns null on success, refreshes the listing, and hands the selection over', async () => {
      const { controller, onSelect } = makeController();

      const failure = await controller.create(request);

      expect(failure).toBeNull();
      expect(controller.getState().entries).toEqual(entries);
      expect(onSelect).toHaveBeenCalledWith('pricing.is-gold');
    });

    it('returns the refusal as text and does not navigate', async () => {
      const client = makeClient({
        createProposition: vi.fn().mockResolvedValue({ outcome: 'nameTaken' }),
      });
      const { controller, onSelect } = makeController(client);

      const failure = await controller.create(request);

      expect(failure).toBe('A proposition is already authored under that name.');
      expect(onSelect).not.toHaveBeenCalled();
    });

    it('returns a thrown failure as text', async () => {
      const client = makeClient({
        createProposition: vi.fn().mockRejectedValue(new Error('down')),
      });
      const { controller } = makeController(client);

      expect(await controller.create(request)).toBe('down');
    });
  });
});

describe('describePropositionFailure', () => {
  it('is silent about a success', () => {
    expect(describePropositionFailure({ outcome: 'saved', version: 3 })).toBeNull();
  });

  it('describes a conflict', () => {
    expect(describePropositionFailure({ outcome: 'conflict', currentVersion: 4 })).toBe(
      'Someone else saved version 4. Reload before saving again.',
    );
  });

  it('describes a duplicate name', () => {
    expect(describePropositionFailure({ outcome: 'nameTaken' })).toBe(
      'A proposition is already authored under that name.',
    );
  });

  it('describes what still references it', () => {
    expect(describePropositionFailure({ outcome: 'referenced', referrers: ['a', 'b'] })).toBe(
      'Still referenced by a, b. Change those first.',
    );
  });

  it('describes broken dependents apart from document errors', () => {
    expect(describePropositionFailure({
      outcome: 'invalid',
      errors: [{ path: '$', code: 'UnknownSpec', message: 'unknown spec' }],
      brokenDependents: [{
        name: 'can-checkout', kind: 'rule',
        errors: [{ path: '$', code: 'UnknownSpec', message: 'no such spec' }],
      }],
    })).toBe('This change would break rule can-checkout (no such spec).');
  });

  it('falls back to the document errors when nothing else broke', () => {
    expect(describePropositionFailure({
      outcome: 'invalid',
      errors: [
        { path: '$', code: 'UnknownSpec', message: 'unknown spec' },
        { path: '$', code: 'InvalidNode', message: 'bad node' },
      ],
      brokenDependents: [],
    })).toBe('unknown spec; bad node');
  });
});

describe('describeUnexpectedFailure', () => {
  it('reads an Error by its message', () => {
    expect(describeUnexpectedFailure(new Error('oops'))).toBe('oops');
  });

  it('stringifies anything else', () => {
    expect(describeUnexpectedFailure('wat')).toBe('wat');
  });
});

describe('whyPropositionSaveUnavailable', () => {
  it('needs something loaded', () => {
    expect(whyPropositionSaveUnavailable({ loaded: null, saving: false })).toBe('Nothing loaded yet.');
  });

  it('refuses to save over a compiled spec', () => {
    expect(whyPropositionSaveUnavailable({ loaded: { name: 'is-adult', version: 0 }, saving: false })).toBe(
      'This name is served by a compiled spec. Use Override to author one.',
    );
  });

  it('refuses a second save while one is in flight', () => {
    expect(whyPropositionSaveUnavailable({ loaded: { name: 'is-adult', version: 1 }, saving: true })).toBe('Saving…');
  });

  it('is silent when a save can run', () => {
    expect(whyPropositionSaveUnavailable({ loaded: { name: 'is-adult', version: 1 }, saving: false })).toBeUndefined();
  });
});
