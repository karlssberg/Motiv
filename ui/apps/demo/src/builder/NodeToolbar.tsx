import {
  isBinaryNode, isSpecNode, type BinaryOperator, type Catalog, type RuleNode,
} from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import { insertQuantifier, toggleNot } from './mutations.js';

const WRAP_OPTIONS: Array<{ label: string; op: BinaryOperator }> = [
  { label: 'AND', op: 'and' },
  { label: 'OR', op: 'or' },
  { label: 'XOR', op: 'xor' },
  { label: 'AndAlso', op: 'andAlso' },
  { label: 'OrElse', op: 'orElse' },
];

/**
 * The edit controls for a rule node: spec select, NOT, wrap, add/remove operand. They sit under
 * the node's summary row — which, along with expand/collapse for every node kind, is owned by
 * {@link RuleNodeEditor} — and stay visible whether or not that node is expanded, because a leaf
 * has no chevron of its own to bring them back with once the accordion has collapsed it.
 */
export function NodeToolbar(props: {
  path: string;
  node: RuleNode;
  modelType: string;
  catalog: Catalog;
}) {
  const { path, node, modelType, catalog } = props;
  const store = useRuleEditorStore();
  const specOptions = catalog.specs.filter((s) => s.modelType === modelType);
  const fallbackSpec = specOptions[0]?.name ?? 'spec';

  return (
    <div className="node-toolbar">
      {isSpecNode(node) && (
        <label className="field">
          <span hidden>spec at {path}</span>
          <select
            aria-label={`spec at ${path}`}
            className="control"
            value={node.spec}
            onChange={(e) => store.replaceNode(path, { spec: e.target.value })}
          >
            {specOptions.map((entry) => (
              <option key={entry.name} value={entry.name}>{entry.name}</option>
            ))}
          </select>
        </label>
      )}
      {isSpecNode(node) && (
        <button
          type="button"
          className="btn ext-point"
          disabled
          title="requires backend (coming)"
        >
          expression — coming
        </button>
      )}
      <button type="button" className="btn" aria-label={`toggle NOT at ${path}`} onClick={() => toggleNot(store, path, node)}>
        NOT
      </button>
      {WRAP_OPTIONS.map(({ label, op }) => (
        <button
          key={op}
          type="button"
          className="btn"
          aria-label={`wrap ${path} in ${label}`}
          onClick={() => store.wrapInOperator(path, op, { spec: fallbackSpec })}
        >
          {label}
        </button>
      ))}
      {isBinaryNode(node) && (
        <button
          type="button"
          className="btn"
          aria-label={`add operand to ${path}`}
          onClick={() => store.addOperand(path, { spec: fallbackSpec })}
        >
          + operand
        </button>
      )}
      {isBinaryNode(node) && (
        <button
          type="button"
          className="btn"
          aria-label={`add quantifier to ${path}`}
          onClick={() => insertQuantifier(store, path, catalog, modelType)}
        >
          + quantifier
        </button>
      )}
      {path.endsWith(']') && (
        <button type="button" className="btn-danger" aria-label={`remove ${path}`} onClick={() => store.removeOperand(path)}>
          Remove
        </button>
      )}
    </div>
  );
}
