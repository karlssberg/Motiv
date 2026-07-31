import { describe, it, expect, vi } from 'vitest';
import { RulesApiClient, RulesApiError } from '../src/client.js';
import type { EvaluationResult, ValidationResponse } from '../src/contracts.js';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

describe('RulesApiClient', () => {
  it('gets the folded catalog of specs and collections', async () => {
    const catalog = {
      specs: [{ name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: null }],
      collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
    };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(catalog));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.getCatalog();

    expect(result).toEqual(catalog);
    expect(fetchMock).toHaveBeenCalledWith('/api/rules/catalog', { method: 'GET' });
  });

  it('posts a validate request and returns the errors', async () => {
    const body: ValidationResponse = { errors: [] };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.validate({ modelType: 'number', document: { rule: { spec: 'is-positive' } } });

    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/rules/validate');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body)).toEqual({ modelType: 'number', document: { rule: { spec: 'is-positive' } } });
    expect(result).toEqual(body);
  });

  it('returns the evaluation result on success', async () => {
    const body: EvaluationResult = {
      satisfied: true, reason: 'is positive', assertions: ['is positive'],
      values: ['is positive'], justification: 'is positive',
      explanation: { assertions: ['is positive'], underlying: [] },
    };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.evaluate({ modelType: 'number', document: { rule: { spec: 'is-positive' } }, model: 5 });

    expect(result.satisfied).toBe(true);
  });

  it('throws RulesApiError carrying validation errors on a 400', async () => {
    const body: ValidationResponse = { errors: [{ path: '$.rule', code: 'UnknownSpec', message: 'nope' }] };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body, 400));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    await expect(client.evaluate({ modelType: 'number', document: { rule: { spec: 'x' } }, model: 5 }))
      .rejects.toMatchObject({ status: 400, errors: body.errors });
    await expect(client.evaluate({ modelType: 'number', document: { rule: { spec: 'x' } }, model: 5 }))
      .rejects.toBeInstanceOf(RulesApiError);
  });

  describe('rule endpoints', () => {
    it('lists rules', async () => {
      const entries = [{
        name: 'can-checkout', modelType: 'customer', metadataType: 'String',
        isAsync: false, isPolicy: false, version: 1, description: null,
      }];
      const fetchMock = vi.fn().mockResolvedValue(jsonResponse(entries));
      const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

      const result = await client.listRules();

      expect(result).toEqual(entries);
      expect(fetchMock).toHaveBeenCalledWith('/api/rules/rules', { method: 'GET' });
    });

    it('gets a rule document with its version', async () => {
      const body = { document: { rule: { spec: 'is-active' } }, version: 3 };
      const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body));
      const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

      const result = await client.getRule('can-checkout');

      expect(result).toEqual(body);
      expect(fetchMock).toHaveBeenCalledWith('/api/rules/rules/can-checkout', { method: 'GET' });
    });

    it('puts a rule and returns the updated version', async () => {
      const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ version: 2 }));
      const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

      const result = await client.putRule('can-checkout', { rule: { spec: 'is-active' } }, 1);

      const [url, init] = fetchMock.mock.calls[0]!;
      expect(url).toBe('/api/rules/rules/can-checkout');
      expect(init.method).toBe('PUT');
      expect(JSON.parse(init.body)).toEqual({ document: { rule: { spec: 'is-active' } }, baseVersion: 1 });
      expect(result).toEqual({ outcome: 'updated', version: 2 });
    });

    it('returns a typed conflict instead of throwing on 409', async () => {
      const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ currentVersion: 4 }, 409));
      const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

      const result = await client.putRule('can-checkout', { rule: { spec: 'is-active' } }, 1);

      expect(result).toEqual({ outcome: 'conflict', currentVersion: 4 });
    });

    it('returns typed validation errors instead of throwing on 400', async () => {
      const errors = [{ path: '$.rule', code: 'UnknownSpec', message: 'x' }];
      const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ errors }, 400));
      const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

      const result = await client.putRule('can-checkout', { rule: { spec: 'nope' } }, 1);

      expect(result).toEqual({ outcome: 'invalid', errors });
    });

    it('throws RulesApiError on a 400 with an unparseable body', async () => {
      // Framework binding failures (e.g. missing baseVersion) return 400 with an empty body.
      const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 400 }));
      const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

      await expect(client.putRule('can-checkout', { rule: { spec: 'is-active' } }, 1))
        .rejects.toMatchObject({ name: 'RulesApiError', status: 400 });
    });

    it('surfaces the server message on a 400 with an error envelope', async () => {
      // Guard failures (missing document, non-positive baseVersion) return 400 { error }.
      const message = 'baseVersion must be a positive integer; versions start at 1';
      const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ error: message }, 400));
      const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

      await expect(client.putRule('can-checkout', { rule: { spec: 'is-active' } }, 0))
        .rejects.toMatchObject({ name: 'RulesApiError', status: 400, message });
    });

    it('throws RulesApiError on a 404 as elsewhere', async () => {
      const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ error: 'Unknown rule.' }, 404));
      const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

      await expect(client.putRule('nope', { rule: { spec: 'is-active' } }, 1))
        .rejects.toMatchObject({ status: 404, message: 'Unknown rule.' });
    });

    it('reverts a rule via delete with the base version in the query', async () => {
      let requested: string | undefined;
      const fetchSpy: typeof fetch = async (input, init) => {
        requested = String(input);
        expect(init?.method).toBe('DELETE');
        return jsonResponse({ version: 5 });
      };
      const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchSpy });

      const result = await client.revertRule('can-checkout', 4);

      expect(requested).toBe('/api/rules/rules/can-checkout?baseVersion=4');
      expect(result).toEqual({ outcome: 'updated', version: 5 });
    });
  });

  it('calls the global fetch with the correct this-binding when none is injected', async () => {
    // Browsers throw "Illegal invocation" if fetch is called detached from the global object;
    // simulate that check to guard against passing an unbound globalThis.fetch to the client.
    const original = globalThis.fetch;
    globalThis.fetch = function boundOnlyFetch(this: unknown): Promise<Response> {
      if (this !== globalThis) throw new TypeError('Failed to execute \'fetch\': Illegal invocation');
      return Promise.resolve(jsonResponse([]));
    } as typeof fetch;
    try {
      const client = new RulesApiClient({ baseUrl: '/api/rules' });
      await expect(client.getCatalog()).resolves.toEqual([]);
    } finally {
      globalThis.fetch = original;
    }
  });

  it('lists propositions', async () => {
    const entries = [{
      name: 'customer.is-active', modelType: 'customer', metadataType: 'String',
      isAsync: false, origin: 'Compiled', version: 0, description: null, quarantine: [],
    }];
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(entries));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.listPropositions();

    expect(result).toEqual(entries);
    expect(fetchMock).toHaveBeenCalledWith('/api/rules/propositions', { method: 'GET' });
  });

  it('passes a dotted name through the path unmangled', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({
      document: null, version: 0, origin: 'Compiled', hasCompiledDefault: true,
    }));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    await client.getProposition('customer.eligibility.is-active');

    // encodeURIComponent leaves '.' untouched — this proves dots survive rather than
    // getting escaped to %2E, not that encoding happens at all (see the next test for that).
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/rules/propositions/customer.eligibility.is-active', { method: 'GET' });
  });

  it('encodes a name containing a character that requires escaping', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({
      document: null, version: 0, origin: 'Compiled', hasCompiledDefault: true,
    }));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    await client.getProposition('customer is-active');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/rules/propositions/customer%20is-active', { method: 'GET' });
  });

  it('creates a proposition and reports the new version', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ version: 1 }, 201));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.createProposition({
      name: 'customer.derived', modelType: 'customer',
      document: { rule: { spec: 'customer.is-active' } }, description: null,
    });

    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/rules/propositions');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body)).toEqual({
      name: 'customer.derived', modelType: 'customer',
      document: { rule: { spec: 'customer.is-active' } }, description: null,
    });
    expect(result).toEqual({ outcome: 'saved', version: 1 });
  });

  it('reports a duplicate name as a typed outcome rather than throwing', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({ error: "A proposition is already authored under 'customer.derived'." }, 409));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.createProposition({
      name: 'customer.derived', modelType: 'customer',
      document: { rule: { spec: 'customer.is-active' } }, description: null,
    });

    expect(result.outcome).toBe('nameTaken');
  });

  it('reports a stale base version as a conflict', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ currentVersion: 3 }, 409));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.putProposition('customer.a', { rule: { spec: 'customer.is-active' } }, 1);

    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/rules/propositions/customer.a');
    expect(init.method).toBe('PUT');
    expect(JSON.parse(init.body)).toEqual({
      document: { rule: { spec: 'customer.is-active' } }, baseVersion: 1,
    });
    expect(result).toEqual({ outcome: 'conflict', currentVersion: 3 });
  });

  it('reports broken dependents separately from document errors', async () => {
    const body = {
      errors: [],
      brokenDependents: [{ name: 'can-checkout', kind: 'rule', errors: [
        { path: '$', code: 'AsyncSpecInSyncLoad', message: 'would not bind' }] }],
    };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body, 400));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.putProposition('customer.a', { rule: { spec: 'customer.is-active' } }, 1);

    expect(result.outcome).toBe('invalid');
    if (result.outcome !== 'invalid') throw new Error('unreachable');
    expect(result.brokenDependents[0]!.name).toBe('can-checkout');
    expect(result.errors).toEqual([]);
  });

  it('reports referrers blocking a delete', async () => {
    let requested: string | undefined;
    const fetchSpy: typeof fetch = async (input, init) => {
      requested = String(input);
      expect(init?.method).toBe('DELETE');
      return jsonResponse({ referrers: ['customer.b'] }, 409);
    };
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchSpy });

    const result = await client.deleteProposition('customer.a', 1);

    expect(requested).toBe('/api/rules/propositions/customer.a?baseVersion=1');
    expect(result.outcome).toBe('referenced');
    if (result.outcome !== 'referenced') throw new Error('unreachable');
    expect(result.referrers).toEqual(['customer.b']);
  });

  it('gets the transitive dependents of a proposition', async () => {
    const dependents = [{ name: 'customer.b', kind: 'proposition' }];
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ dependents }));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.getDependents('customer.a');

    expect(result).toEqual(dependents);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/rules/propositions/customer.a/dependents', { method: 'GET' });
  });
});
