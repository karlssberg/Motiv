import { useState } from 'react';
import type { RulesApiClient } from '@motiv/rules-core';
import { useCatalog, useRuleEditorStore } from '@motiv/rules-react';
import { DslEditor } from '../dsl/DslEditor.js';
import { BuilderBody, EMPTY_CATALOG } from './BuilderPane.js';

/** The two ways this pane lets you author the same rule document. */
type Surface = 'builder' | 'dsl';

/** The tabs, in the order they are offered. */
const SURFACES: ReadonlyArray<{ id: Surface; label: string }> = [
  { id: 'builder', label: 'Builder' },
  { id: 'dsl', label: 'DSL' },
];

/**
 * The authoring pane: the same rule document edited either through the accordion builder or
 * as DSL text, switched by a tablist in the pane header. Both surfaces are views over the one
 * {@link RuleEditorStore}, so a switch never loses (or forks) the document.
 */
export function EditorPane(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;

  const [surface, setSurface] = useState<Surface>('builder');

  return (
    <section className="pane" aria-label="Editor">
      <div className="pane-header">
        <div className="surface-tabs" role="tablist" aria-label="Editing surface">
          {SURFACES.map(({ id, label }) => (
            <button
              key={id}
              type="button"
              role="tab"
              aria-selected={surface === id}
              className={surface === id ? 'tab active' : 'tab'}
              onClick={() => setSurface(id)}
            >
              {label}
            </button>
          ))}
        </div>
        {surface === 'dsl' && <span className="pane-hint">text is the source of truth</span>}
      </div>

      {surface === 'builder'
        ? <BuilderBody client={props.client} />
        : <DslEditor store={store} catalog={catalog} />}
    </section>
  );
}
