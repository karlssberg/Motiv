import { describe, it, expect } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { RuleEditorStore } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { JsonPane } from '../../src/panes/JsonPane.js';

function renderWith(store: RuleEditorStore) {
  return render(
    <RuleEditorProvider store={store}>
      <JsonPane />
    </RuleEditorProvider>,
  );
}

describe('JsonPane', () => {
  it('shows the live document JSON', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    expect(screen.getByLabelText('rule document').textContent).toContain('"spec": "is-active"');

    act(() => store.replaceNode('$.rule', { spec: 'is-adult' }));
    expect(screen.getByLabelText('rule document').textContent).toContain('"spec": "is-adult"');
  });

  it('lists validation errors set on the store', () => {
    const store = new RuleEditorStore({ rule: { spec: 'nope' } });
    renderWith(store);
    act(() => store.setErrors([{ path: '$.rule', code: 'UnknownSpec', message: 'unknown spec' }]));
    expect(screen.getByText(/UnknownSpec/)).toBeDefined();
    expect(screen.getByText(/unknown spec/)).toBeDefined();
  });
});
