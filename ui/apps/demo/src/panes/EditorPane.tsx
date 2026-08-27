import { useState } from 'react';
import type { RulesApiClient } from '@motiv-rules/core';
import { useCatalog, useDslSync, useRuleEditorStore } from '@motiv-rules/react';
import { DslEditor } from '../dsl/DslEditor.js';
import { BuilderBody, EMPTY_CATALOG } from './BuilderPane.js';

/** The two ways this pane lets you author the same rule document. */
type Surface = 'builder' | 'dsl';

/** The tabs, in the order they are offered. */
const SURFACES: ReadonlyArray<{ id: Surface; label: string }> = [
  { id: 'builder', label: 'Builder' },
  { id: 'dsl', label: 'DSL' },
];

/** Ties both tabs to the one panel they swap the contents of. */
const SURFACE_PANEL_ID = 'editor-surface';

/** The id of a surface's tab, which is what names the panel while that surface is shown. */
const tabId = (surface: Surface): string => `editor-tab-${surface}`;

/**
 * The authoring pane: the same rule document edited either through the accordion builder or
 * as DSL text, switched by a tablist in the pane header. Both surfaces are views over the one
 * {@link RuleEditorStore}, so a switch never loses (or forks) the document.
 *
 * The DSL buffer is bound here rather than inside {@link DslEditor}, so it outlives the editing
 * surface: switching to the builder tears down the CodeMirror view while uncommitted text, an
 * unresolved conflict and any pending commit all survive to be picked up on the way back.
 */
export function EditorPane(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;
  const sync = useDslSync(store);

  const [surface, setSurface] = useState<Surface>('builder');

  return (
    <section className="pane" aria-label="Editor">
      <div className="pane-header">
        <div className="surface-tabs" role="tablist" aria-label="Editing surface">
          {SURFACES.map(({ id, label }) => (
            <button
              key={id}
              id={tabId(id)}
              type="button"
              role="tab"
              aria-selected={surface === id}
              aria-controls={SURFACE_PANEL_ID}
              className={surface === id ? 'tab active' : 'tab'}
              onClick={() => setSurface(id)}
            >
              {label}
            </button>
          ))}
        </div>
        {/* The header item that yields when the pane is too narrow for all three (see `.truncate`). */}
        {surface === 'dsl' && <span className="pane-hint truncate">text is the source of truth</span>}
        <button
          type="button"
          className="btn ext-point"
          disabled
          title="requires backend (coming)"
        >
          parameters — coming
        </button>
      </div>

      <div
        role="tabpanel"
        id={SURFACE_PANEL_ID}
        aria-labelledby={tabId(surface)}
        className="surface-panel"
      >
        {surface === 'builder'
          ? <BuilderBody client={props.client} />
          : <DslEditor store={store} catalog={catalog} sync={sync} />}
      </div>
    </section>
  );
}
