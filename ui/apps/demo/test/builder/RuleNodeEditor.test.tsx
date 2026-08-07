import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { EditorView } from '@codemirror/view';
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

describe('BuilderPane accordion (boolean)', () => {
  /** Via the actions menu, which offers Details on every node kind. */
  const openDetail = async (path: string) => {
    fireEvent.click(await screen.findByRole('button', { name: `actions for ${path}` }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Details' }));
  };

  it('starts with every detail panel closed', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await screen.findByRole('button', { name: 'details for $.rule' });
    expect(screen.queryByLabelText('name at $.rule')).toBeNull();
    expect(screen.queryByRole('button', { name: 'toggle NOT at $.rule' })).toBeNull();
  });

  it('starts with every subtree expanded', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    expect(await screen.findByRole('button', { name: 'details for $.rule.and[1]' })).toBeDefined();
  });

  it('holds nothing that can change the node it belongs to', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    const { container } = renderWith(store);
    await openDetail('$.rule');

    // A control here that re-kinded the node would re-render the panel into a different thing —
    // the reveal invalidating its own trigger. Structure is authored in the row instead.
    for (const name of ['toggle NOT at $.rule', 'wrap $.rule in AND', 'add operand to $.rule']) {
      expect(screen.queryByRole('button', { name })).toBeNull();
    }
    expect(container.querySelector('.node-detail .decoration')).not.toBeNull();
  });

  it('offers no remove, which would delete the panel you opened', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule.and[1]');
    expect(screen.queryByRole('button', { name: 'remove $.rule.and[1]' })).toBeNull();
  });

  it('edits whenTrue decoration into the document', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.change(screen.getByLabelText('whenTrue at $.rule'), { target: { value: 'yes' } });
    expect((store.getState().document.rule as { whenTrue?: string }).whenTrue).toBe('yes');
  });

  it('opening one panel closes the previously open one', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule.and[0]');
    expect(screen.getByLabelText('name at $.rule.and[0]')).toBeDefined();
    await openDetail('$.rule.and[1]');
    expect(screen.queryByLabelText('name at $.rule.and[0]')).toBeNull();
    expect(screen.getByLabelText('name at $.rule.and[1]')).toBeDefined();
  });

  it('collapsing a subtree hides its children but not its detail panel', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.click(screen.getByRole('button', { name: 'collapse $.rule' }));
    expect(screen.queryByRole('button', { name: 'details for $.rule.and[1]' })).toBeNull();
    expect(screen.getByLabelText('name at $.rule')).toBeDefined();
  });

  it('re-expanding a child does not collapse the root subtree', async () => {
    const store = new RuleEditorStore({ rule: { and: [ { or: [ { spec: 'is-active' }, { spec: 'is-adult' } ] }, { spec: 'is-active' } ] } });
    renderWith(store);
    await screen.findByRole('button', { name: 'details for $.rule.and[1]' });
    fireEvent.click(screen.getByRole('button', { name: 'collapse $.rule.and[0]' }));
    fireEvent.click(screen.getByRole('button', { name: 'expand $.rule.and[0]' }));
    expect(screen.getByRole('button', { name: 'details for $.rule.and[1]' })).toBeDefined();
  });

  it('no longer offers a spec select — the row is the way to change a spec', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openDetail('$.rule');
    expect(screen.queryByLabelText('spec at $.rule')).toBeNull();
  });

  it('no longer offers an add-quantifier button', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule');
    expect(screen.queryByRole('button', { name: 'add quantifier to $.rule' })).toBeNull();
  });

  it('opens a leaf\'s metadata from its caret', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    const caret = await screen.findByRole('button', { name: 'details for $.rule' });

    // A leaf has no subtree to fold, so its caret discloses metadata instead — a shortcut to
    // what the actions menu also offers, in the slot a bullet would otherwise waste.
    expect(caret.className).toContain('node-chev');
    expect(caret.textContent).toBe('▸');

    fireEvent.click(caret);
    expect(caret.textContent).toBe('▾');
    expect(caret.getAttribute('aria-expanded')).toBe('true');
    expect(screen.getByLabelText('name at $.rule')).toBeDefined();
  });

  it('keeps the caret structural on a parent, which reaches metadata through its menu', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await screen.findByRole('button', { name: 'collapse $.rule' });

    // A parent's caret is taken by its subtree, so it has no `details for` shortcut of its own.
    expect(screen.queryByRole('button', { name: 'details for $.rule' })).toBeNull();
    await openDetail('$.rule');
    expect(screen.getByLabelText('name at $.rule')).toBeDefined();
  });

  /**
   * The caret is two controls sharing one slot, and a commit can swap which one it is *between*
   * a press and the click it produces: pressing a leaf's detail caret blurs the open editor, and
   * committing a buffer that gives the row children makes that same caret the subtree's collapse
   * toggle. React reuses the DOM node, so the click landed on a control the gesture was never
   * aimed at and collapsed a subtree the same gesture had just created.
   *
   * The blur is fired directly because jsdom does not raise one from a mousedown elsewhere — the
   * same reason `NodeInsert.test.tsx` drives it explicitly. Everything else here is the real
   * gesture in order, and because the guard is decided at the press rather than by which element
   * survives to receive the click, jsdom can prove it outright.
   */
  it('does not collapse a subtree the same gesture has just brought into being', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);

    const caret = await screen.findByRole('button', { name: 'details for $.rule' });
    fireEvent.focus(screen.getByRole('button', { name: 'edit expression at $.rule' }));
    const view = EditorView.findFromDOM(container)!;
    act(() => view.dispatch({
      changes: { from: 0, to: view.state.doc.length, insert: 'is-active & (is-active | is-adult)' },
    }));

    // The press, on what is a leaf's detail toggle at this instant...
    fireEvent.mouseDown(caret);
    // ...which blurs the editor, committing a buffer that gives the row children.
    fireEvent.blur(container.querySelector('.cm-content')!);
    expect(store.getState().document.rule).toHaveProperty('and');

    // The release, landing on what has become the subtree's collapse toggle. `detail` is what
    // separates a pointer click from a keyboard one, and jsdom defaults it to 0 — left unset this
    // would take the keyboard path and prove nothing.
    fireEvent.click(caret, { detail: 1 });

    expect(screen.getByRole('button', { name: 'collapse $.rule' }).getAttribute('aria-expanded')).toBe('true');
    expect(screen.getByRole('button', { name: 'details for $.rule.and[0]' })).toBeDefined();
  });

  /** The guard must not cost the caret its ordinary job: a press and release on one kind acts. */
  it('still collapses on an ordinary press and release', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    const caret = await screen.findByRole('button', { name: 'collapse $.rule' });

    fireEvent.mouseDown(caret);
    fireEvent.click(caret, { detail: 1 });

    expect(screen.getByRole('button', { name: 'expand $.rule' })).toBeDefined();
    expect(screen.queryByRole('button', { name: 'details for $.rule.and[0]' })).toBeNull();
  });

  /**
   * Enter and Space produce a click with no press before it and no click count on it. The guard
   * identifies that positively rather than treating "no press recorded" as proof of a keyboard,
   * so this pins the branch that distinction buys — a caret reachable by keyboard alone.
   */
  it('acts on a keyboard activation, which has no press to match', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);

    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));

    expect(screen.getByRole('button', { name: 'expand $.rule' })).toBeDefined();
  });

});
