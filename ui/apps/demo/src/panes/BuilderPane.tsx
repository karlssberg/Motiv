import { useState } from 'react';
import { listPaths, splitLast, type Catalog, type RuleDocument, type RulesApiClient } from '@motiv/rules-core';
import { useCatalog, useRuleEditorStore } from '@motiv/rules-react';
import { AccordionContext, RuleNodeEditor } from '../builder/RuleNodeEditor.js';
import { MODEL_TYPE } from '../App.js';

const ROOT = '$.rule';
const MAX_EXPAND_DEPTH = 5;
const EMPTY_CATALOG: Catalog = { specs: [], collections: [] };

/** Depth of a node path: the number of dot-segments after the root. */
function depthOf(path: string): number {
  if (path === ROOT) return 0;
  return path.slice(ROOT.length).split('.').filter(Boolean).length;
}

/**
 * The prefix identifying a node's sibling group: its parent path, or `null` for the root.
 * The root has no siblings, so it never collides with (nor is collapsed by) any real path.
 */
function parentPrefixOf(path: string): string | null {
  if (path === ROOT) return null;
  return splitLast(path).parentPath;
}

/** Builds the initial expanded-paths set: root down to {@link MAX_EXPAND_DEPTH}. */
function initialExpanded(document: RuleDocument): Set<string> {
  const expanded = new Set<string>();
  for (const { path } of listPaths(document)) {
    if (depthOf(path) <= MAX_EXPAND_DEPTH) expanded.add(path);
  }
  return expanded;
}

/** The recursive single-open-accordion rule builder over the boolean grammar. */
export function BuilderPane(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;

  const [expanded, setExpanded] = useState<Set<string>>(() => initialExpanded(store.getState().document));

  const toggle = (path: string): void => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(path)) {
        next.delete(path);
        return next;
      }
      const prefix = parentPrefixOf(path);
      if (prefix !== null) {
        for (const candidate of next) {
          if (candidate !== path && parentPrefixOf(candidate) === prefix) next.delete(candidate);
        }
      }
      next.add(path);
      return next;
    });
  };

  return (
    <section className="pane" aria-label="Builder">
      <div className="pane-header">
        <h2>Builder</h2>
        <button
          type="button"
          className="btn ext-point"
          disabled
          title="requires backend (coming)"
        >
          parameters — coming
        </button>
      </div>
      {catalogState.status === 'loading' && <p>Loading catalog…</p>}
      {catalogState.status === 'error' && <p role="alert">Failed to load catalog.</p>}
      <AccordionContext.Provider value={{ isExpanded: (path) => expanded.has(path), toggle, catalog }}>
        <RuleNodeEditor path={ROOT} depth={0} modelType={MODEL_TYPE} />
      </AccordionContext.Provider>
    </section>
  );
}
