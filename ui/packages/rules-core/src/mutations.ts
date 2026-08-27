import {
  binaryOperator, higherOrderBody, higherOrderKey, operandsOf,
  type BinaryNode, type BinaryOperator, type HigherOrderKey, type HigherOrderNode, type RuleNode,
} from './document.js';
import type { RuleEditorStore } from './editor.js';

/**
 * Rebuilds a binary node under a different operator, keeping its operands in order and its
 * decoration (`name`/`whenTrue`/`whenFalse`). The old operator key is dropped, since the key
 * *is* the operator — a node carrying two of them would be ambiguous rather than merely wrong.
 */
export function setBinaryOperator(
  store: RuleEditorStore, path: string, node: BinaryNode, operator: BinaryOperator,
): void {
  const previous = binaryOperator(node);
  if (previous === operator) return;
  const { [previous]: _operands, ...rest } = node as unknown as Record<string, unknown>;
  store.replaceNode(path, { ...rest, [operator]: operandsOf(node) } as unknown as RuleNode);
}

/** The higher-order quantifier kinds that carry an `n` count. */
export const N_QUANTIFIER_KINDS: readonly HigherOrderKey[] = [
  'asNSatisfied', 'asAtLeastNSatisfied', 'asAtMostNSatisfied',
];

/**
 * The `n` count a node carries, when it is an N-kind node holding a literal count — `undefined`
 * for the kinds without one and for a `@param` reference. Exported so a control *displaying*
 * the count and the mutations *committing* it share one fallback rule instead of drifting.
 */
export function literalCountOf(node: HigherOrderNode): number | undefined {
  return 'n' in node && typeof node.n === 'number' ? node.n : undefined;
}

/**
 * Rebuilds a quantifier node under a new kind, preserving its child, collection path, decoration
 * (`name`/`whenTrue`/`whenFalse`), and `n` only for N-kinds. The old kind key and any stale `n` are dropped.
 */
export function setQuantifierKind(
  store: RuleEditorStore, path: string, node: HigherOrderNode, kind: HigherOrderKey,
): void {
  const oldKind = higherOrderKey(node);
  const child = higherOrderBody(node);
  const { [oldKind]: _oldChild, n: _oldN, ...rest } = node as unknown as Record<string, unknown>;
  const rebuilt: Record<string, unknown> = {
    ...rest, // keeps path, name, whenTrue, whenFalse
    [kind]: child,
    ...(N_QUANTIFIER_KINDS.includes(kind) ? { n: literalCountOf(node) ?? 1 } : {}),
  };
  store.replaceNode(path, rebuilt as unknown as RuleNode);
}

/** Repoints a quantifier node at a different registered collection. */
export function setQuantifierCollection(
  store: RuleEditorStore, path: string, node: HigherOrderNode, collectionPath: string,
): void {
  store.replaceNode(path, { ...node, path: collectionPath });
}

/** Updates the `n` count on an N-kind quantifier node, ignoring non-finite values. */
export function setQuantifierN(
  store: RuleEditorStore, path: string, node: HigherOrderNode, n: number,
): void {
  const safeN = Number.isFinite(n) ? n : literalCountOf(node) ?? 1;
  store.replaceNode(path, { ...node, n: safeN } as RuleNode);
}
