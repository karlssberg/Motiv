import { describe, it, expect, vi } from 'vitest';
import { act, render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type Catalog, type RuleDocument, type RulesApiClient } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { BuilderBody } from '../../src/panes/BuilderPane.js';

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

const EMPTY: Catalog = { specs: [], collections: [] };

function client(catalog: Catalog): RulesApiClient {
  return {
    getCatalog: vi.fn().mockResolvedValue(catalog),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    evaluate: vi.fn(),
  } as unknown as RulesApiClient;
}

/**
 * The pane is one half of the builder — it lives inside the builder's tree context, since every
 * definition is a tree of the same rows the rule is — so it is rendered the way the builder
 * mounts it rather than on its own.
 */
const renderWith = async (
  store: RuleEditorStore, catalog: Catalog = EMPTY, onPromote?: (name: string) => void,
) => {
  const result = render(
    <RuleEditorProvider store={store}>
      <BuilderBody client={client(catalog)} onPromote={onPromote} />
    </RuleEditorProvider>,
  );
  await act(async () => {});
  return result;
};

describe('DefinitionsPane', () => {
  it('shows the empty state when the document has no definitions', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    await renderWith(store);
    expect(screen.getByText('No definitions. Extract a node to create one.')).toBeDefined();
    expect(screen.getByRole('region', { name: 'Definitions' })).toBeDefined();
  });

  it('lists definitions in key order, each row addressable by id', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    await renderWith(store);
    const region = screen.getByRole('region', { name: 'Definitions' });
    const rows = region.querySelectorAll('[id^="definition-"]');
    expect(Array.from(rows).map((row) => row.id)).toEqual(['definition-a', 'definition-b']);
    expect(document.getElementById('definition-a')?.getAttribute('tabindex')).toBe('-1');
  });

  it('lists a definition above the definitions it references, whatever the key order', async () => {
    const store = new RuleEditorStore({
      rule: { local: 'top' },
      definitions: {
        leaf: { rule: { spec: 'is-active' } },
        top: { rule: { and: [{ local: 'leaf' }, { spec: 'is-adult' }] } },
      },
    });
    await renderWith(store);
    const rows = screen.getByRole('region', { name: 'Definitions' }).querySelectorAll('[id^="definition-"]');
    expect(Array.from(rows).map((row) => row.id)).toEqual(['definition-top', 'definition-leaf']);
  });

  it('shows a reference count per definition, singular and plural', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    await renderWith(store);
    const rowA = document.getElementById('definition-a')!;
    const rowB = document.getElementById('definition-b')!;
    expect(rowA.textContent).toContain('2 references');
    expect(rowB.textContent).toContain('1 reference');
  });

  it('renames a definition and rewrites every reference on commit', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    await renderWith(store);
    const input = screen.getByLabelText('name of definition a') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'renamed' } });
    fireEvent.blur(input);

    const doc = store.getState().document;
    expect(doc.definitions?.['renamed']).toBeDefined();
    expect(doc.definitions?.['a']).toBeUndefined();
    expect(doc.rule).toEqual({ and: [{ local: 'renamed' }, { local: 'renamed' }] });
  });

  it('does not rename to a name that is already taken', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    await renderWith(store);
    const input = screen.getByLabelText('name of definition a') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'b' } });
    fireEvent.blur(input);

    expect(store.getState().document.definitions?.['a']).toBeDefined();
    expect(store.getState().document.definitions?.['b']).toEqual({ rule: { spec: 'is-active' } });
  });

  it('disables Remove while the definition is referenced', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    await renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition a' }));
    const remove = screen.getByRole('menuitem', { name: 'Remove' });
    expect(remove).toHaveProperty('disabled', true);
  });

  it('enables Remove once no references remain, and it removes the definition', async () => {
    const store = new RuleEditorStore({
      rule: { spec: 'is-active' },
      definitions: { unused: { rule: { spec: 'is-adult' } } },
    });
    await renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition unused' }));
    const remove = screen.getByRole('menuitem', { name: 'Remove' });
    expect(remove).toHaveProperty('disabled', false);
    fireEvent.click(remove);
    expect(store.getState().document.definitions).toBeUndefined();
  });

  it('inlines every reference to a multiply-referenced definition, removing it', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    await renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition a' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Inline everywhere' }));

    const doc = store.getState().document;
    expect(doc.definitions?.['a']).toBeUndefined();
    // `a`'s body (`{ local: 'b' }`) is copied into both call sites, so `b` — referenced from
    // inside `a` before this — ends up with two live references instead of losing its last one.
    expect(doc.rule).toEqual({ and: [{ local: 'b' }, { local: 'b' }] });
    expect(doc.definitions?.['b']).toEqual({ rule: { spec: 'is-active' } });
  });

  it('inlining the only reference to a definition removes it, even from inside another body', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    await renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition b' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Inline everywhere' }));

    const doc = store.getState().document;
    expect(doc.definitions?.['b']).toBeUndefined();
    expect(doc.definitions?.['a']).toEqual({ rule: { spec: 'is-active' } });
  });

  it('edits a definition’s decoration from its body root’s detail panel, as the rule does', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    await renderWith(store);
    expect(screen.queryByLabelText('whenTrue of definition a')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'details for $.definitions.a.rule' }));
    fireEvent.change(screen.getByLabelText('whenTrue of definition a'), { target: { value: 'a holds' } });
    expect(store.getState().document.definitions?.['a']?.whenTrue).toBe('a holds');
    // No Name field: the name is the row's own input, and no Extract: the body already is one.
    expect(screen.queryByLabelText('name at $.definitions.a.rule')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'details for $.definitions.b.rule' }));
    fireEvent.change(screen.getByLabelText('whenFalse of definition b'), { target: { value: 'b fails' } });
    expect(store.getState().document.definitions?.['b']?.whenFalse).toBe('b fails');
  });

  it('renders each definition as the same tree the rule is, under its own strip', async () => {
    const store = new RuleEditorStore({
      rule: { local: 'pair' },
      definitions: { pair: { rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } } },
    });
    await renderWith(store);
    const row = document.getElementById('definition-pair')!;
    expect(screen.getByLabelText('definition pair expression').textContent).toBe('is-active & is-adult');
    expect(row.querySelector('[role="group"][aria-label="definition pair composition"]')).not.toBeNull();
    // The body's rows carry the same controls as the rule's rows, addressed by their own paths.
    expect(screen.getByRole('button', { name: 'collapse $.definitions.pair.rule' })).toBeDefined();
    expect(screen.getByRole('button', { name: 'select $.definitions.pair.rule.and[0]' })).toBeDefined();
    expect(screen.getByRole('button', { name: 'insert after $.definitions.pair.rule.and[1]' })).toBeDefined();
    expect(screen.getByRole('button', { name: 'actions for $.definitions.pair.rule.and[0]' })).toBeDefined();
  });

  it('offers Extract to definition on a node inside a definition, not on the body itself', async () => {
    const store = new RuleEditorStore({
      rule: { local: 'pair' },
      definitions: { pair: { rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } } },
    });
    await renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for $.definitions.pair.rule' }));
    expect(screen.queryByRole('menuitem', { name: 'Extract to definition' })).toBeNull();
    fireEvent.keyDown(document.activeElement ?? document.body, { key: 'Escape' });

    fireEvent.click(screen.getByRole('button', { name: 'actions for $.definitions.pair.rule.and[0]' }));
    expect(screen.getByRole('menuitem', { name: 'Extract to definition' })).toBeDefined();
  });

  it('hovering a row inside a definition marks that definition’s strip, not the rule’s', async () => {
    const store = new RuleEditorStore({
      rule: { and: [{ local: 'pair' }, { spec: 'is-vip' }] },
      definitions: { pair: { rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } } },
    });
    const { container } = await renderWith(store);
    const row = screen.getByRole('button', { name: 'select $.definitions.pair.rule.and[1]' }).closest('.node-row')!;
    fireEvent.mouseEnter(row);
    const marked = [...container.querySelectorAll('.dsl-strip-hover')].map((el) => el.textContent).join('');
    expect(marked).toBe('is-adult');
  });

  it('prints a definition body with args in the catalog’s declared order, not insertion order', async () => {
    const store = new RuleEditorStore({
      rule: { local: 'c' },
      definitions: {
        // `label` before `floor` here; the catalog declares `floor` first.
        c: { rule: { spec: 'at-least', args: { label: 'high', floor: 2 } } },
      },
    });
    await renderWith(store, CATALOG);
    const row = document.getElementById('definition-c')!;
    expect(row.textContent).toContain('at-least(floor = 2, label = "high")');
  });

  it('does not show Promote to catalog… without an onPromote handler, and shows it with one', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    await renderWith(store);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition a' }));
    expect(screen.queryByRole('menuitem', { name: 'Promote to catalog…' })).toBeNull();
  });

  it('shows Promote to catalog… with an onPromote handler, and calls it with the name', async () => {
    const store = new RuleEditorStore(twoDefinitionsDocument());
    const onPromote = vi.fn();
    await renderWith(store, EMPTY, onPromote);
    fireEvent.click(screen.getByRole('button', { name: 'actions for definition a' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Promote to catalog…' }));
    expect(onPromote).toHaveBeenCalledWith('a');
  });
});
