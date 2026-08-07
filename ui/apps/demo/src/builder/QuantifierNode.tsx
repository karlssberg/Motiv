import type { Catalog, HigherOrderNode } from '@motiv-rules/core';
import { useRuleEditorStore } from '@motiv-rules/react';
import {
  KINDS, N_KINDS, quantifierKindOf, setQuantifierCollection, setQuantifierKind, setQuantifierN,
  type QuantifierKind, type QuantifierLike,
} from './mutations.js';

const KIND_LABELS: Record<QuantifierKind, string> = {
  asAllSatisfied: 'all satisfied',
  asAnySatisfied: 'any satisfied',
  asNSatisfied: 'exactly N satisfied',
  asAtLeastNSatisfied: 'at least N satisfied',
  asAtMostNSatisfied: 'at most N satisfied',
};

/**
 * Config controls for a higher-order quantifier node: kind, target collection, N (when relevant).
 * They live in the node's detail panel, alongside its decoration fields.
 *
 * These configure the quantifier without changing what it is — swapping `all` for `atLeast`
 * leaves a quantifier row hosting a quantifier panel — which is why they belong here while
 * composition (negating, wrapping, removing) does not. Its single child is rendered by the
 * `childPaths` recursion.
 */
export function QuantifierNode(props: {
  path: string;
  node: HigherOrderNode;
  catalog: Catalog;
  modelType: string;
}) {
  const { path, node, catalog, modelType } = props;
  const store = useRuleEditorStore();
  const quantNode = node as unknown as QuantifierLike;
  const kind = quantifierKindOf(quantNode);
  const isNKind = N_KINDS.includes(kind);
  const collection = catalog.collections.find((c) => c.path === quantNode.path);
  const availableCollections = catalog.collections.filter((c) => c.parentModelType === modelType);

  return (
    <div className="node-toolbar">
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
          {availableCollections.map((c) => (
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
    </div>
  );
}
