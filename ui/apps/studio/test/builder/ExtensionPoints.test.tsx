import { describe, it, expect, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { EditorPane } from '../../src/panes/EditorPane.js';

const catalog = {
  specs: [{ name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: null }],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><EditorPane client={client()} modelType="customer" /></RuleEditorProvider>);

describe('extension points', () => {
  it('shows no disabled placeholder affordance, in the chrome or a leaf\'s detail panel', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-adult' } });
    renderWith(store);
    // Nothing greyed-out sits in the pane header at rest: a disabled button for a feature that
    // does not exist yet is noise in the primary chrome, so the parameters placeholder is gone.
    expect(screen.queryByRole('button', { name: /parameters .*coming/i })).toBeNull();

    // A leaf's own extension point used to be a disabled "expression — coming" button in its
    // detail panel; expression editing is now the row itself (typed DSL, completion included),
    // so the placeholder is gone there too, with nothing left to replace it.
    fireEvent.click(await screen.findByRole('button', { name: 'details for $.rule' }));
    expect(screen.queryByRole('button', { name: /expression .*coming/i })).toBeNull();
  });
});
