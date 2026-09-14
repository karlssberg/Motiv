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

const DESCRIBE = 'Describe what it means for this to be true or false…';
const REMOVE = 'Remove these descriptions';

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
    expect(screen.queryByLabelText('whenTrue at $.rule')).toBeNull();
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
    fireEvent.click(screen.getByRole('button', { name: DESCRIBE }));
    fireEvent.change(screen.getByLabelText('whenTrue at $.rule'), { target: { value: 'yes' } });
    expect((store.getState().document.rule as { whenTrue?: string }).whenTrue).toBe('yes');
  });

  it('opening one panel closes the previously open one', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    const expanded = (path: string) =>
      screen.getByRole('button', { name: `details for ${path}` }).getAttribute('aria-expanded');

    await openDetail('$.rule.and[0]');
    expect(expanded('$.rule.and[0]')).toBe('true');
    await openDetail('$.rule.and[1]');
    expect(expanded('$.rule.and[0]')).toBe('false');
    expect(expanded('$.rule.and[1]')).toBe('true');
  });

  it('collapsing a subtree hides its children but not its detail panel', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.click(screen.getByRole('button', { name: 'collapse $.rule' }));
    expect(screen.queryByRole('button', { name: 'details for $.rule.and[1]' })).toBeNull();
    expect(screen.getByLabelText('whenTrue at $.rule')).toBeDefined();
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
    // Drawn as an SVG chevron, not a unicode triangle: the glyph set renders the same on every
    // platform, and the button's state is the attribute, not the character.
    expect(caret.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
    expect(caret.textContent).toBe('');
    expect(caret.getAttribute('aria-expanded')).toBe('false');

    fireEvent.click(caret);
    expect(caret.getAttribute('aria-expanded')).toBe('true');
    expect(screen.getByLabelText('whenTrue at $.rule')).toBeDefined();
  });

  it('keeps the caret structural on a parent, which reaches metadata through its menu', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await screen.findByRole('button', { name: 'collapse $.rule' });

    // A parent's caret is taken by its subtree, so it has no `details for` shortcut of its own.
    expect(screen.queryByRole('button', { name: 'details for $.rule' })).toBeNull();
    await openDetail('$.rule');
    expect(screen.getByLabelText('whenTrue at $.rule')).toBeDefined();
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

/**
 * Ticket #234: a name below the root is no longer authored in place. A nested node that still
 * carries one is shown the migration affordance instead, and the definitions it becomes are
 * referenced by `let` rows that can be read, inlined, and jumped to.
 */
describe('BuilderPane scoped propositions (#234)', () => {
  const openDetail = async (path: string) => {
    fireEvent.click(await screen.findByRole('button', { name: `actions for ${path}` }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Details' }));
  };

  const NESTED_NAME = {
    rule: { and: [{ spec: 'is-active', name: 'activity' }, { spec: 'is-adult' }] },
  };
  const WITH_LOCAL = {
    definitions: { activity: { rule: { spec: 'is-active' } } },
    rule: { and: [{ local: 'activity' }, { spec: 'is-adult' }] },
  };

  it('offers no name field below the root', async () => {
    renderWith(new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } }));
    await openDetail('$.rule.and[0]');
    expect(screen.queryByLabelText('name at $.rule.and[0]')).toBeNull();
  });

  it('offers no name field on the root either — the DSL has no inline name', async () => {
    renderWith(new RuleEditorStore({ rule: { spec: 'is-active' } }));
    await openDetail('$.rule');
    expect(screen.getByLabelText('whenTrue at $.rule')).toBeDefined();
    expect(screen.queryByLabelText('name at $.rule')).toBeNull();
  });

  it('shows the migration notice for a nested node that still carries a name', async () => {
    const { container } = renderWith(new RuleEditorStore(NESTED_NAME));
    await openDetail('$.rule.and[0]');
    const notice = container.querySelector('.decoration-notice');
    expect(notice).not.toBeNull();
    expect(notice!.textContent).toContain('"activity"');
    expect(notice!.textContent).not.toContain('as "');
  });

  it('shows no notice for a nested node with nothing to migrate', async () => {
    const { container } = renderWith(
      new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } }),
    );
    await openDetail('$.rule.and[0]');
    expect(container.querySelector('.decoration-notice')).toBeNull();
  });

  it('extracts a named nested node into a definition carrying that name', async () => {
    const store = new RuleEditorStore(NESTED_NAME);
    renderWith(store);
    await openDetail('$.rule.and[0]');
    fireEvent.click(screen.getByRole('button', { name: 'extract $.rule.and[0]' }));

    const document = store.getState().document;
    expect((document.rule as { and: unknown[] }).and[0]).toEqual({ local: 'activity' });
    expect(document.definitions?.activity).toEqual({ rule: { spec: 'is-active' } });
  });

  it('refuses to extract onto a name already taken', async () => {
    renderWith(new RuleEditorStore({
      definitions: { activity: { rule: { spec: 'is-adult' } } },
      rule: { and: [{ spec: 'is-active', name: 'activity' }, { local: 'activity' }] },
    }));
    await openDetail('$.rule.and[0]');
    const extract = screen.getByRole('button', { name: 'extract $.rule.and[0]' });
    expect(extract.hasAttribute('disabled')).toBe(true);
    expect(extract.getAttribute('title')).toContain('activity');
  });

  it('renders a local reference as its name, coloured as a local, with no keyword', async () => {
    const { container } = renderWith(new RuleEditorStore(WITH_LOCAL));
    await screen.findByRole('button', { name: 'details for $.rule.and[0]' });
    const badge = container.querySelector('.node-badge-local');
    expect(badge?.textContent).toBe('activity');
    expect([...container.querySelectorAll('.node-badge')].map((b) => b.textContent)).not.toContain('let');
  });

  it('shows the definition body read-only in a local row\'s panel', async () => {
    const { container } = renderWith(new RuleEditorStore(WITH_LOCAL));
    await openDetail('$.rule.and[0]');
    const detail = container.querySelector('.node-local-detail');
    expect(detail).not.toBeNull();
    expect(detail!.textContent).toContain('is-active');
    // Read-only: there is no editor to focus, and no decoration fields to fill in.
    expect(screen.queryByRole('button', { name: 'edit expression at $.rule.and[0]' })).toBeNull();
    expect(screen.queryByLabelText('whenTrue at $.rule.and[0]')).toBeNull();
  });

  it('inlines a local back into the tree as its bare body — no name is written', async () => {
    const store = new RuleEditorStore(WITH_LOCAL);
    renderWith(store);
    await openDetail('$.rule.and[0]');
    fireEvent.click(screen.getByRole('button', { name: 'inline $.rule.and[0]' }));

    const document = store.getState().document;
    expect((document.rule as { and: unknown[] }).and[0]).toEqual({ spec: 'is-active' });
    expect(document.definitions?.activity).toBeUndefined();
  });

  it('offers Go to definition, which is inert while no definitions panel is mounted', async () => {
    renderWith(new RuleEditorStore(WITH_LOCAL));
    await openDetail('$.rule.and[0]');
    expect(() => fireEvent.click(
      screen.getByRole('button', { name: 'go to definition $.rule.and[0]' }),
    )).not.toThrow();
  });
});

