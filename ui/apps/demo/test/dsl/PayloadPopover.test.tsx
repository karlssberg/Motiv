import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore } from '@motiv/rules-core';
import type { Catalog } from '@motiv/rules-core';
import { PayloadPopover } from '../../src/dsl/PayloadPopover.js';

const CATALOG: Catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Currently active.' },
    { name: 'is-tiered', modelType: 'customer', metadataType: 'Tier', isAsync: false, description: 'Tiered.' },
  ],
  collections: [],
  metadataTypes: {
    Tier: { type: 'object', properties: { tier: { type: 'string' } } },
  },
};

function renderPopover() {
  const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
  const onClose = vi.fn();
  render(
    <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-active" onClose={onClose} />,
  );
  return { store, onClose };
}

describe('PayloadPopover', () => {
  it('shows the spec name and its catalog description', () => {
    renderPopover();
    expect(screen.getByText('is-active')).toBeTruthy();
    expect(screen.getByText(/Currently active/)).toBeTruthy();
  });

  it('saves the node name to the store', async () => {
    const user = userEvent.setup();
    const { store } = renderPopover();

    await user.type(screen.getByLabelText('Name'), 'activity');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(store.getState().document.rule).toMatchObject({ name: 'activity' });
  });

  it('saves string payloads for an Explanation spec', async () => {
    const user = userEvent.setup();
    const { store } = renderPopover();

    await user.type(screen.getByLabelText('When true'), 'is active');
    await user.type(screen.getByLabelText('When false'), 'not active');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(store.getState().document.rule).toMatchObject({
      whenTrue: 'is active', whenFalse: 'not active',
    });
  });

  it('saves object payloads for an object metadata spec', async () => {
    const user = userEvent.setup();
    const store = new RuleEditorStore({ rule: { spec: 'is-tiered', name: 'tier' } });
    render(
      <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-tiered" onClose={vi.fn()} />,
    );

    const whenTrue = screen.getByLabelText('When true');
    await user.clear(whenTrue);
    await user.type(whenTrue, '{{"tier": "gold"}');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(store.getState().document.rule).toMatchObject({ whenTrue: { tier: 'gold' } });
  });

  it('reports invalid JSON instead of saving', async () => {
    const user = userEvent.setup();
    const store = new RuleEditorStore({ rule: { spec: 'is-tiered', name: 'tier' } });
    render(
      <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-tiered" onClose={vi.fn()} />,
    );

    await user.type(screen.getByLabelText('When true'), '{{not json');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(screen.getByRole('alert')).toBeTruthy();
    expect(store.getState().document.rule).not.toHaveProperty('whenTrue');
  });

  it('closes without saving on cancel', async () => {
    const user = userEvent.setup();
    const { store, onClose } = renderPopover();

    await user.type(screen.getByLabelText('Name'), 'ignored');
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalled();
    expect(store.getState().document.rule).not.toHaveProperty('name');
  });

  it('pre-fills existing decorations', () => {
    const store = new RuleEditorStore({
      rule: { spec: 'is-active', name: 'activity', whenTrue: 'yes', whenFalse: 'no' },
    });
    render(
      <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-active" onClose={vi.fn()} />,
    );

    expect(screen.getByLabelText<HTMLInputElement>('Name').value).toBe('activity');
    expect(screen.getByLabelText<HTMLTextAreaElement>('When true').value).toBe('yes');
  });

  it('pretty-prints an existing object payload for editing', () => {
    const store = new RuleEditorStore({
      rule: { spec: 'is-tiered', name: 'tier', whenTrue: { tier: 'gold' } },
    });
    render(
      <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-tiered" onClose={vi.fn()} />,
    );

    expect(screen.getByLabelText<HTMLTextAreaElement>('When true').value)
      .toBe('{\n  "tier": "gold"\n}');
  });

  it('closes after a successful save', async () => {
    const user = userEvent.setup();
    const { onClose } = renderPopover();

    await user.type(screen.getByLabelText('Name'), 'activity');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onClose).toHaveBeenCalled();
  });
});
