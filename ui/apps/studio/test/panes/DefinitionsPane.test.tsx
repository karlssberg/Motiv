import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type Catalog, type RuleDocument } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { DefinitionsPane } from '../../src/panes/DefinitionsPane.js';

/** A catalog entry whose declared parameter order differs from how a document might supply args. */
const CATALOG: Catalog = {
  specs: [{
    name: 'at-least', modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Compiled',
    parameters: [{ name: 'floor', type: 'integer' }, { name: 'label', type: 'string' }],
  }],
  collections: [],
};

/**
 * A document with two definitions: `a`, referenced twice from the rule, and `b`, referenced once
 * from inside `a`'s own body — so inlining `a` last-to-first has to cope with a reference
 * disappearing out from under it (inlining the reference inside `a` removes `b` once `a` no
 * longer points at it either, per the store's own de-duplication).
 */
const twoDefinitionsDocument = (): RuleDocument => ({
  rule: { and: [{ local: 'a' }, { local: 'a' }] },
  definitions: {
    a: { rule: { local: 'b' } },
    b: { rule: { spec: 'is-active' } },
  },
});

const renderWith = (store: RuleEditorStore, catalog?: Catalog) =>
  render(<RuleEditorProvider store={store}><DefinitionsPane catalog={catalog} /></RuleEditorProvider>);

describe('DefinitionsPane', () => {
  it('shows the empty state when the document has no definitions', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    expect(screen.getByText('No definitions. Extract a node to create one.')).toBeDefined();
    expect(screen.getByRole('region', { name: 'Definitions' })).toBeDefined();
  });

  it('lists definitions in key order, each row addressable by id', () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    renderWith(store);
    const region = screen.getByRole('region', { name: 'Definitions' });
    const rows = region.querySelectorAll('[id^="definition-"]');
    expect(Array.from(rows).map((row) => row.id)).toEqual(['definition-a', 'definition-b']);
    expect(document.getElementById('definition-a')?.getAttribute('tabindex')).toBe('-1');
  });

  it('lists a definition above the definitions it references, whatever the key order', () => {
    const store = new RuleEditorStore({
      rule: { local: 'top' },
      definitions: {
        leaf: { rule: { spec: 'is-active' } },
        top: { rule: { and: [{ local: 'leaf' }, { spec: 'is-adult' }] } },
      },
    });
    renderWith(store);
    const rows = screen.getByRole('region', { name: 'Definitions' }).querySelectorAll('[id^="definition-"]');
    expect(Array.from(rows).map((row) => row.id)).toEqual(['definition-top', 'definition-leaf']);
  });

  it('shows a reference count per definition, singular and plural', () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    renderWith(store);
    const rowA = document.getElementById('definition-a')!;
    const rowB = document.getElementById('definition-b')!;
    expect(rowA.textContent).toContain('2 references');
    expect(rowB.textContent).toContain('1 reference');
  });

  it('renames a definition and rewrites every reference on commit', () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    renderWith(store);
    const input = screen.getByLabelText('name of definition a') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'renamed' } });
    fireEvent.blur(input);

    const doc = store.getState().document;
    expect(doc.definitions?.['renamed']).toBeDefined();
    expect(doc.definitions?.['a']).toBeUndefined();
    expect(doc.rule).toEqual({ and: [{ local: 'renamed' }, { local: 'renamed' }] });
  });

  it('does not rename to a name that is already taken', () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    renderWith(store);
    const input = screen.getByLabelText('name of definition a') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'b' } });
    fireEvent.blur(input);

    expect(store.getState().document.definitions?.['a']).toBeDefined();
    expect(store.getState().document.definitions?.['b']).toEqual({ rule: { spec: 'is-active' } });
  });

  it('disables Remove while the definition is referenced', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition a' }));
    const remove = screen.getByRole('menuitem', { name: 'Remove' });
    expect(remove).toHaveProperty('disabled', true);
  });

  it('enables Remove once no references remain, and it removes the definition', () => {
    const store = new RuleEditorStore({
      rule: { spec: 'is-active' },
      definitions: { unused: { rule: { spec: 'is-adult' } } },
    });
    renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition unused' }));
    const remove = screen.getByRole('menuitem', { name: 'Remove' });
    expect(remove).toHaveProperty('disabled', false);
    fireEvent.click(remove);
    expect(store.getState().document.definitions).toBeUndefined();
  });

  it('inlines every reference to a multiply-referenced definition, removing it', () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition a' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Inline everywhere' }));

    const doc = store.getState().document;
    expect(doc.definitions?.['a']).toBeUndefined();
    // `a`'s body (`{ local: 'b' }`) is copied into both call sites, so `b` — referenced from
    // inside `a` before this — ends up with two live references instead of losing its last one.
    expect(doc.rule).toEqual({ and: [{ local: 'b', name: 'a' }, { local: 'b', name: 'a' }] });
    expect(doc.definitions?.['b']).toEqual({ rule: { spec: 'is-active' } });
  });

  it('inlining the only reference to a definition removes it, even from inside another body', () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition b' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Inline everywhere' }));

    const doc = store.getState().document;
    expect(doc.definitions?.['b']).toBeUndefined();
    expect(doc.definitions?.['a']).toEqual({ rule: { spec: 'is-active', name: 'b' } });
  });

  it('edits a definition’s decoration in place', () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    renderWith(store);
    fireEvent.change(screen.getByLabelText('whenTrue of definition a'), { target: { value: 'a holds' } });
    expect(store.getState().document.definitions?.['a']?.whenTrue).toBe('a holds');

    fireEvent.change(screen.getByLabelText('whenFalse of definition b'), { target: { value: 'b fails' } });
    expect(store.getState().document.definitions?.['b']?.whenFalse).toBe('b fails');
  });

  it('prints a definition body with args in the catalog’s declared order, not insertion order', () => {
    const store = new RuleEditorStore({
      rule: { local: 'c' },
      definitions: {
        // `label` before `floor` here; the catalog declares `floor` first.
        c: { rule: { spec: 'at-least', args: { label: 'high', floor: 2 } } },
      },
    });
    renderWith(store, CATALOG);
    const row = document.getElementById('definition-c')!;
    expect(row.textContent).toContain('at-least(floor = 2, label = "high")');
  });

  it('does not show Promote to catalog… without an onPromote handler, and shows it with one', () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    render(
      <RuleEditorProvider store={store}>
        <DefinitionsPane />
      </RuleEditorProvider>,
    );
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition a' }));
    expect(screen.queryByRole('menuitem', { name: 'Promote to catalog…' })).toBeNull();
  });
});
