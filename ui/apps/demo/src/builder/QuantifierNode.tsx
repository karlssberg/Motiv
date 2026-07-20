import type { Catalog, HigherOrderNode } from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import {
  KINDS, N_KINDS, quantifierKindOf, setQuantifierCollection, setQuantifierKind, setQuantifierN,
  toggleNot, type QuantifierKind, type QuantifierLike,
} from './mutations.js';

const KIND_LABELS: Record<QuantifierKind, string> = {
  asAllSatisfied: 'all satisfied',
  asAnySatisfied: 'any satisfied',
  asNSatisfied: 'exactly N satisfied',
  asAtLeastNSatisfied: 'at least N satisfied',
  asAtMostNSatisfied: 'at most N satisfied',
};

/** A compact collapsed-state summary of a quantifier node, e.g. `≥2 of orders`. */
function badgeFor(kind: QuantifierKind, path: string, n: unknown): string {
  switch (kind) {
    case 'asAllSatisfied': return `all of ${path}`;
    case 'asAnySatisfied': return `any of ${path}`;
    case 'asNSatisfied': return `=${n ?? 1} of ${path}`;
    case 'asAtLeastNSatisfied': return `≥${n ?? 1} of ${path}`;
    case 'asAtMostNSatisfied': return `≤${n ?? 1} of ${path}`;
    default: return path;
  }
}

/**
 * Header/config row for a higher-order quantifier node: kind, target collection, N (when relevant).
 * The node's single child is rendered by {@link RuleNodeEditor}'s existing `childPaths` recursion.
 */
export function QuantifierNode(props: {
  path: string;
  node: HigherOrderNode;
  catalog: Catalog;
  expanded: boolean;
  onToggleExpand: () => void;
}) {
  const { path, node, catalog, expanded, onToggleExpand } = props;
  const store = useRuleEditorStore();
  const quantNode = node as unknown as QuantifierLike;
  const kind = quantifierKindOf(quantNode);
  const isNKind = N_KINDS.includes(kind);
  const collection = catalog.collections.find((c) => c.path === quantNode.path);

  return (
    <div className="node-header toolbar">
      <button
        type="button"
        className="btn-icon"
        aria-label={`${expanded ? 'collapse' : 'expand'} ${path}`}
        onClick={onToggleExpand}
      >
        {expanded ? '▾' : '▸'}
      </button>
      {!expanded && <span className="badge">{badgeFor(kind, String(quantNode.path), quantNode.n)}</span>}
      {expanded && (
        <>
          <label className="field">
            <span hidden>quantifier kind at {path}</span>
            <select
              aria-label={`quantifier kind at ${path}`}
              className="control"
              value={kind}
              onChange={(e) => setQuantifierKind(store, path, quantNode, e.target.value as QuantifierKind)}
            >
              {KINDS.map((k) => (
                <option key={k} value={k}>{KIND_LABELS[k]}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span hidden>quantifier collection at {path}</span>
            <select
              aria-label={`quantifier collection at ${path}`}
              className="control"
              value={quantNode.path}
              onChange={(e) => setQuantifierCollection(store, path, quantNode, e.target.value)}
            >
              {catalog.collections.map((c) => (
                <option key={c.path} value={c.path}>{c.path}</option>
              ))}
            </select>
          </label>
          {isNKind && (
            <label className="field">
              <span hidden>quantifier n at {path}</span>
              <input
                type="number"
                min={0}
                aria-label={`quantifier n at ${path}`}
                className="control"
                value={typeof quantNode.n === 'number' ? quantNode.n : 1}
                onChange={(e) => setQuantifierN(store, path, quantNode, Number(e.target.value))}
              />
            </label>
          )}
          <span className="caption">for each {collection?.elementModelType ?? '?'}</span>
          <button
            type="button"
            className="btn"
            aria-label={`toggle NOT at ${path}`}
            onClick={() => toggleNot(store, path, node)}
          >
            NOT
          </button>
        </>
      )}
      {path.endsWith(']') && (
        <button type="button" className="btn-danger" aria-label={`remove ${path}`} onClick={() => store.removeOperand(path)}>
          Remove
        </button>
      )}
    </div>
  );
}
