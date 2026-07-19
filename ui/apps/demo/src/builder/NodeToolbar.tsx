import {
  isBinaryNode, isSpecNode, type BinaryOperator, type Catalog, type RuleNode,
} from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import { toggleNot } from './mutations.js';
import { childPaths } from './childPaths.js';

const WRAP_OPTIONS: Array<{ label: string; op: BinaryOperator }> = [
  { label: 'AND', op: 'and' },
  { label: 'OR', op: 'or' },
  { label: 'XOR', op: 'xor' },
  { label: 'AndAlso', op: 'andAlso' },
  { label: 'OrElse', op: 'orElse' },
];

/** The header controls for a rule node: expand/collapse, spec select, NOT, wrap, add/remove operand. */
export function NodeToolbar(props: {
  path: string;
  node: RuleNode;
  modelType: string;
  catalog: Catalog;
  expanded: boolean;
  onToggleExpand: () => void;
}) {
  const { path, node, modelType, catalog, expanded, onToggleExpand } = props;
  const store = useRuleEditorStore();
  const hasChildren = childPaths(node, path).length > 0;
  const specOptions = catalog.specs.filter((s) => s.modelType === modelType);
  const fallbackSpec = specOptions[0]?.name ?? 'spec';

  return (
    <div className="node-header toolbar">
      {hasChildren && (
        <button
          type="button"
          className="btn-icon"
          aria-label={`${expanded ? 'collapse' : 'expand'} ${path}`}
          onClick={onToggleExpand}
        >
          {expanded ? '▾' : '▸'}
        </button>
      )}
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
      {path !== '$.rule' && (
        <button type="button" className="btn-danger" aria-label={`remove ${path}`} onClick={() => store.removeOperand(path)}>
          Remove
        </button>
      )}
    </div>
  );
}
