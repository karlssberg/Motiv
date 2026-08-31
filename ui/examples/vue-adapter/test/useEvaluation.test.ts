import { describe, it, expect, vi } from 'vitest';
import type { EvaluationResult, RulesApiClient } from '@motiv-rules/core';
import { useEvaluation } from '../src/useEvaluation.js';
import { inScope } from './scope.js';

const result = { satisfied: true, reason: 'is-active == true' } as unknown as EvaluationResult;

describe('useEvaluation', () => {
  it('is idle until asked, then reports the result', async () => {
    const client = { evaluate: vi.fn().mockResolvedValue(result) } as unknown as RulesApiClient;
    const { value: evaluation, stop } = inScope(() => useEvaluation(client));

    expect(evaluation.state.value).toEqual({ status: 'idle' });

    const pending = evaluation.evaluate({ document: { rule: { spec: 'is-active' } } } as never);
    expect(evaluation.state.value).toEqual({ status: 'loading' });

    await pending;
    expect(evaluation.state.value).toEqual({ status: 'ready', result });
    stop();
  });

  it('reports a failed evaluation', async () => {
    const client = {
      evaluate: vi.fn().mockRejectedValue(new Error('bad request')),
    } as unknown as RulesApiClient;
    const { value: evaluation, stop } = inScope(() => useEvaluation(client));

    await evaluation.evaluate({ document: { rule: { spec: 'is-active' } } } as never);
    expect(evaluation.state.value.status).toBe('error');
    stop();
  });
});
