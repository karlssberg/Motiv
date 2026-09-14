import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore } from '@motiv-rules/core';
import type { Catalog } from '@motiv-rules/core';
import { PayloadPopover } from '../../src/dsl/PayloadPopover.js';

const CATALOG: Catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Currently active.', origin: 'Compiled' },
    { name: 'is-tiered', modelType: 'customer', metadataType: 'Tier', isAsync: false, description: 'Tiered.', origin: 'Compiled' },
  ],
  collections: [],
  metadataTypes: {
    Tier: { type: 'object', properties: { tier: { type: 'string' } } },
  },
};

const DESCRIBE = 'Describe what it means for this to be true or false…';
const REMOVE = 'Remove these descriptions';
/** The writable card keeps its payload fields behind a link; this takes it up. */
const openPayloadFields = (user: ReturnType<typeof userEvent.setup>) =>
  user.click(screen.getByRole('button', { name: DESCRIBE }));

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

  it('offers no Name field on the root — the DSL has no inline name', () => {
    renderPopover();
    expect(screen.queryByLabelText('Name')).toBeNull();
    expect(screen.getByRole('button', { name: DESCRIBE })).toBeTruthy();
  });

  it('saves string payloads for an Explanation spec', async () => {
    const user = userEvent.setup();
    const { store } = renderPopover();

    await openPayloadFields(user);
    await user.type(screen.getByLabelText('When true'), 'is active');
    await user.type(screen.getByLabelText('When false'), 'not active');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(store.getState().document.rule).toMatchObject({
      whenTrue: 'is active', whenFalse: 'not active',
    });
  });

  it('saves object payloads for an object metadata spec', async () => {
    const user = userEvent.setup();
    const store = new RuleEditorStore({ rule: { spec: 'is-tiered' } });
    render(
      <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-tiered" onClose={vi.fn()} />,
    );

    await openPayloadFields(user);
    const whenTrue = screen.getByLabelText('When true');
    await user.clear(whenTrue);
    await user.type(whenTrue, '{{"tier": "gold"}');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(store.getState().document.rule).toMatchObject({ whenTrue: { tier: 'gold' } });
  });

  it('reports invalid JSON instead of saving', async () => {
    const user = userEvent.setup();
    const store = new RuleEditorStore({ rule: { spec: 'is-tiered' } });
    render(
      <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-tiered" onClose={vi.fn()} />,
    );

    await openPayloadFields(user);
    await user.type(screen.getByLabelText('When true'), '{{not json');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(screen.getByRole('alert')).toBeTruthy();
    expect(store.getState().document.rule).not.toHaveProperty('whenTrue');
  });

  it('closes without saving on cancel', async () => {
    const user = userEvent.setup();
    const { store, onClose } = renderPopover();

    await openPayloadFields(user);
    await user.type(screen.getByLabelText('When true'), 'ignored');
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalled();
    expect(store.getState().document.rule).not.toHaveProperty('whenTrue');
  });

  it('pre-fills existing decorations', () => {
    const store = new RuleEditorStore({
      rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' },
    });
    render(
      <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-active" onClose={vi.fn()} />,
    );

    expect(screen.getByLabelText<HTMLTextAreaElement>('When true').value).toBe('yes');
    expect(screen.getByLabelText<HTMLTextAreaElement>('When false').value).toBe('no');
  });

  it('pretty-prints an existing object payload for editing', () => {
    const store = new RuleEditorStore({
      rule: { spec: 'is-tiered', whenTrue: { tier: 'gold' } },
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

    await openPayloadFields(user);
    await user.type(screen.getByLabelText('When true'), 'active');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onClose).toHaveBeenCalled();
  });
});

