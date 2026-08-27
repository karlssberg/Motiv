import {
  HIGHER_ORDER_KEYS, N_QUANTIFIER_KINDS, higherOrderKey,
  setQuantifierCollection, setQuantifierKind, setQuantifierN,
  type Catalog, type HigherOrderKey, type HigherOrderNode,
} from '@motiv-rules/core';
import { useRuleEditorStore } from '@motiv-rules/react';

const KIND_LABELS: Record<HigherOrderKey, string> = {
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
  const kind = higherOrderKey(node);
  const isNKind = N_QUANTIFIER_KINDS.includes(kind);
  const collection = catalog.collections.find((c) => c.path === node.path);
  const availableCollections = catalog.collections.filter((c) => c.parentModelType === modelType);

  return (
    <div className="node-toolbar">
      <label className="field">
        <span hidden>quantifier kind at {path}</span>
        <select
          aria-label={`quantifier kind at ${path}`}
          className="control"
          value={kind}
          onChange={(e) => setQuantifierKind(store, path, node, e.target.value as HigherOrderKey)}
        >
          {HIGHER_ORDER_KEYS.map((k) => (
            <option key={k} value={k}>{KIND_LABELS[k]}</option>
          ))}
        </select>
      </label>
      <label className="field">
        <span hidden>quantifier collection at {path}</span>
        <select
          aria-label={`quantifier collection at ${path}`}
          className="control"
          value={node.path}
          onChange={(e) => setQuantifierCollection(store, path, node, e.target.value)}
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
            value={'n' in node && typeof node.n === 'number' ? node.n : 1}
            onChange={(e) => setQuantifierN(store, path, node, Number(e.target.value))}
          />
        </label>
      )}
      <span className="caption">for each {collection?.elementModelType ?? '?'}</span>
    </div>
  );
}
