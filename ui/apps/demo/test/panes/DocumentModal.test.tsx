import { describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { DocumentModal } from '../../src/panes/DocumentModal.js';

function renderWith(store: RuleEditorStore) {
  return render(
    <RuleEditorProvider store={store}>
      <DocumentModal onClose={() => {}} />
    </RuleEditorProvider>,
  );
}

describe('DocumentModal', () => {
  it('shows the live document as formatted JSON, following the store', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    expect(screen.getByLabelText('rule document').textContent).toContain('"spec": "is-active"');

    // "live" is the claim the badge makes, so an edit made while it is open has to reach it.
    act(() => store.replaceNode('$.rule', { spec: 'is-adult' }));
    expect(screen.getByLabelText('rule document').textContent).toContain('"spec": "is-adult"');
  });

  it('lists validation errors set on the store', () => {
    const store = new RuleEditorStore({ rule: { spec: 'nope' } });
    renderWith(store);
    act(() => store.setErrors([{ path: '$.rule', code: 'UnknownSpec', message: 'unknown spec' }]));
    expect(screen.getByText(/UnknownSpec/)).toBeTruthy();
    expect(screen.getByText(/unknown spec/)).toBeTruthy();
  });

  it('names itself for assistive technology', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    expect(screen.getByRole('dialog', { name: /document/i })).toBeTruthy();
  });

  it('reports dismissal', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const onClose = vi.fn();
    render(
      <RuleEditorProvider store={store}>
        <DocumentModal onClose={onClose} />
      </RuleEditorProvider>,
    );
    await userEvent.click(screen.getByRole('button', { name: /close/i }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
