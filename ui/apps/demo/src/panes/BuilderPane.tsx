import { useState } from 'react';
import type { Catalog, RulesApiClient } from '@motiv/rules-core';
import { useCatalog } from '@motiv/rules-react';
import { AccordionContext, RuleNodeEditor } from '../builder/RuleNodeEditor.js';
import {
  EMPTY_ACCORDION, closeAll, toggleCollapsed, toggleOpen, togglePin,
  type AccordionModel,
} from '../builder/accordion.js';
import { MODEL_TYPE } from '../App.js';

const ROOT = '$.rule';
/** What a pane renders against until (or unless) the real catalog arrives. */
export const EMPTY_CATALOG: Catalog = { specs: [], collections: [] };

/**
 * The recursive rule builder over the boolean grammar, without any surrounding pane chrome — so
 * it can be hosted either by {@link BuilderPane} or as one surface of a pane that toggles between
 * the builder and the DSL text editor.
 *
 * Accordion state is demo-local UI state, not document state, and is held here so that both the
 * tree and the close-all strip read the one model.
 */
export function BuilderBody(props: { client: RulesApiClient }) {
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;

  const [model, setModel] = useState<AccordionModel>(EMPTY_ACCORDION);
  /** Which row popup — an actions menu or an operator picker — is open. One at a time, tree-wide. */
  const [openPopover, setOpenPopover] = useState<string | null>(null);

  return (
    <>
      {catalogState.status === 'loading' && <p>Loading catalog…</p>}
      {catalogState.status === 'error' && <p role="alert">Failed to load catalog.</p>}
      {/* Height is reserved rather than conditional, so the tree does not jump when the first
          node is pinned. */}
      <div className="accordion-strip">
        {model.pinned.size > 0 && (
          <>
            <span className="caption">{model.pinned.size} pinned</span>
            <button type="button" className="btn" onClick={() => setModel(closeAll)}>
              close all
            </button>
          </>
        )}
      </div>
      <AccordionContext.Provider
        value={{
          model,
          toggleCollapsed: (path) => setModel((prev) => toggleCollapsed(prev, path)),
          toggleOpen: (path) => setModel((prev) => toggleOpen(prev, path)),
          togglePin: (path) => setModel((prev) => togglePin(prev, path)),
          openPopover,
          setOpenPopover,
          catalog,
        }}
      >
        <RuleNodeEditor path={ROOT} modelType={MODEL_TYPE} />
      </AccordionContext.Provider>
    </>
  );
}

/** The builder as a standalone pane, for hosts that show it without the DSL surface. */
export function BuilderPane(props: { client: RulesApiClient }) {
  return (
    <section className="pane" aria-label="Builder">
      <div className="pane-header">
        <h2>Builder</h2>
      </div>
      <BuilderBody client={props.client} />
    </section>
  );
}
