import { describe, it, expect, vi } from 'vitest';
import { shallowRef } from 'vue';
import {
  RuleEditorStore, type PropositionCreateRequest, type RulesApiClient,
} from '@motiv-rules/core';
import { usePropositionWorkflow } from '../src/workflow/usePropositionWorkflow.js';
import { inScope } from './scope.js';

const entries = [
  {
    name: 'customer.is-active', origin: 'Authored', modelType: 'customer',
    metadataType: 'String', isAsync: false, isPolicy: true, version: 2, description: null,
  },
];

function makeClient(): RulesApiClient {
  return {
    listPropositions: vi.fn().mockResolvedValue(entries),
    getProposition: vi.fn().mockResolvedValue({
      document: { rule: { spec: 'is-adult' } }, version: 2, origin: 'Authored',
    }),
    getDependents: vi.fn().mockResolvedValue([]),
    putProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 3 }),
    createProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 1 }),
  } as unknown as RulesApiClient;
}

const creation = (name: string): PropositionCreateRequest => ({
  name, modelType: 'customer', document: { rule: { spec: 'is-adult' } }, description: null,
});

describe('usePropositionWorkflow', () => {
  it('exposes the controller state, updating as actions land', async () => {
    const client = makeClient();
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: workflow, stop } = inScope(() => usePropositionWorkflow(client, store));

    await workflow.refreshEntries();
    expect(workflow.state.value.entries).toEqual(entries);

    await workflow.select('customer.is-active');
    expect(workflow.state.value.loaded).toEqual({ name: 'customer.is-active', version: 2 });
    expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } });
    stop();
  });

  it('hands the selection over to the callback the consumer holds now', async () => {
    const client = makeClient();
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const first = vi.fn();
    const options = shallowRef({ onSelect: first });
    const { value: workflow, stop } = inScope(() => usePropositionWorkflow(client, store, options));

    expect(await workflow.create(creation('customer.is-vip'))).toBeNull();
    expect(first).toHaveBeenCalledWith('customer.is-vip');

    // The consumer swaps the callback — an inline closure rebuilt on render is the ordinary case.
    // The handover must reach the one it holds now, not the one the controller was built around;
    // that is the defect `@motiv-rules/react` spends a ref and an effect to avoid.
    const second = vi.fn();
    options.value = { onSelect: second };

    expect(await workflow.create(creation('customer.is-gold'))).toBeNull();
    expect(second).toHaveBeenCalledWith('customer.is-gold');
    expect(first).not.toHaveBeenCalledWith('customer.is-gold');
    stop();
  });

  it('runs with no options at all', async () => {
    const client = makeClient();
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: workflow, stop } = inScope(() => usePropositionWorkflow(client, store));

    expect(await workflow.create(creation('customer.is-vip'))).toBeNull();
    expect(workflow.state.value.entries).toEqual(entries);
    stop();
  });
});
