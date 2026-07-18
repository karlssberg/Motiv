import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { RuleEditorStore } from '../src/editor.js';
import { createValidationController } from '../src/validation.js';
import type { RulesApiClient } from '../src/client.js';
import type { ValidationResponse } from '../src/contracts.js';

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

function fakeClient(response: ValidationResponse) {
  return { validate: vi.fn().mockResolvedValue(response) } as unknown as RulesApiClient;
}

describe('createValidationController', () => {
  it('debounces edits into a single validate call and pushes errors to the store', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const response: ValidationResponse = { errors: [{ path: '$.rule', code: 'UnknownSpec', message: 'x' }] };
    const client = fakeClient(response);
    const dispose = createValidationController(store, client, { modelType: 'number', debounceMs: 100 });

    store.replaceNode('$.rule', { spec: 'b' });
    store.replaceNode('$.rule', { spec: 'c' });
    expect(client.validate).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(100);

    expect(client.validate).toHaveBeenCalledTimes(1);
    expect(client.validate).toHaveBeenCalledWith({ modelType: 'number', document: store.getState().document });
    expect(store.getState().errors).toEqual(response.errors);
    dispose();
  });

  it('stops validating after dispose', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const client = fakeClient({ errors: [] });
    const dispose = createValidationController(store, client, { modelType: 'number', debounceMs: 100 });

    dispose();
    store.replaceNode('$.rule', { spec: 'b' });
    await vi.advanceTimersByTimeAsync(100);

    expect(client.validate).not.toHaveBeenCalled();
  });
});
