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
  render(<RuleEditorProvider store={store}><EditorPane client={client()} /></RuleEditorProvider>);

describe('extension points', () => {
  it('shows expression as a disabled affordance inside the detail panel, and nothing in the chrome', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-adult' } });
    renderWith(store);
    // Nothing greyed-out sits in the pane header at rest: a disabled button for a feature that
    // does not exist yet is noise in the primary chrome, so the parameters placeholder is gone.
    expect(screen.queryByRole('button', { name: /parameters .*coming/i })).toBeNull();

    // The node's own extension point lives in its detail panel, which starts closed.
    fireEvent.click(await screen.findByRole('button', { name: 'details for $.rule' }));

    const expr = screen.getByRole('button', { name: /expression .*coming/i }) as HTMLButtonElement;
    expect(expr.disabled).toBe(true);
  });
});
