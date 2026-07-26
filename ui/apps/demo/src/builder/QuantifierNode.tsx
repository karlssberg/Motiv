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

/**
 * Config controls for a higher-order quantifier node: kind, target collection, N (when relevant).
 * They sit under the node's summary row — chevron and badge alike live in {@link RuleNodeEditor},
 * which owns expand/collapse for every node kind — and, like {@link NodeToolbar}, stay visible
 * whether or not the node is expanded. Its single child is rendered by the `childPaths` recursion.
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
      <button
        type="button"
        className="btn"
        aria-label={`toggle NOT at ${path}`}
        onClick={() => toggleNot(store, path, node)}
      >
        NOT
      </button>
      {path.endsWith(']') && (
        <button type="button" className="btn-danger" aria-label={`remove ${path}`} onClick={() => store.removeOperand(path)}>
          Remove
        </button>
      )}
    </div>
  );
}
