import { describe, it, expect, vi } from 'vitest';
import { shallowRef } from 'vue';
import { RuleEditorStore, type RulesApiClient } from '@motiv-rules/core';
import { useRuleWorkflow } from '../src/workflow/useRuleWorkflow.js';
import { inScope } from './scope.js';

const listing = [
  {
    name: 'can-checkout', modelType: 'customer', metadataType: 'String',
    isAsync: false, isPolicy: false, version: 1, description: null,
  },
];

function makeClient(): RulesApiClient {
  return {
    listRules: vi.fn().mockResolvedValue(listing),
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'is-adult' } }, version: 3 }),
    putRule: vi.fn().mockResolvedValue({ outcome: 'updated', version: 4 }),
  } as unknown as RulesApiClient;
}

describe('useRuleWorkflow', () => {
  it('exposes the controller state, updating as actions land', async () => {
    const client = makeClient();
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: workflow, stop } = inScope(() => useRuleWorkflow(client, store));

    expect(workflow.state.value.loaded).toBeNull();

    await workflow.refresh();
    expect(workflow.state.value.rules).toEqual(listing);

    await workflow.load('can-checkout');
    expect(workflow.state.value.loaded).toEqual({ name: 'can-checkout', version: 3, isCodeDefault: false });
    expect(store.getState().document).toEqual({ rule: { spec: 'is-adult' } });

    await workflow.save();
    expect(client.putRule).toHaveBeenCalledWith('can-checkout', store.getState().document, 3);
    expect(workflow.state.value.loaded?.version).toBe(4);
    stop();
  });

  it('binds a fresh controller when the client changes', async () => {
    const first = makeClient();
    const second = makeClient();
    const client = shallowRef(first);
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: workflow, stop } = inScope(() => useRuleWorkflow(client, store));

    await workflow.refresh();
    expect(workflow.state.value.rules).toEqual(listing);

    client.value = second;
    // A different client is a different server world: nothing carries over.
    expect(workflow.state.value.rules).toEqual([]);

    await workflow.refresh();
    expect(second.listRules).toHaveBeenCalled();
    stop();
  });
});
