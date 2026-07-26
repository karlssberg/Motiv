import { useEffect, type KeyboardEvent } from 'react';
import { binaryOperator, type BinaryNode } from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import { BINARY_OPERATORS, setBinaryOperator } from './mutations.js';
import { OPERATOR_LABELS } from './nodeSummary.js';
import { usePopoverCard } from './usePopoverCard.js';

/** Where the arrow keys land from `index`, or `null` for a key the list does not handle. */
function nextIndex(key: string, index: number, length: number): number | null {
  // The ends are stops rather than wrap-arounds, matching the native select this replaced.
  if (key === 'ArrowDown') return Math.min(index + 1, length - 1);
  if (key === 'ArrowUp') return Math.max(index - 1, 0);
  if (key === 'Home') return 0;
  if (key === 'End') return length - 1;
  return null;
}

/**
 * A binary node's badge, doubling as the control that changes its operator.
 *
 * The badge is the value, so it is drawn as the badge it replaces rather than as a form control —
 * and it is exactly as wide as the operator it names. A native `<select>` cannot do that: it
 * reserves the width of its longest option, which leaves a hole after `OR` that reads as a
 * missing word in the expression.
 *
 * Losing the native control means owning what it gave away for free, which is most of this file.
 * The operator is named as well as shown, because a control's accessible name replaces its text:
 * label it `operator at …` alone and the one thing the badge exists to say is exactly what a
 * screen reader never hears. Focus opens onto the operator in force and the arrow keys walk from
 * there, since `listbox` promises that and a column of tab stops is not it.
 *
 * Choosing is `listbox`/`option` rather than the `menu`/`menuitem` next door because this picks a
 * value rather than invoking an action — and because the two popups then stay distinguishable.
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
  const { trigger, card, style, placed, close } = usePopoverCard(open, setOpen);
  const current = binaryOperator(node);
  const listboxId = `operators-${path}`;

  const options = (): HTMLElement[] => Array.from(card.current?.querySelectorAll('[role="option"]') ?? []);

  // Opening puts the keyboard on the operator in force, so the list starts where the value is.
  // `placed` is a dependency, not just a guard: until the card has been measured it is
  // `visibility: hidden`, and a hidden element refuses focus — silently, and only in a real
  // browser, since jsdom focuses it regardless. Without the re-run once it is placed, the one
  // attempt lands while it is still hidden and the keyboard never enters the list. Covered by
  // `e2e/operator.spec.ts`, which is the only harness that can see it.
  useEffect(() => {
    if (!open || !placed) return;
    options()[BINARY_OPERATORS.indexOf(current)]?.focus();
  }, [open, placed]);

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>): void => {
    const items = options();
    const target = nextIndex(event.key, items.indexOf(document.activeElement as HTMLElement), items.length);
    if (target === null) return;
    event.preventDefault();
    items[target]?.focus();
  };

  return (
    <>
      <button
        ref={trigger}
        type="button"
        role="combobox"
        className="node-badge node-badge-op node-operator"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listboxId}
        aria-label={`operator at ${path}, ${OPERATOR_LABELS[current]}`}
        onClick={() => setOpen(!open)}
      >
        {OPERATOR_LABELS[current]}
      </button>
      {open && (
        <div
          ref={card}
          id={listboxId}
          role="listbox"
          className="node-menu node-operator-menu"
          style={style}
          aria-label={`operators for ${path}`}
          onKeyDown={onKeyDown}
        >
          {BINARY_OPERATORS.map((operator) => (
            <button
              key={operator}
              type="button"
              role="option"
              aria-selected={operator === current}
              className="node-menu-item"
              onClick={() => { setBinaryOperator(store, path, node, operator); close(); }}
            >
              {OPERATOR_LABELS[operator]}
            </button>
          ))}
        </div>
      )}
    </>
  );
}
