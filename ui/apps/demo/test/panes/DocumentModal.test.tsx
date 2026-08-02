import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { DocumentModal } from '../../src/panes/DocumentModal.js';

function renderWith(store: RuleEditorStore) {
  return render(
    <RuleEditorProvider store={store}>
      <DocumentModal onClose={() => {}} />
    </RuleEditorProvider>,
  );
}

describe('DocumentModal', () => {
  it('shows the live document as formatted JSON', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    expect(screen.getByLabelText('rule document')).toBeTruthy();
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
