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

const AND = { rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } };

/** Matches the picker whatever operator it currently names, so a test need not spell the name out. */
const PICKER = /^operator at/;
const findPicker = () => screen.findByRole('combobox', { name: PICKER });

const openPicker = async () => {
  fireEvent.click(await findPicker());
};

describe('operator picker', () => {
  it('wears the operator now in force, and stays a badge until asked', async () => {
    renderWith(new RuleEditorStore(AND));
    expect((await findPicker()).textContent).toBe('AND');
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('names the operator now in force, so it is not only visible', async () => {
    renderWith(new RuleEditorStore(AND));
    // The badge's text is its value, but a name given to the control replaces that text for
    // assistive tech — so the value has to be in the name too, or it is lost when collapsed.
    expect(await screen.findByRole('combobox', { name: 'operator at $.rule, AND' })).toBeDefined();
  });

  it('offers every binary operator, marking the node\'s own', async () => {
    renderWith(new RuleEditorStore(AND));
    await openPicker();
    const options = screen.getAllByRole('option');
    expect(options.map((o) => o.textContent)).toEqual(['AND', 'OR', 'XOR', 'AndAlso', 'OrElse']);
    expect(options.filter((o) => o.getAttribute('aria-selected') === 'true').map((o) => o.textContent))
      .toEqual(['AND']);
  });

  it('ties the badge to the list it opens', async () => {
    renderWith(new RuleEditorStore(AND));
    await openPicker();
    const listboxId = screen.getByRole('listbox').getAttribute('id');
    expect(listboxId).toBeTruthy();
    expect((await findPicker()).getAttribute('aria-controls')).toBe(listboxId);
  });

  it('opens onto the operator in force, and walks the list from there', async () => {
    renderWith(new RuleEditorStore(AND));
    await openPicker();
    const [and, or, , , orElse] = screen.getAllByRole('option');
    expect(document.activeElement).toBe(and);

    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'ArrowDown' });
    expect(document.activeElement).toBe(or);
    // The ends are stops, not wrap-arounds: End reaches the last, and Up from the first stays put.
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'End' });
    expect(document.activeElement).toBe(orElse);
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Home' });
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'ArrowUp' });
    expect(document.activeElement).toBe(and);
  });

  it('rewrites the node under the chosen operator, keeping its operands in order', async () => {
    const store = new RuleEditorStore(AND);
    renderWith(store);
    await openPicker();
    fireEvent.click(screen.getByRole('option', { name: 'OrElse' }));

    const rule = store.getState().document.rule as unknown as Record<string, unknown>;
    expect(rule).toEqual({ orElse: [{ spec: 'is-active' }, { spec: 'is-adult' }] });
    expect('and' in rule).toBe(false);
    // Choosing closes it: the picker is transient, and its trigger has just been relabelled.
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('preserves decoration across the change', async () => {
    const store = new RuleEditorStore({
      rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }], name: 'pair', whenTrue: 'yes' },
    });
    renderWith(store);
    await openPicker();
    fireEvent.click(screen.getByRole('option', { name: 'XOR' }));

    expect(store.getState().document.rule).toEqual({
      xor: [{ spec: 'is-active' }, { spec: 'is-adult' }], name: 'pair', whenTrue: 'yes',
    });
  });

  it('restates the badge and the gloss for the operator now in force', async () => {
    renderWith(new RuleEditorStore(AND));
    expect(await screen.findByText('all must hold')).toBeDefined();
    await openPicker();
    fireEvent.click(screen.getByRole('option', { name: 'OR' }));

    expect((await findPicker()).textContent).toBe('OR');
    expect(screen.getByText('any may hold')).toBeDefined();
    expect(screen.queryByText('all must hold')).toBeNull();
  });

  it('is offered only by binary nodes', async () => {
    renderWith(new RuleEditorStore({
      rule: { not: { asAllSatisfied: { spec: 'is-active' }, path: 'orders' } },
    }));
    await screen.findByRole('button', { name: 'collapse $.rule' });
    expect(screen.queryByRole('combobox', { name: PICKER })).toBeNull();
  });

  it('gives way to the text once the subtree is collapsed', async () => {
    renderWith(new RuleEditorStore(AND));
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    expect(screen.queryByRole('combobox', { name: PICKER })).toBeNull();
    expect(screen.getByRole('button', { name: 'edit expression at $.rule' }).textContent)
      .toBe('is-active & is-adult');
  });

  it('closes on Escape and hands focus back to its trigger', async () => {
    renderWith(new RuleEditorStore(AND));
    await openPicker();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(document.activeElement).toBe(await findPicker());
  });

  it('closes when a click lands outside it', async () => {
    renderWith(new RuleEditorStore(AND));
    await openPicker();
    fireEvent.mouseDown(document.body);
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('displaces the actions menu, and is displaced by it', async () => {
    renderWith(new RuleEditorStore(AND));
    fireEvent.click(await screen.findByRole('button', { name: 'actions for $.rule' }));
    await openPicker();
    expect(screen.queryByRole('menu')).toBeNull();
    expect(screen.getByRole('listbox')).toBeDefined();

    fireEvent.click(screen.getByRole('button', { name: 'actions for $.rule' }));
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(screen.getByRole('menu')).toBeDefined();
  });
});

/**
 * The IDREF the palette already learned to drop (`CommandPalette.tsx`): a control that names a
 * relationship to an element which is not in the document has an *invalid* relationship, not an
 * inert one — and a picker spends nearly all of its life closed, so this was the default state of
 * every operator badge and rule-name picker in the app.
 */
describe('operator picker, closed', () => {
  it('claims no listbox while it renders none', async () => {
    renderWith(new RuleEditorStore(AND));
    const trigger = await findPicker();

    expect(trigger.getAttribute('aria-controls')).toBeNull();
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
  });

  it('claims the listbox it does render, once open', async () => {
    renderWith(new RuleEditorStore(AND));
    await openPicker();
    const trigger = await findPicker();

    const controls = trigger.getAttribute('aria-controls');
    expect(controls).not.toBeNull();
    expect(document.getElementById(controls!)).toBe(screen.getByRole('listbox'));
  });
});
