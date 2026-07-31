import { useMemo, useState, type CSSProperties, type KeyboardEvent, type MouseEvent } from 'react';
import {
  buildNamespaceTree, countLeaves, filterTree,
  type NamespaceNode, type PropositionListEntry,
} from '@motiv/rules-core';

/** What the explorer can ask the page to do. */
export interface ExplorerActions {
  onSelect: (name: string) => void;
  onDerive: (name: string) => void;
  onOverride: (name: string) => void;
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

  // Override composes from another spec over the same model — UI propositions are
  // composition-only, so a compiled spec whose model has no *other* spec offers nothing to build
  // an override from, and the action must not be offered as if it did.
  const canOverride = selectedEntry !== undefined
    && selectedEntry.origin === 'Compiled'
    && entries.some((entry) => entry.modelType === selectedEntry.modelType && entry.name !== selectedEntry.name);

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
          {canOverride && (
            <button type="button" className="btn" onClick={() => actions.onOverride(selectedEntry.name)}>
              Override
            </button>
          )}
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
 *
 * Keyboard support is intentionally minimal: Enter/Space activates the focused row, matching a
 * click. This is not full WAI-ARIA tree navigation — there is no arrow-key movement, type-ahead,
 * or roving tabindex — so the gap is recorded here rather than implied by the `role="tree"`.
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

  // Only a node that *is* a proposition is selectable; a bare namespace is scaffolding.
  const activate = (): void => {
    if (entry) onSelect(entry.name);
  };

  // The click handler always lives on the treeitem itself (not a descendant span) and always
  // stops propagation, even when this node has no entry. A click event bubbles from its target up
  // through ancestors, never down into children, so a handler placed only on a descendant span
  // would never fire when the treeitem element itself is the click target. And if a bare-namespace
  // row simply carried no handler at all, an unhandled click would keep bubbling past it into
  // whichever ancestor *does* have one — e.g. a dual-role namespace/proposition further up the
  // tree — silently selecting that ancestor instead of doing nothing. Always stopping propagation
  // here, then deciding separately whether to call `onSelect`, preserves both: nothing is selected
  // for a bare namespace, and no click leaks past it either.
  const handleClick = (event: MouseEvent<HTMLLIElement>): void => {
    event.stopPropagation();
    activate();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLLIElement>): void => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault(); // Space must not scroll the page
      activate();
    }
  };

  // The accessible name is composed from this node's own segment plus its origin/quarantine state
  // — not the segment alone — because those badges (compiled/overridden/authored, quarantined)
  // are this task's whole deliverable, and an explicit `aria-label` overrides element content
  // entirely in ARIA name computation. Without this, a screen reader announces only the segment
  // and never the state the visible badges carry. (Scoping the name to this node at all — rather
  // than leaving it to default content-based computation — is still necessary: without it, a
  // treeitem's accessible name aggregates its whole subtree, so a namespace's name would include
  // every leaf nested beneath it and collide with queries for those leaves.)
  const accessibleName = entry
    ? [node.segment, ORIGIN_LABEL[entry.origin], quarantined ? 'quarantined' : null]
        .filter((part): part is string => part !== null)
        .join(' ')
    : node.segment;

  return (
    <li
      role="treeitem"
      aria-label={accessibleName}
      aria-selected={entry?.name === selected}
      aria-expanded={node.children.length > 0 ? true : undefined}
      // Only a proposition is focusable; a bare namespace stays out of the tab order entirely.
      tabIndex={entry ? 0 : undefined}
      className="explorer-node"
      style={{ '--depth': depth } as CSSProperties}
      title={quarantined ? quarantine.map((error) => error.message).join('\n') : undefined}
      onClick={handleClick}
      onKeyDown={entry ? handleKeyDown : undefined}
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
