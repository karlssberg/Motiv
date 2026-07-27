import {
  binaryOperator, isBinaryNode, operandsOf,
  type BinaryOperator, type RuleDocument, type RuleNode,
} from './document.js';
import { normalizeAt } from './normalize.js';
import { getNode, setNode, splitLast } from './paths.js';

/**
 * Where an insertion goes. Two kinds, not three: appending onto an operator row is a `slot` whose
 * index is the operand count, so it needs no case of its own.
 *
 * - `slot` — become operand `index` of the n-ary operator at `parentPath`.
 * - `wrap` — replace the node at `path` with `and: [thatNode, inserted]`. This is how a position
 *   beside a node with no operand list of its own — the root rule, a NOT's child, a quantifier's
 *   body — is expressed.
 */
export type InsertTarget =
  | { kind: 'slot'; parentPath: string; index: number }
  | { kind: 'wrap'; path: string };

/** The operator a `wrap` introduces. Never inferred: the new parent's picker sits one click away. */
const WRAP_OPERATOR: BinaryOperator = 'and';

/**
 * The target for the `+` on the row at `path`, which means the same thing on every row: *insert a
 * sibling immediately after me*.
 *
 * A row that is an operand — its path ends in `[i]` — resolves to the slot after it. Every other
 * row has no list to be a sibling within, so "after me" is expressed as a wrap.
 *
 * One button per row cannot reach every slot, and no assignment fixes that: a row participates in
 * both its parent's list and its own children's, so `and: [a, {or: [b, c]}, d]` offers seven slots
 * to six rows. The unreachable position — before an operator's first child — is served by
 * {@link firstOperandTarget} from the row's menu instead.
 */
export function insertTargetForRow(path: string): InsertTarget {
  if (!path.endsWith(']')) return { kind: 'wrap', path };
  const { parentPath, step } = splitLast(path);
  return { kind: 'slot', parentPath, index: step.index! + 1 };
}

/** The target for `Insert first operand` on the operator row at `operatorPath`. */
export function firstOperandTarget(operatorPath: string): InsertTarget {
  return { kind: 'slot', parentPath: operatorPath, index: 0 };
}

/**
 * A new document with `node` inserted at `target`, then normalized at the point of change.
 *
 * Pure: this is the same function the preview prints and the commit applies, so a preview cannot
 * describe a mutation different from the one it triggers.
 */
export function planInsert(document: RuleDocument, target: InsertTarget, node: RuleNode): RuleDocument {
  if (target.kind === 'wrap') {
    const existing = getNode(document, target.path);
    if (!existing) throw new Error(`No node at ${target.path}.`);
    const wrapped = { [WRAP_OPERATOR]: [existing, node] } as unknown as RuleNode;
    return normalizeAt(setNode(document, target.path, wrapped), target.path);
  }

  const parent = getNode(document, target.parentPath);
  if (!parent || !isBinaryNode(parent)) throw new Error(`${target.parentPath} is not an operator node.`);
  const operator = binaryOperator(parent);
  const operands = [...operandsOf(parent)];
  operands.splice(target.index, 0, node);
  const next = { ...parent, [operator]: operands } as unknown as RuleNode;
  return normalizeAt(setNode(document, target.parentPath, next), target.parentPath);
}
