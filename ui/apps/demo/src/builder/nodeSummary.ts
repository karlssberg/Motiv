import {
  binaryOperator, isBinaryNode, isExpressionNode, isHigherOrderNode, isNotNode, isSpecNode,
  type BinaryOperator, type RuleNode,
} from '@motiv/rules-core';
import { quantifierKindOf, type QuantifierKind, type QuantifierLike } from './mutations.js';

/** What a node's badge is: the row renders it as a `.node-badge-{kind}` class, which colours it. */
export type NodeBadgeKind = 'op' | 'quant' | 'spec';

/** The always-visible one-line summary for a node's accordion row. */
export interface NodeSummary {
  /** The short colored token, e.g. `AND`, `atLeast(3)`, `is-active`. */
  badge: string;
  /** The plain-language gloss beside the badge, e.g. `all must hold`, `in orders`. Empty for leaves. */
  description: string;
  kind: NodeBadgeKind;
}

const OP_LABEL: Record<BinaryOperator, string> = { and: 'AND', or: 'OR', xor: 'XOR', andAlso: 'AndAlso', orElse: 'OrElse' };
const OP_DESCRIPTION: Record<BinaryOperator, string> = {
  and: 'all must hold', or: 'any may hold', xor: 'exactly one must hold',
  andAlso: 'all must hold, short-circuit', orElse: 'any may hold, short-circuit',
};
const QUANT_TOKEN: Record<QuantifierKind, (n: unknown) => string> = {
  asAllSatisfied: () => 'all',
  asAnySatisfied: () => 'any',
  asNSatisfied: (n) => `exactly(${n ?? 1})`,
  asAtLeastNSatisfied: (n) => `atLeast(${n ?? 1})`,
  asAtMostNSatisfied: (n) => `atMost(${n ?? 1})`,
};

/** The one-line summary shown on a node's accordion row, regardless of node kind. */
export function summarize(node: RuleNode): NodeSummary {
  if (isHigherOrderNode(node)) {
    const quant = node as unknown as QuantifierLike;
    const kind = quantifierKindOf(quant);
    return { badge: QUANT_TOKEN[kind](quant.n), description: `in ${quant.path}`, kind: 'quant' };
  }
  if (isNotNode(node)) return { badge: 'NOT', description: 'must not hold', kind: 'op' };
  if (isBinaryNode(node)) {
    const op = binaryOperator(node);
    return { badge: OP_LABEL[op], description: OP_DESCRIPTION[op], kind: 'op' };
  }
  if (isExpressionNode(node)) return { badge: node.expression, description: '', kind: 'spec' };
  if (isSpecNode(node)) return { badge: node.spec, description: '', kind: 'spec' };
  return { badge: '?', description: '', kind: 'spec' };
}
