import {
  binaryOperator, higherOrderBody, higherOrderKey, isBinaryNode, isHigherOrderNode, isNotNode,
  operandsOf, type BinaryOperator, type RuleNode, type RuleDocument,
} from './document.js';
import { getNode, setNode } from './paths.js';

/**
 * The operators whose nesting is safe to dissolve. `xor` is absent deliberately: the binders fold
 * operands pairwise, so an n-ary `xor` is parity ("an odd number hold") rather than one-of, and a
 * flattened `xor` would invite the wrong reading of a document that already means something else.
 *
 * `and`/`andAlso` and `or`/`orElse` are distinct keys with distinct short-circuit semantics, so a
 * run only ever merges into a parent carrying the *same* key. That falls out of the equality check
 * below rather than needing a rule of its own.
 */
const FLATTENABLE: readonly BinaryOperator[] = ['and', 'or', 'andAlso', 'orElse'];

/**
 * True when dissolving this node would destroy something. A `name` or a `whenTrue`/`whenFalse`
 * payload belongs to the node, and a node spliced away has nowhere to put it — so decoration is
 * the signal that a nesting is deliberate rather than residual.
 */
function isDecorated(node: RuleNode): boolean {
  return node.name !== undefined || node.whenTrue !== undefined || node.whenFalse !== undefined;
}

/**
 * Rebuilds a subtree with residual same-operator nesting removed. Children are rewritten before
 * the parent merges them, so nesting of any depth collapses in this single pass: by the time a
 * child is considered for splicing it is already flat.
 */
function flatten(node: RuleNode): RuleNode {
  if (isNotNode(node)) return { ...node, not: flatten(node.not) };

  if (isHigherOrderNode(node)) {
    const key = higherOrderKey(node);
    return { ...node, [key]: flatten(higherOrderBody(node)) } as unknown as RuleNode;
  }

  if (!isBinaryNode(node)) return node;

  const operator = binaryOperator(node);
  const children = operandsOf(node).map(flatten);
  const merged = FLATTENABLE.includes(operator)
    ? children.flatMap((child) => (
      isBinaryNode(child) && binaryOperator(child) === operator && !isDecorated(child)
        ? operandsOf(child)
        : [child]
    ))
    : children;

  return { ...node, [operator]: merged } as unknown as RuleNode;
}

/**
 * Returns a new document with residual same-operator nesting removed from the *entire* subtree
 * rooted at `path` — every descendant, not only the nodes near whatever a caller's mutation
 * actually changed. `flatten` recurses unconditionally, so a sibling operand's subtree that the
 * triggering gesture never touched is flattened too, as a side effect of sharing an ancestor with
 * the node that did change.
 *
 * Scoped rather than document-wide on purpose: a hand-authored document, or one round-tripped
 * through the DSL, is displayed as authored, and calling this at the document root would rewrite
 * all of it on any single edit. But scoping is only as narrow as the `path` a caller passes — pass
 * the narrowest path that could plausibly have gained nesting from the mutation, not a wider
 * ancestor "to be safe", or you flatten more than the mutation touched.
 */
export function normalizeAt(document: RuleDocument, path: string): RuleDocument {
  const node = getNode(document, path);
  if (!node) return document;
  return setNode(document, path, flatten(node));
}
