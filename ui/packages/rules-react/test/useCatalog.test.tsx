import { describe, it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { Catalog, RulesApiClient } from '@motiv/rules-core';
import { useCatalog } from '../src/useCatalog.js';

describe('useCatalog', () => {
  it('loads the catalog and reports ready', async () => {
    const catalog: Catalog = {
      specs: [
        { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
      ],
      collections: [
        { path: 'orders', parentModelType: 'customer', elementModelType: 'order' },
      ],
    };
    const client = { getCatalog: vi.fn().mockResolvedValue(catalog) } as unknown as RulesApiClient;
    const { result } = renderHook(() => useCatalog(client));

    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.status === 'ready' && result.current.data).toEqual(catalog);
  });

  it('reports error when the fetch fails', async () => {
    const client = { getCatalog: vi.fn().mockRejectedValue(new Error('boom')) } as unknown as RulesApiClient;
    const { result } = renderHook(() => useCatalog(client));
    await waitFor(() => expect(result.current.status).toBe('error'));
  });
});
