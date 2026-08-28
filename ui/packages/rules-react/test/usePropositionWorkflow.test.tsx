import { describe, it, expect, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { RuleEditorStore, type PropositionListEntry, type RulesApiClient } from '@motiv-rules/core';
import { usePropositionWorkflow } from '../src/workflow/usePropositionWorkflow.js';

const entries: PropositionListEntry[] = [
  {
    name: 'pricing.is-vip', modelType: 'customer', metadataType: 'String',
    isAsync: false, origin: 'Authored', version: 2, description: null, quarantine: [],
  },
];

function makeClient(overrides: Partial<Record<string, unknown>> = {}): RulesApiClient {
  return {
    listPropositions: vi.fn().mockResolvedValue(entries),
    getProposition: vi.fn().mockResolvedValue({
      document: { rule: { spec: 'is-adult' } }, version: 2, origin: 'Authored', hasCompiledDefault: false,
    }),
    getDependents: vi.fn().mockResolvedValue([{ name: 'can-checkout', kind: 'rule' }]),
    putProposition: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 5 }),
    deleteProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 0 }),
    createProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 1 }),
    ...overrides,
  } as unknown as RulesApiClient;
}

describe('usePropositionWorkflow', () => {
  it('exposes selection, blast radius and failure text', async () => {
    const client = makeClient();
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => usePropositionWorkflow(client, store));

    await act(() => result.current.select('pricing.is-vip'));
    expect(result.current.loaded).toEqual({ name: 'pricing.is-vip', version: 2 });
    expect(result.current.dependents).toEqual([{ name: 'can-checkout', kind: 'rule' }]);

    await act(() => result.current.save());
    expect(result.current.failure).toBe('Someone else saved version 5. Reload before saving again.');
  });

  it('reports the handover through the latest onSelect callback', async () => {
    const client = makeClient();
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const early = vi.fn();
    const late = vi.fn();
    const { result, rerender } = renderHook(
      ({ onSelect }) => usePropositionWorkflow(client, store, { onSelect }),
      { initialProps: { onSelect: early } },
    );

    rerender({ onSelect: late });
    const failure = await act(() => result.current.create({
      name: 'pricing.is-gold', modelType: 'customer',
      document: { rule: { spec: 'is-adult' } }, description: null,
    }));

    expect(failure).toBeNull();
    // The consumer swapped its callback after the controller was built; the handover must reach
    // the one it holds now, not a stale closure captured at construction.
    expect(late).toHaveBeenCalledWith('pricing.is-gold');
    expect(early).not.toHaveBeenCalled();
  });

  it('a delete hands the selection over as null', async () => {
    const client = makeClient();
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const onSelect = vi.fn();
    const { result } = renderHook(() => usePropositionWorkflow(client, store, { onSelect }));

    await act(() => result.current.select('pricing.is-vip'));
    await act(() => result.current.remove(entries[0]!));

    expect(onSelect).toHaveBeenCalledWith(null);
  });
});
