import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';
import { replaceBuffer } from '../support/codemirror.js';

const catalog = { specs: [], collections: [] };
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;

function renderBuilder(rule: unknown) {
  const store = new RuleEditorStore({ rule } as never);
  const view = render(
    <RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>,
  );
  return { store, ...view };
}

const slot = (container: HTMLElement) => container.querySelector('.node-row-pending .cm-content');

/**
 * Opens the slot after `path` and types `text` into it, leaving it *uncommitted* and returning its
 * editable element — so each test dispatches the dismissal gesture it is about (Enter, blur) itself.
 */
const typeIntoSlotAfter = async (container: HTMLElement, path: string, text: string) => {
  fireEvent.click(await screen.findByRole('button', { name: `insert after ${path}` }));
  replaceBuffer(container.querySelector('.node-row-pending') as HTMLElement, text);
  return slot(container)!;
};

/** Opens the slot after `path` and types `text` into it, committing with Enter. */
const insertAfter = async (container: HTMLElement, path: string, text: string) => {
  fireEvent.keyDown(await typeIntoSlotAfter(container, path, text), { key: 'Enter' });
};

describe('row + insertion', () => {
  it('inserts a sibling immediately after an operand row', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    await insertAfter(container, '$.rule.and[0]', 'c');

    expect(store.getState().document.rule)
      .toEqual({ and: [{ spec: 'a' }, { spec: 'c' }, { spec: 'b' }] });
  });

  it('wraps a lone root spec in and', async () => {
    const { store, container } = renderBuilder({ spec: 'a' });

    await insertAfter(container, '$.rule', 'b');

    expect(store.getState().document.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }] });
  });

  it('appends to the root operator rather than nesting it, since the wrap normalizes away', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    await insertAfter(container, '$.rule', 'c');

    expect(store.getState().document.rule)
      .toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });
  });

  it('leaves the document untouched when the slot is cancelled', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });
    const before = store.getState().document;

    fireEvent.click(await screen.findByRole('button', { name: 'insert after $.rule.and[0]' }));
    fireEvent.keyDown(slot(container)!, { key: 'Escape' });

    expect(store.getState().document).toEqual(before);
    expect(store.getState().canUndo).toBe(false);
    expect(slot(container)).toBeNull();
  });

  it('opens at most one slot at a time', async () => {
    const { container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    fireEvent.click(await screen.findByRole('button', { name: 'insert after $.rule.and[0]' }));
    fireEvent.click(await screen.findByRole('button', { name: 'insert after $.rule.and[1]' }));

    expect(container.querySelectorAll('.node-row-pending')).toHaveLength(1);
  });

  it('commits a slot abandoned by clicking elsewhere, since a real blur commits a parseable buffer', async () => {
    // `fireEvent.click` in jsdom moves no focus and dispatches no `blur` — clicking another row's
    // `+` here leaves the pending editor untouched as far as jsdom is concerned. In a browser,
    // mousedown on the new target blurs the still-focused editor first, and that blur commits a
    // parseable buffer (see the design spec's edge case: "the edit commits first"). So this test
    // drives the blur directly with `fireEvent.blur` to exercise the real contract, rather than
    // relying on a click to produce it.
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    fireEvent.blur(await typeIntoSlotAfter(container, '$.rule.and[0]', 'c'));

    expect(store.getState().document.rule)
      .toEqual({ and: [{ spec: 'a' }, { spec: 'c' }, { spec: 'b' }] });
    expect(container.querySelectorAll('.node-row-pending')).toHaveLength(0);
  });

  it('discards an abandoned slot whose buffer does not parse, rather than leaving it stuck', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });
    const before = store.getState().document;

    fireEvent.blur(await typeIntoSlotAfter(container, '$.rule.and[0]', 'abandoned &'));

    expect(store.getState().document).toEqual(before);
    expect(store.getState().canUndo).toBe(false);
    expect(container.querySelectorAll('.node-row-pending')).toHaveLength(0);
  });
});

/** Opens the first-operand slot on `path` via its menu and commits `text`. */
const insertFirst = async (container: HTMLElement, path: string, text: string) => {
  fireEvent.click(await screen.findByRole('button', { name: `actions for ${path}` }));
  fireEvent.click(screen.getByRole('menuitem', { name: 'Insert first operand' }));
  const pending = container.querySelector('.node-row-pending') as HTMLElement;
  replaceBuffer(pending, text);
  fireEvent.keyDown(pending.querySelector('.cm-content')!, { key: 'Enter' });
};

describe('insert first operand', () => {
  it('inserts before the first child of an operator row', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    await insertFirst(container, '$.rule', 'z');

    expect(store.getState().document.rule)
      .toEqual({ and: [{ spec: 'z' }, { spec: 'a' }, { spec: 'b' }] });
  });

  it('reaches the slot before a nested group first child', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { or: [{ spec: 'b' }, { spec: 'c' }] }] });

    await insertFirst(container, '$.rule.and[1]', 'z');

    expect(store.getState().document.rule).toEqual({
      and: [{ spec: 'a' }, { or: [{ spec: 'z' }, { spec: 'b' }, { spec: 'c' }] }],
    });
  });

  it('is not offered on a leaf row, which has no operand list', async () => {
    renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    fireEvent.click(await screen.findByRole('button', { name: 'actions for $.rule.and[0]' }));

    // Assert the menu is genuinely open before asserting what it lacks: without this,
    // a menu that failed to open at all would satisfy the absence check too.
    expect(screen.getByRole('menuitem', { name: 'Details' })).toBeDefined();
    expect(screen.queryByRole('menuitem', { name: 'Insert first operand' })).toBeNull();
  });

  it('offers the first-operand slot on a collapsed parent, where .node-kids is not mounted', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    // Prove the row actually collapsed before relying on it. The pending row renders the same
    // `.node-row-pending` markup at both sites, so without this the test would pass unchanged
    // against an expanded row — exercising the very branch it exists to avoid.
    expect(await screen.findByRole('button', { name: 'expand $.rule' })).toBeDefined();
    fireEvent.click(await screen.findByRole('button', { name: 'actions for $.rule' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Insert first operand' }));

    const pending = container.querySelector('.node-row-pending') as HTMLElement;
    expect(pending).not.toBeNull();
    replaceBuffer(pending, 'z');
    fireEvent.keyDown(pending.querySelector('.cm-content')!, { key: 'Enter' });

    expect(store.getState().document.rule)
      .toEqual({ and: [{ spec: 'z' }, { spec: 'a' }, { spec: 'b' }] });
  });
});
