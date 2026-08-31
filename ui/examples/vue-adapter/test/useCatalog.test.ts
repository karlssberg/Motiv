import { describe, it, expect, vi } from 'vitest';
import { shallowRef } from 'vue';
import type { Catalog, RulesApiClient } from '@motiv-rules/core';
import { useCatalog } from '../src/useCatalog.js';
import { inScope } from './scope.js';

const catalog = { specs: [], collections: [], modelTypes: [] } as unknown as Catalog;

function makeClient(getCatalog: () => Promise<Catalog>): RulesApiClient {
  return { getCatalog: vi.fn(getCatalog) } as unknown as RulesApiClient;
}

describe('useCatalog', () => {
  it('starts loading and settles on the catalog', async () => {
    const client = makeClient(async () => catalog);
    const { value: state, stop } = inScope(() => useCatalog(client));

    expect(state.value).toEqual({ status: 'loading' });
    await vi.waitFor(() => expect(state.value).toEqual({ status: 'ready', data: catalog }));
    stop();
  });

  it('reports a failed load', async () => {
    const client = makeClient(async () => { throw new Error('offline'); });
    const { value: state, stop } = inScope(() => useCatalog(client));

    await vi.waitFor(() => expect(state.value.status).toBe('error'));
    stop();
  });

  it('reloads when the client changes, and ignores the reply from the one it dropped', async () => {
    let releaseFirst!: (value: Catalog) => void;
    const first = makeClient(() => new Promise<Catalog>((resolve) => { releaseFirst = resolve; }));
    const second = makeClient(async () => catalog);
    const client = shallowRef(first);
    const { value: state, stop } = inScope(() => useCatalog(client));

    client.value = second;
    await vi.waitFor(() => expect(state.value).toEqual({ status: 'ready', data: catalog }));

    // The dropped client answers late. It must not overwrite the current client's catalog.
    releaseFirst({ specs: [{ name: 'stale' }] } as unknown as Catalog);
    await Promise.resolve();
    expect(state.value).toEqual({ status: 'ready', data: catalog });
    stop();
  });

  it('drops a reply that lands after the scope ended', async () => {
    let release!: (value: Catalog) => void;
    const client = makeClient(() => new Promise<Catalog>((resolve) => { release = resolve; }));
    const { value: state, stop } = inScope(() => useCatalog(client));

    stop();
    release(catalog);
    await Promise.resolve();
    expect(state.value).toEqual({ status: 'loading' });
  });
});
