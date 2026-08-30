import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
    { name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
  ],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

const COMPOSITE = { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] };

/**
 * Ticket 18's answer, tested: the accordion is nested labelled groups plus disclosure — never a
 * `tree` — and every group is named by the DSL text the product already generates for it, which
 * is what carries the *composition* to a reader who cannot see the indentation.
 */
describe('builder accessibility', () => {
  it('is not a tree, which is a navigation pattern and not an editing one', async () => {
    renderWith(new RuleEditorStore({ rule: COMPOSITE }));
    await screen.findByRole('button', { name: 'details for $.rule.and[0]' });

    expect(screen.queryAllByRole('tree')).toHaveLength(0);
    expect(screen.queryAllByRole('treeitem')).toHaveLength(0);
  });

  it("names the group holding a node's operands by that node's generated expression", async () => {
    renderWith(new RuleEditorStore({ rule: COMPOSITE }));
    await screen.findByRole('button', { name: 'details for $.rule.and[0]' });

    expect(screen.getByRole('group', { name: 'is-active & is-adult' })).toBeDefined();
  });

  it('describes the whole composition by the same generated text the strip shows', async () => {
    renderWith(new RuleEditorStore({ rule: COMPOSITE }));
    await screen.findByRole('button', { name: 'details for $.rule.and[0]' });

    const composition = screen.getByRole('group', { name: 'rule composition' });
    const describedBy = composition.getAttribute('aria-describedby');
    expect(describedBy).not.toBeNull();
    expect(document.getElementById(describedBy!)?.textContent).toBe('is-active & is-adult');
  });

  it("points a parent's disclosure at the group it opens", async () => {
    renderWith(new RuleEditorStore({ rule: COMPOSITE }));
    const caret = await screen.findByRole('button', { name: 'collapse $.rule' });

    const controls = caret.getAttribute('aria-controls');
    expect(controls).not.toBeNull();
    expect(document.getElementById(controls!)).toBe(screen.getByRole('group', { name: 'is-active & is-adult' }));
  });

  it('drops that reference while the group is unmounted, rather than naming nothing', async () => {
    renderWith(new RuleEditorStore({ rule: COMPOSITE }));
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));

    const caret = screen.getByRole('button', { name: 'expand $.rule' });
    expect(caret.getAttribute('aria-controls')).toBeNull();
    expect(screen.queryByRole('group', { name: 'is-active & is-adult' })).toBeNull();
  });

  it('gives a leaf rule no operand group, since it has no operands to hold', async () => {
    renderWith(new RuleEditorStore({ rule: { spec: 'is-active' } }));
    await screen.findByRole('button', { name: 'details for $.rule' });

    const composition = screen.getByRole('group', { name: 'rule composition' });
    const describedBy = composition.getAttribute('aria-describedby');
    expect(document.getElementById(describedBy!)?.textContent).toBe('is-active');
    expect(screen.queryByRole('group', { name: 'is-active' })).toBeNull();
  });
});
