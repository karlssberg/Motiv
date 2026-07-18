import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { RuleEditorStore, type EvaluationResult, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { EvaluatePane } from '../src/EvaluatePane.js';

const evaluation: EvaluationResult = {
  satisfied: true,
  reason: 'customer is active',
  assertions: ['customer is active'],
  values: ['customer is active'],
  justification: 'customer is active',
  explanation: { assertions: ['customer is active'], underlying: [] },
};

function client(evaluate = vi.fn().mockResolvedValue(evaluation)): RulesApiClient {
  return { evaluate } as unknown as RulesApiClient;
}

describe('EvaluatePane', () => {
  it('evaluates the current document against the sample model and renders the outcome', async () => {
    const evaluate = vi.fn().mockResolvedValue(evaluation);
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    render(
      <RuleEditorProvider store={store}>
        <EvaluatePane client={client(evaluate)} />
      </RuleEditorProvider>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Evaluate' }));

    await waitFor(() => expect(screen.getByLabelText('outcome').textContent).toContain('Satisfied'));
    expect(evaluate).toHaveBeenCalledWith(
      expect.objectContaining({ modelType: 'customer', document: store.getState().document }),
    );
    expect(screen.getByText('customer is active')).toBeDefined();
  });
});
