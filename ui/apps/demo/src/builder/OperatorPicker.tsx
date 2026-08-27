import {
  BINARY_OPERATORS, OPERATOR_LABELS, binaryOperator, setBinaryOperator, type BinaryNode,
} from '@motiv-rules/core';
import { useRuleEditorStore } from '@motiv-rules/react';
import { ListboxPicker } from './ListboxPicker.js';

const OPTIONS = BINARY_OPERATORS.map((operator) => ({ value: operator, label: OPERATOR_LABELS[operator] }));

/**
 * A binary node's badge, doubling as the control that changes its operator.
 *
 * The badge is the value, so it is drawn as the badge it replaces rather than as a form control —
 * and it is exactly as wide as the operator it names. A native `<select>` cannot do that: it
 * reserves the width of its longest option, which leaves a hole after `OR` that reads as a
 * missing word in the expression. `ListboxPicker` owns what that costs.
 *
 * Making the badge interactive is safe only on an expanded row: once the subtree collapses, this
 * same slot hosts the DSL text editor, and a control nested inside one would fight it for events.
 */
export function OperatorPicker(props: {
  path: string;
  node: BinaryNode;
  open: boolean;
  /** Opens this picker, or closes whichever popup is open. Held by the host so only one ever is. */
  setOpen: (open: boolean) => void;
}) {
  const { path, node, open, setOpen } = props;
  const store = useRuleEditorStore();
  const current = binaryOperator(node);

  return (
    <ListboxPicker
      options={OPTIONS}
      value={current}
      onChoose={(operator) => setBinaryOperator(store, path, node, operator)}
      open={open}
      setOpen={setOpen}
      triggerName={`operator at ${path}`}
      listLabel={`operators for ${path}`}
      triggerClassName="node-badge node-badge-op"
      listClassName="node-operator-menu"
    />
  );
}
