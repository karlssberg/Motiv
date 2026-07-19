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
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(catalog), { status: 200 }));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch });

    const result = await client.getCatalog();

    expect(result).toEqual(catalog);
    expect(fetch).toHaveBeenCalledWith('/api/rules/catalog', { method: 'GET' });
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
});
