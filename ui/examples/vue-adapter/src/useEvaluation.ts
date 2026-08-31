import { shallowRef, toValue, type MaybeRefOrGetter, type ShallowRef } from 'vue';
import type { EvaluateRequest, EvaluationResult, RulesApiClient } from '@motiv-rules/core';

/** The state of an evaluation the consumer has asked for. */
export type EvaluationState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; result: EvaluationResult }
  | { status: 'error'; error: unknown };

/** The evaluation state plus the trigger that drives it. */
export interface Evaluation {
  readonly state: Readonly<ShallowRef<EvaluationState>>;
  evaluate(request: EvaluateRequest): Promise<void>;
}

/** Exposes an `evaluate()` trigger and tracks the async result. */
export function useEvaluation(client: MaybeRefOrGetter<RulesApiClient>): Evaluation {
  const state = shallowRef<EvaluationState>({ status: 'idle' });

  const evaluate = async (request: EvaluateRequest): Promise<void> => {
    state.value = { status: 'loading' };
    try {
      state.value = { status: 'ready', result: await toValue(client).evaluate(request) };
    } catch (error) {
      state.value = { status: 'error', error };
    }
  };

  return { state, evaluate };
}
