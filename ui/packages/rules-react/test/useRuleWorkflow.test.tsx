import { describe, it, expect, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv-rules/core';
import { useRuleWorkflow } from '../src/workflow/useRuleWorkflow.js';

const listing = [
  {
    name: 'can-checkout', modelType: 'customer', metadataType: 'String',
    isAsync: false, isPolicy: false, version: 1, description: null,
  },
];

function makeClient(overrides: Partial<Record<string, unknown>> = {}): RulesApiClient {
  return {
    listRules: vi.fn().mockResolvedValue(listing),
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'is-adult' } }, version: 3 }),
    putRule: vi.fn().mockResolvedValue({ outcome: 'updated', version: 4 }),
    ...overrides,
  } as unknown as RulesApiClient;
}

describe('useRuleWorkflow', () => {
  it('exposes the controller state, updating as actions land', async () => {
    const client = makeClient();
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useRuleWorkflow(client, store));

    expect(result.current.loaded).toBeNull();

    await act(() => result.current.refresh());
    expect(result.current.rules).toEqual(listing);

    await act(() => result.current.load('can-checkout'));
    expect(result.current.loaded).toEqual({ name: 'can-checkout', version: 3, isCodeDefault: false });
    expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } });

    await act(() => result.current.save());
    expect(client.putRule).toHaveBeenCalledWith('can-checkout', store.getState().document, 3);
    await waitFor(() => expect(result.current.loaded?.version).toBe(4));
  });

  it('binds a fresh controller when the client changes', async () => {
    const first = makeClient();
    const second = makeClient();
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result, rerender } = renderHook(
      ({ client }) => useRuleWorkflow(client, store),
      { initialProps: { client: first } },
    );

    await act(() => result.current.refresh());
    expect(result.current.rules).toEqual(listing);

    rerender({ client: second });
    // A different client is a different server world: nothing carries over.
    expect(result.current.rules).toEqual([]);

    await act(() => result.current.refresh());
    expect(second.listRules).toHaveBeenCalled();
  });
});
