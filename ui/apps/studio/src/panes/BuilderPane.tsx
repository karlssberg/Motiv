import { useId, useMemo, useState } from 'react';
import {
  EMPTY_ACCORDION, EMPTY_HIGHLIGHT, closeAll, setHovered, setSelected,
  toggleCollapsed, toggleOpen, togglePin,
  type AccordionModel, type Catalog, type HighlightModel, type RulesApiClient,
} from '@motiv-rules/core';
import { useCatalog, useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { BuilderTreeContext, RuleNodeEditor } from '../builder/RuleNodeEditor.js';
import { RuleDslStrip } from '../builder/RuleDslStrip.js';
import { DefinitionsPane } from './DefinitionsPane.js';
import { MODEL_TYPE } from '../App.js';
import { Tooltip } from '../shell/Tooltip.js';

/** The rule's own root path — the one node whose name is the rule's name (#234). */
export const ROOT = '$.rule';
/** What a pane renders against until (or unless) the real catalog arrives. */
export const EMPTY_CATALOG: Catalog = { specs: [], collections: [] };

/**
 * The recursive rule builder over the boolean grammar, without any surrounding pane chrome — so
 * it can be hosted either by {@link BuilderPane} or as one surface of a pane that toggles between
 * the builder and the DSL text editor.
 *
 * Accordion and highlight state are app-local UI state, not document state, and are held here so
 * that the tree and the strips above it read the one model rather than each keeping their own.
 */
export function BuilderBody(props: {
  client: RulesApiClient;
  /** Opens the host's *Extract to catalog* dialog for a row; absent, the row does not offer it (#234). */
  onExtractToCatalog?: ((path: string) => void) | undefined;
  /** Opens the host's *Promote to catalog* dialog for a definition; absent, its menu does not offer it (#234). */
  onPromote?: ((name: string) => void) | undefined;
}) {
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;

  const [model, setModel] = useState<AccordionModel>(EMPTY_ACCORDION);
  /** Which row popup — an actions menu or an operator picker — is open. One at a time, tree-wide. */
  const [openPopover, setOpenPopover] = useState<string | null>(null);
  const [highlight, setHighlight] = useState<HighlightModel>(EMPTY_HIGHLIGHT);
  /** The open insertion slot, if any: a row path plus which of that row's two positions. */
  const [pending, setPending] = useState<{ path: string; where: 'after' | 'first' } | null>(null);
  const editorState = useRuleEditor(useRuleEditorStore());
  /**
   * The document's definition names, which every reparse of a printed row needs: `printInline`
   * renders `{ local: 'a' }` as the bare word `a`, and without this the parser reads it back as a
   * spec reference (#234).
   */
  const locals = useMemo(
    () => new Set(Object.keys(editorState.document.definitions ?? {})),
    [editorState.document],
  );
  /** Names the strip's generated text, so the tree below can be described by it. */
  const expressionId = useId();

  return (
    <>
      {catalogState.status === 'loading' && <p>Loading catalog…</p>}
      {catalogState.status === 'error' && <p role="alert">Failed to load catalog.</p>}
      <RuleDslStrip
        rule={editorState.document.rule}
        highlight={highlight}
        textId={expressionId}
        locals={locals}
        catalog={catalog}
      />
      {/* Height is reserved rather than conditional, so the tree does not jump when the first
          node is pinned. */}
      <div className="accordion-strip">
        {model.pinned.size > 0 && (
          <>
            <span className="caption">{model.pinned.size} pinned</span>
            <Tooltip text="Collapse every pinned node"><button type="button" className="btn" onClick={() => setModel(closeAll)}>
              close all
            </button></Tooltip>
          </>
        )}
      </div>
      {/*
        The composition, described by the one line that states it (ticket 18). Every group *inside*
        the tree is named by its own subtree's generated text, but a rule that is a single spec has
        no subtree and so no group at all — and it is still a composition a reader arriving here
        needs stated. Pointing at the strip rather than repeating the string into an `aria-label`
        keeps one source for it: what is announced is what is on screen, including its marks.
      */}
      <BuilderTreeContext.Provider
        value={{
            model,
            toggleCollapsed: (path) => setModel((prev) => toggleCollapsed(prev, path)),
            toggleOpen: (path) => setModel((prev) => toggleOpen(prev, path)),
            togglePin: (path) => setModel((prev) => togglePin(prev, path)),
            openPopover,
            setOpenPopover,
            catalog,
            locals,
            onExtractToCatalog: props.onExtractToCatalog,
            highlight,
            setHovered: (path) => setHighlight((prev) => setHovered(prev, path)),
            setSelected: (path) => setHighlight((prev) => setSelected(prev, path)),
            pending,
            setPending,
          }}
      >
        <div role="group" aria-label="rule composition" aria-describedby={expressionId}>
          <RuleNodeEditor path={ROOT} modelType={MODEL_TYPE} />
        </div>
        {/* The rule first, its definitions below it: the page reads top-down from what the
            document decides to what it is built from. Inside the provider, because every
            definition body is a tree of these same rows (#234). */}
        <DefinitionsPane onPromote={props.onPromote} />
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
