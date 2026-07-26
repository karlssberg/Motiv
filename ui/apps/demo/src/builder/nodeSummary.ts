import {
  binaryOperator, higherOrderKey, isBinaryNode, isExpressionNode, isHigherOrderNode, isNotNode,
  isSpecNode,
  type BinaryOperator, type Countable, type HigherOrderKey, type RuleNode,
} from '@motiv/rules-core';

/** What a node's badge is: the row renders it as a `.node-badge-{kind}` class, which colours it. */
export type NodeBadgeKind = 'op' | 'quant' | 'spec';

/**
 * The one-line summary shown on a parent row while its subtree is expanded. A collapsed parent,
 * and every leaf, shows the node's DSL text instead — so the leaf branches below are reached
 * only if a future caller summarises a node the tree renders as text.
 */
export interface NodeSummary {
  /** The short colored token, e.g. `AND`, `atLeast(3)`, `is-active`. */
  badge: string;
  /** The plain-language gloss beside the badge, e.g. `all must hold`, `in orders`. Empty for leaves. */
  description: string;
  kind: NodeBadgeKind;
}

/** What each binary operator is called on a row — the badge's text, and the picker's options. */
export const OPERATOR_LABELS: Record<BinaryOperator, string> = {
  and: 'AND', or: 'OR', xor: 'XOR', andAlso: 'AndAlso', orElse: 'OrElse',
};
const OP_DESCRIPTION: Record<BinaryOperator, string> = {
  and: 'all must hold', or: 'any may hold', xor: 'exactly one must hold',
  andAlso: 'all must hold, short-circuit', orElse: 'any may hold, short-circuit',
};
const QUANT_TOKEN: Record<HigherOrderKey, (n: Countable | undefined) => string> = {
  asAllSatisfied: () => 'all',
  asAnySatisfied: () => 'any',
  asNSatisfied: (n) => `exactly(${n ?? 1})`,
  asAtLeastNSatisfied: (n) => `atLeast(${n ?? 1})`,
  asAtMostNSatisfied: (n) => `atMost(${n ?? 1})`,
};

/** The one-line summary shown on an expanded parent row, regardless of node kind. */
export function summarize(node: RuleNode): NodeSummary {
  if (isHigherOrderNode(node)) {
    // `'n' in node` is the discriminator the printer uses: only the three N-kinds carry a count.
    const n = 'n' in node ? node.n : undefined;
    return { badge: QUANT_TOKEN[higherOrderKey(node)](n), description: `in ${node.path}`, kind: 'quant' };
  }
  if (isNotNode(node)) return { badge: 'NOT', description: 'must not hold', kind: 'op' };
  if (isBinaryNode(node)) {
    const op = binaryOperator(node);
    return { badge: OPERATOR_LABELS[op], description: OP_DESCRIPTION[op], kind: 'op' };
  }
  if (isExpressionNode(node)) return { badge: node.expression, description: '', kind: 'spec' };
  if (isSpecNode(node)) return { badge: node.spec, description: '', kind: 'spec' };
  return { badge: '?', description: '', kind: 'spec' };
}
