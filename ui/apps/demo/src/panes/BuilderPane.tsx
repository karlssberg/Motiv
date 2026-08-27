import { useState } from 'react';
import {
  EMPTY_ACCORDION, EMPTY_HIGHLIGHT, closeAll, setHovered, setSelected,
  toggleCollapsed, toggleOpen, togglePin,
  type AccordionModel, type Catalog, type HighlightModel, type RulesApiClient,
} from '@motiv-rules/core';
import { useCatalog, useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { BuilderTreeContext, RuleNodeEditor } from '../builder/RuleNodeEditor.js';
import { RuleDslStrip } from '../builder/RuleDslStrip.js';
import { MODEL_TYPE } from '../App.js';

const ROOT = '$.rule';
/** What a pane renders against until (or unless) the real catalog arrives. */
export const EMPTY_CATALOG: Catalog = { specs: [], collections: [] };

/**
 * The recursive rule builder over the boolean grammar, without any surrounding pane chrome — so
 * it can be hosted either by {@link BuilderPane} or as one surface of a pane that toggles between
 * the builder and the DSL text editor.
 *
 * Accordion and highlight state are demo-local UI state, not document state, and are held here so
 * that the tree and the strips above it read the one model rather than each keeping their own.
 */
export function BuilderBody(props: { client: RulesApiClient }) {
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;

  const [model, setModel] = useState<AccordionModel>(EMPTY_ACCORDION);
  /** Which row popup — an actions menu or an operator picker — is open. One at a time, tree-wide. */
  const [openPopover, setOpenPopover] = useState<string | null>(null);
  const [highlight, setHighlight] = useState<HighlightModel>(EMPTY_HIGHLIGHT);
  /** The open insertion slot, if any: a row path plus which of that row's two positions. */
  const [pending, setPending] = useState<{ path: string; where: 'after' | 'first' } | null>(null);
  const editorState = useRuleEditor(useRuleEditorStore());

  return (
    <>
      {catalogState.status === 'loading' && <p>Loading catalog…</p>}
      {catalogState.status === 'error' && <p role="alert">Failed to load catalog.</p>}
      <RuleDslStrip rule={editorState.document.rule} highlight={highlight} />
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
      <BuilderTreeContext.Provider
        value={{
          model,
          toggleCollapsed: (path) => setModel((prev) => toggleCollapsed(prev, path)),
          toggleOpen: (path) => setModel((prev) => toggleOpen(prev, path)),
          togglePin: (path) => setModel((prev) => togglePin(prev, path)),
          openPopover,
          setOpenPopover,
          catalog,
          highlight,
          setHovered: (path) => setHighlight((prev) => setHovered(prev, path)),
          setSelected: (path) => setHighlight((prev) => setSelected(prev, path)),
          pending,
          setPending,
        }}
      >
        <RuleNodeEditor path={ROOT} modelType={MODEL_TYPE} />
      </BuilderTreeContext.Provider>
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