describe('whenTrue / whenFalse disclosure', () => {
  const openRoot = async () => {
    fireEvent.click(await screen.findByRole('button', { name: 'actions for $.rule' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Details' }));
  };

  it('keeps the fields behind a link until asked for, then focuses the first', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openRoot();
    expect(screen.queryByLabelText('whenTrue at $.rule')).toBeNull();
    expect(screen.queryByLabelText('whenFalse at $.rule')).toBeNull();
    expect(screen.queryByRole('button', { name: REMOVE })).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: DESCRIBE }));
    expect(screen.getByLabelText('whenTrue at $.rule')).toBe(document.activeElement);
    expect(screen.getByLabelText('whenFalse at $.rule')).toBeDefined();
    expect(screen.queryByRole('button', { name: DESCRIBE })).toBeNull();
    expect(screen.getByRole('button', { name: REMOVE })).toBeDefined();
  });

  it('shows the fields at once when the node already carries a payload', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' } });
    renderWith(store);
    await openRoot();
    expect((screen.getByLabelText('whenTrue at $.rule') as HTMLInputElement).value).toBe('yes');
    expect((screen.getByLabelText('whenFalse at $.rule') as HTMLInputElement).value).toBe('no');
    expect(screen.queryByRole('button', { name: DESCRIBE })).toBeNull();
    // Opening a panel that already holds text must not steal focus from wherever the user was.
    expect(document.activeElement).not.toBe(screen.getByLabelText('whenTrue at $.rule'));
  });

  it('stays open once opened, even with both fields cleared again', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openRoot();
    fireEvent.click(screen.getByRole('button', { name: DESCRIBE }));
    fireEvent.change(screen.getByLabelText('whenTrue at $.rule'), { target: { value: 'yes' } });
    fireEvent.change(screen.getByLabelText('whenTrue at $.rule'), { target: { value: '' } });
    expect(screen.getByLabelText('whenTrue at $.rule')).toBeDefined();
  });

  it('placeholders show what Motiv will say for a named proposition — the name with its suffix', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active', name: 'is active' } });
    renderWith(store);
    await openRoot();
    fireEvent.click(screen.getByRole('button', { name: DESCRIBE }));
    expect(screen.getByLabelText('whenTrue at $.rule').getAttribute('placeholder')).toBe('is active == true');
    expect(screen.getByLabelText('whenFalse at $.rule').getAttribute('placeholder')).toBe('is active == false');
  });

  it('placeholders follow the name as it is typed', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openRoot();
    fireEvent.click(screen.getByRole('button', { name: DESCRIBE }));
    // Unnamed: the suffix rule has no name to apply to, so the placeholder is a prompt, not a guess.
    expect(screen.getByLabelText('whenTrue at $.rule').getAttribute('placeholder')).toBe('what it means when true');
    fireEvent.change(screen.getByLabelText('name at $.rule'), { target: { value: 'can checkout' } });
    expect(screen.getByLabelText('whenFalse at $.rule').getAttribute('placeholder')).toBe('can checkout == false');
  });

  it('remove clears both payloads and puts the link back', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' } });
    renderWith(store);
    await openRoot();
    fireEvent.click(screen.getByRole('button', { name: REMOVE }));
    const rule = store.getState().document.rule as { whenTrue?: string; whenFalse?: string };
    expect(rule.whenTrue).toBeUndefined();
    expect(rule.whenFalse).toBeUndefined();
    expect(screen.queryByLabelText('whenTrue at $.rule')).toBeNull();
    expect(screen.getByRole('button', { name: DESCRIBE })).toBeDefined();
  });
});
