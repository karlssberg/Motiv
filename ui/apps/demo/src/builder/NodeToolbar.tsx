import {
  binaryOperator, isBinaryNode, isSpecNode, operandsOf,
  type BinaryOperator, type Catalog, type RuleNode,
} from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import { toggleNot } from './mutations.js';

const WRAP_OPTIONS: Array<{ label: string; op: BinaryOperator }> = [
  { label: 'AND', op: 'and' },
  { label: 'OR', op: 'or' },
  { label: 'XOR', op: 'xor' },
  { label: 'AndAlso', op: 'andAlso' },
  { label: 'OrElse', op: 'orElse' },
];

/**
 * The structural edit controls for a rule node: NOT, wrap, add/remove operand. They live inside
 * the node's detail panel, which is closed by default.
 *
 * There is deliberately no spec picker here. A node's expression is edited as DSL text in its
 * own row, where completion offers the same catalog specs scoped the same way — so a picker
 * would be a second, narrower way to do what the row already does. `+ quantifier` is gone for
 * the same reason: completion offers the five quantifier keywords.
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

  /**
   * Adds an operand and hands it the keyboard with its seeded spec selected, so the first
   * keystroke replaces it. That is what makes `+ operand` read as "type a new expression"
   * rather than "insert a placeholder you must then hunt down".
   */
  const addOperand = (): void => {
    if (!isBinaryNode(node)) return;
    const childPath = `${path}.${binaryOperator(node)}[${operandsOf(node).length}]`;
    store.addOperand(path, { spec: fallbackSpec });
    // The row mounts on the next render, so the focus waits for it.
    requestAnimationFrame(() => {
      document
        .querySelector<HTMLButtonElement>(`[aria-label="edit expression at ${childPath}"]`)
        ?.focus();
    });
  };

  return (
    <div className="node-toolbar">
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
          onClick={addOperand}
        >
          + operand
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