/** Ticket #234: a name below the root is a definition, not an inline decoration. */
describe('PayloadPopover naming (#234)', () => {
  it('offers no Name field on a nested spec', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    render(
      <PayloadPopover
        store={store} catalog={CATALOG} path="$.rule.and[0]" spec="is-active" onClose={vi.fn()}
      />,
    );
    expect(screen.queryByLabelText('Name')).toBeNull();
    // A reader, not a disclosure: what is there is shown, and there is no link.
    expect(screen.getByLabelText('When true')).toBeTruthy();
  });

  it('renders the payload fields read-only below the root', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    render(
      <PayloadPopover
        store={store} catalog={CATALOG} path="$.rule.and[0]" spec="is-active" onClose={vi.fn()}
      />,
    );

    for (const label of ['When true', 'When false']) {
      const field = screen.getByLabelText<HTMLTextAreaElement>(label);
      expect(field.readOnly).toBe(true);
      expect(field.getAttribute('aria-readonly')).toBe('true');
    }
    expect(screen.getByText('Extract this node to a definition to decorate it.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull();
  });

  it('cannot write a nested node decoration — typing never reaches the store', async () => {
    const user = userEvent.setup();
    const store = new RuleEditorStore({
      rule: { and: [{ spec: 'is-active', name: 'activity' }, { spec: 'is-adult' }] },
    });
    render(
      <PayloadPopover
        store={store} catalog={CATALOG} path="$.rule.and[0]" spec="is-active" onClose={vi.fn()}
      />,
    );

    await user.type(screen.getByLabelText('When true'), 'yes');

    expect((store.getState().document.rule as { and: unknown[] }).and[0])
      .toEqual({ spec: 'is-active', name: 'activity' });
    expect(store.getState().canUndo).toBe(false);
  });

  it('shows an existing nested decoration, so what is there is still readable', () => {
    const store = new RuleEditorStore({
      rule: { and: [{ spec: 'is-active', whenTrue: 'yes' }, { spec: 'is-adult' }] },
    });
    render(
      <PayloadPopover
        store={store} catalog={CATALOG} path="$.rule.and[0]" spec="is-active" onClose={vi.fn()}
      />,
    );
    expect(screen.getByLabelText<HTMLTextAreaElement>('When true').value).toBe('yes');
  });

  it('keeps Save, and no hint, at the root', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    render(
      <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-active" onClose={vi.fn()} />,
    );
    expect(screen.getByRole('button', { name: 'Save' })).toBeTruthy();
    expect(screen.getByRole('button', { name: DESCRIBE })).toBeTruthy();
    expect(screen.queryByText('Extract this node to a definition to decorate it.')).toBeNull();
  });

  describe('payload disclosure at the root', () => {
    it('keeps the fields, and the JSON hint, behind a link until asked for', async () => {
      const user = userEvent.setup();
      const store = new RuleEditorStore({ rule: { spec: 'is-tiered' } });
      render(
        <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-tiered" onClose={vi.fn()} />,
      );
      expect(screen.queryByLabelText('When true')).toBeNull();
      expect(screen.queryByText(/JSON object/)).toBeNull();

      await openPayloadFields(user);
      expect(screen.getByLabelText('When true')).toBe(document.activeElement);
      expect(screen.getByLabelText('When false')).toBeTruthy();
      expect(screen.getByText(/JSON object · properties: tier/)).toBeTruthy();
      expect(screen.getByRole('button', { name: REMOVE })).toBeTruthy();
      expect(screen.queryByRole('button', { name: DESCRIBE })).toBeNull();
    });

    it('shows the fields at once when the node already carries a payload', () => {
      const store = new RuleEditorStore({ rule: { spec: 'is-active', whenTrue: 'yes' } });
      render(
        <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-active" onClose={vi.fn()} />,
      );
      expect(screen.getByLabelText<HTMLTextAreaElement>('When true').value).toBe('yes');
      expect(screen.queryByRole('button', { name: DESCRIBE })).toBeNull();
    });

    it('remove clears the draft, and Save then clears the store', async () => {
      const user = userEvent.setup();
      const store = new RuleEditorStore({ rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' } });
      render(
        <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-active" onClose={vi.fn()} />,
      );
      await user.click(screen.getByRole('button', { name: REMOVE }));
      expect(screen.queryByLabelText('When true')).toBeNull();
      // Nothing has reached the store yet — the card is a draft until Save.
      expect(store.getState().document.rule).toMatchObject({ whenTrue: 'yes' });

      await user.click(screen.getByRole('button', { name: 'Save' }));
      const rule = store.getState().document.rule as { whenTrue?: string; whenFalse?: string };
      expect(rule.whenTrue).toBeUndefined();
      expect(rule.whenFalse).toBeUndefined();
    });

    it('string placeholders name the root by its document, with the suffix', async () => {
      const user = userEvent.setup();
      const store = new RuleEditorStore({ name: 'activity', rule: { spec: 'is-active' } });
      render(
        <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-active" onClose={vi.fn()} />,
      );
      await openPayloadFields(user);
      expect(screen.getByLabelText('When true').getAttribute('placeholder')).toBe('activity == true');
      expect(screen.getByLabelText('When false').getAttribute('placeholder')).toBe('activity == false');
    });

    it('string placeholders prompt when nothing names the root', async () => {
      const user = userEvent.setup();
      renderPopover();
      await openPayloadFields(user);
      expect(screen.getByLabelText('When true').getAttribute('placeholder')).toBe('what it means when true');
    });

    it('offers no suffix placeholder for an object payload, which the text would misdescribe', async () => {
      const user = userEvent.setup();
      const store = new RuleEditorStore({ rule: { spec: 'is-tiered', name: 'tier' } });
      render(
        <PayloadPopover store={store} catalog={CATALOG} path="$.rule" spec="is-tiered" onClose={vi.fn()} />,
      );
      await openPayloadFields(user);
      expect(screen.getByLabelText('When true').getAttribute('placeholder')).toBeNull();
    });

    it('stays a plain reader below the root — fields shown, no link', () => {
      const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
      render(
        <PayloadPopover store={store} catalog={CATALOG} path="$.rule.and[0]" spec="is-active" onClose={vi.fn()} />,
      );
      expect(screen.getByLabelText<HTMLTextAreaElement>('When true').readOnly).toBe(true);
      // Nor a placeholder: a field that cannot be typed in must not invite typing.
      expect(screen.getByLabelText('When true').getAttribute('placeholder')).toBeNull();
      expect(screen.queryByRole('button', { name: DESCRIBE })).toBeNull();
      expect(screen.queryByRole('button', { name: REMOVE })).toBeNull();
    });
  });
});
