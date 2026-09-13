import { useState } from 'react';
import { definitionOrder, type Catalog } from '@motiv-rules/core';
import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { DefinitionRow } from './DefinitionRow.js';

/**
 * The document's `definitions` map, laid out one row per local: rename, read its body as DSL,
 * see how many places reference it, edit its own decoration, or reach for the menu that inlines,
 * promotes or removes it.
 *
 * Always present below the builder (in {@link EditorPane}) rather than only once a definition
 * exists — an author with no definitions yet still needs to be told where they will show up once
 * `Extract to definition` makes one. Rows run whole-before-parts (`definitionOrder`), so the
 * definitions the rule uses directly come first and what they are built from follows (#234).
 */
export function DefinitionsPane(props: {
  /**
   * Resolved from the same `useCatalog` call `EditorPane` already makes for the builder — needed
   * so a definition body that calls a parameterised spec prints its args in the catalog's
   * declared order rather than whatever order the document happens to store them in (#234).
   */
  catalog?: Catalog | undefined;
  onPromote?: ((name: string) => void) | undefined;
}) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const definitions = state.document.definitions ?? {};
  const names = definitionOrder(state.document);
  const locals = new Set(names);

  // One popover open at a time, tree-wide — the same rule `NodeMenu` enforces for the builder,
  // kept here as this row's own id rather than the builder's `openPopover` so that opening a
  // definition's menu never has to know about (or close) a builder row's.
  const [openMenu, setOpenMenu] = useState<string | null>(null);

  return (
    <section className="pane definitions-pane" aria-label="Definitions">
      <div className="pane-header">
        <h2>Definitions</h2>
      </div>
      <div className="pane-body">
        {names.length === 0 ? (
          <p className="definitions-empty">No definitions. Extract a node to create one.</p>
        ) : (
          names.map((name) => (
            <DefinitionRow
              key={name}
              name={name}
              definition={definitions[name]!}
              document={state.document}
              taken={new Set(names.filter((other) => other !== name))}
              locals={locals}
              catalog={props.catalog}
              open={openMenu === name}
              setOpen={(open) => setOpenMenu(open ? name : null)}
              onPromote={props.onPromote}
            />
          ))
        )}
      </div>
    </section>
  );
}
