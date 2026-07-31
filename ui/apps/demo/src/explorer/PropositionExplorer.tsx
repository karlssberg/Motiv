import { useMemo, useState, type CSSProperties, type MouseEvent } from 'react';
import {
  buildNamespaceTree, countLeaves, filterTree,
  type NamespaceNode, type PropositionListEntry,
} from '@motiv/rules-core';

/** What the explorer can ask the page to do. */
export interface ExplorerActions {
  onSelect: (name: string) => void;
  onDerive: (name: string) => void;
  onNew: () => void;
  onDelete: (entry: PropositionListEntry) => void;
}

const ORIGIN_LABEL: Record<PropositionListEntry['origin'], string> = {
  Compiled: 'compiled',
  Overridden: 'overridden',
  Authored: 'authored',
};

/**
 * The namespaced tree rail. The hierarchy is a pure projection of the dotted names — there is no
 * stored folder structure — so a rename moves a proposition and nothing else needs to know.
 */
export function PropositionExplorer(props: {
  entries: PropositionListEntry[];
  selected: string | null;
  actions: ExplorerActions;
}) {
  const { entries, selected, actions } = props;
  const [query, setQuery] = useState('');
  const [models, setModels] = useState<string[]>([]);

  const tree = useMemo(() => buildNamespaceTree(entries), [entries]);
  const filtered = useMemo(() => filterTree(tree, query, models), [tree, query, models]);
  const total = entries.length;
  const shown = countLeaves(filtered);

  const modelTypes = useMemo(
    () => [...new Set(entries.map((entry) => entry.modelType))].sort(),
    [entries],
  );

  const selectedEntry = entries.find((entry) => entry.name === selected);

  const toggleModel = (model: string): void =>
    setModels((current) =>
      current.includes(model) ? current.filter((kept) => kept !== model) : [...current, model]);

  return (
    <aside className="explorer" aria-label="Propositions">
      <div className="explorer-header">
        <input
          type="search"
          className="explorer-search"
          aria-label="Filter propositions"
          placeholder="Filter…"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
        <button type="button" className="btn" onClick={actions.onNew}>New</button>
      </div>

      <div className="explorer-chips">
        {modelTypes.map((model) => (
          <button
            key={model}
            type="button"
            className="model-pill"
            // `aria-pressed` is the single source of truth for the toggled state — the stylesheet
            // selects on it, as it already does for the builder's toggles, so there is no parallel
            // class to keep in step with it.
            aria-pressed={models.includes(model)}
            onClick={() => toggleModel(model)}
          >
            {model}
          </button>
        ))}
        <span className="explorer-count">{shown} of {total}</span>
      </div>

      {shown === 0
        ? <p className="explorer-empty">No propositions match “{query}”.</p>
        : (
          <ul className="explorer-tree" role="tree" aria-label="Proposition namespaces">
            {filtered.map((node) => (
              <TreeNode
                key={node.path}
                node={node}
                depth={0}
                selected={selected}
                onSelect={actions.onSelect}
              />
            ))}
          </ul>
        )}

      {selectedEntry && (
        <div className="explorer-actions">
          <button type="button" className="btn" onClick={() => actions.onDerive(selectedEntry.name)}>
            Derive from this
          </button>
          {selectedEntry.origin !== 'Compiled' && (
            <button type="button" className="btn" onClick={() => actions.onDelete(selectedEntry)}>
              {selectedEntry.origin === 'Overridden' ? 'Revert to compiled' : 'Delete'}
            </button>
          )}
        </div>
      )}
    </aside>
  );
}

/**
 * One node. A node can be both a namespace and a proposition — `customer` may be a name in its own
 * right — so the entry and the children are rendered independently rather than as an either/or.
 */
function TreeNode(props: {
  node: NamespaceNode;
  depth: number;
  selected: string | null;
  onSelect: (name: string) => void;
}) {
  const { node, depth, selected, onSelect } = props;
  const entry = node.entry;
  const quarantine = entry?.quarantine ?? [];
  const quarantined = quarantine.length > 0;

  // The click handler lives on the treeitem itself (not a descendant span): a click event bubbles
  // from its target up through ancestors, never down into children, so a handler placed only on an
  // inner span would never fire when the treeitem element is the click target. Only a node that
  // *is* a proposition is selectable; a bare namespace is scaffolding, so its handler must be
  // `undefined` — not a no-op — so nothing runs at all. `stopPropagation` keeps a node that is both
  // a namespace and a proposition from also re-triggering an ancestor's handler when one of its own
  // descendants is clicked.
  const select = entry
    ? (event: MouseEvent<HTMLLIElement>): void => { event.stopPropagation(); onSelect(entry.name); }
    : undefined;

  return (
    <li
      role="treeitem"
      // Without this, a treeitem's accessible name aggregates its entire subtree's text (the
      // ARIA name-from-content computation walks descendants), so a namespace node's name would
      // include every leaf nested beneath it and collide with queries for those leaves. Scoping
      // the name to this node's own segment is the standard remedy, not a structural change.
      aria-label={node.segment}
      aria-selected={entry?.name === selected}
      aria-expanded={node.children.length > 0 ? true : undefined}
      className="explorer-node"
      style={{ '--depth': depth } as CSSProperties}
      title={quarantined ? quarantine.map((error) => error.message).join('\n') : undefined}
      onClick={select}
    >
      <span className={entry ? 'explorer-leaf' : 'explorer-namespace'}>
        <span className="explorer-segment">{node.segment}</span>
        {entry && (
          <>
            <span className="model-pill">{entry.modelType}</span>
            <span className="origin-badge">{ORIGIN_LABEL[entry.origin]}</span>
            {quarantined && <span className="quarantine-badge">quarantined</span>}
          </>
        )}
      </span>

      {node.children.length > 0 && (
        <ul role="group">
          {node.children.map((child) => (
            <TreeNode
              key={child.path}
              node={child}
              depth={depth + 1}
              selected={selected}
              onSelect={onSelect}
            />
          ))}
        </ul>
      )}
    </li>
  );
}
