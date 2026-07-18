import { useCallback, useState } from 'react';
import type { EvaluateRequest, EvaluationResult, RulesApiClient } from '@motiv/rules-core';

type EvaluationStatus =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; result: EvaluationResult }
  | { status: 'error'; error: unknown };

/** The evaluation state plus a trigger to run an evaluation. */
export type EvaluationState = EvaluationStatus & {
  evaluate: (request: EvaluateRequest) => Promise<void>;
};

/** Exposes an evaluate() trigger and tracks the async result. */
export function useEvaluation(client: RulesApiClient): EvaluationState {
  const [state, setState] = useState<EvaluationStatus>({ status: 'idle' });

  const evaluate = useCallback(async (request: EvaluateRequest): Promise<void> => {
    setState({ status: 'loading' });
    try {
      const result = await client.evaluate(request);
      setState({ status: 'ready', result });
    } catch (error) {
      setState({ status: 'error', error });
    }
  }, [client]);

  return { ...state, evaluate };
}
