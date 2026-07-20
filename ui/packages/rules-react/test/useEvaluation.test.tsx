import { describe, it, expect, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { EvaluationResult, RulesApiClient } from '@motiv/rules-core';
import { useEvaluation } from '../src/useEvaluation.js';

const result: EvaluationResult = {
  satisfied: true, reason: 'is positive', assertions: ['is positive'],
  values: ['is positive'], justification: 'is positive',
  explanation: { assertions: ['is positive'], underlying: [] },
};

describe('useEvaluation', () => {
  it('starts idle, then reports the result after evaluate()', async () => {
    const client = { evaluate: vi.fn().mockResolvedValue(result) } as unknown as RulesApiClient;
    const { result: hook } = renderHook(() => useEvaluation(client));

    expect(hook.current.status).toBe('idle');

    await act(async () => {
      await hook.current.evaluate({ modelType: 'number', document: { rule: { spec: 'is-positive' } }, model: 5 });
    });

    await waitFor(() => expect(hook.current.status).toBe('ready'));
    expect(hook.current.status === 'ready' && hook.current.result.satisfied).toBe(true);
  });

  it('reports error when evaluate rejects', async () => {
    const client = { evaluate: vi.fn().mockRejectedValue(new Error('bad')) } as unknown as RulesApiClient;
    const { result: hook } = renderHook(() => useEvaluation(client));
    await act(async () => {
      await hook.current.evaluate({ modelType: 'number', document: { rule: { spec: 'x' } }, model: 5 });
    });
    await waitFor(() => expect(hook.current.status).toBe('error'));
  });
});
