import { useId, useState, type ReactNode } from 'react';
import type { RulesApiClient } from '@motiv-rules/core';
import { useCatalog, useDslSync, useRuleEditorStore } from '@motiv-rules/react';
import { DslEditor } from '../dsl/DslEditor.js';
import { BuilderBody, EMPTY_CATALOG } from './BuilderPane.js';
import { DefinitionsPane } from './DefinitionsPane.js';

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
 *
 * The DSL buffer is bound here rather than inside {@link DslEditor}, so it outlives the editing
 * surface: switching to the builder tears down the CodeMirror view while uncommitted text, an
 * unresolved conflict and any pending commit all survive to be picked up on the way back.
 */
export function EditorPane(props: {
  client: RulesApiClient;
  /**
   * What the pane is editing, named by the host (a `DocumentTitle`, in Studio). First in the header,
   * so the row reads as "this document, edited this way", with the surface tabs at its end.
   */
  title?: ReactNode;
  /** The document's name as the DSL surface files it; absent for a nameless draft. */
  documentName?: string | undefined;
  /**
   * The document's own actions (JSON, Save), drawn at the end of the header after the surface tabs — on the
   * pane that holds the document, so they read as belonging to it. Optional: the pages still
   * draw theirs in the app bar. (Added for the dynamic-tabs prototype; kept because it is the
   * seam the chosen direction needs.)
   */
  actions?: ReactNode | undefined;
  /**
   * Opens the host's *Extract to catalog* dialog for a builder row, and its *Promote to catalog*
   * dialog for a definition. Absent, neither action is offered: the dialog belongs to the shell
   * that owns the proposition listing, not to the pane (#234).
   */
  onExtractToCatalog?: ((path: string) => void) | undefined;
  onPromote?: ((name: string) => void) | undefined;
}) {
  const store = useRuleEditorStore();
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;
  const sync = useDslSync(store);

  const [surface, setSurface] = useState<Surface>('builder');

  // Per instance: several editors are mounted at once, one per open tab, and an id shared between
  // them would point every `aria-controls` at whichever came first in the document.
  const idBase = useId();
  /** The one panel both tabs swap the contents of. */
  const SURFACE_PANEL_ID = `${idBase}-surface`;
  /** The id of a surface's tab, which is what names the panel while that surface is shown. */
  const tabId = (which: Surface): string => `${idBase}-tab-${which}`;

  return (
    <section className="pane" aria-label="Editor">
      <div className="pane-header">
        {props.title !== undefined && <div className="pane-title truncate">{props.title}</div>}
        <div className="pane-header-fill" />
        {/* The header item that yields when the pane is too narrow for everything (see `.truncate`). */}
        {surface === 'dsl' && <span className="pane-hint truncate">text is the source of truth</span>}
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
        {/* Last in the row, so Save — the action the document is for — ends it. */}
        {props.actions !== undefined && <div className="pane-actions">{props.actions}</div>}
      </div>

      <div
        role="tabpanel"
        id={SURFACE_PANEL_ID}
        aria-labelledby={tabId(surface)}
        className="surface-panel"
      >
        {surface === 'builder'
          ? (
            <>
              {/* Above the builder rather than beside it, so a definition is one scroll away from
                  every `local` reference that points at it, not off in a separate column (#234). */}
              <DefinitionsPane catalog={catalog} onPromote={props.onPromote} />
              <BuilderBody client={props.client} onExtractToCatalog={props.onExtractToCatalog} />
            </>
          )
          : <DslEditor store={store} catalog={catalog} sync={sync} documentName={props.documentName} />}
      </div>
    </section>
  );
}
