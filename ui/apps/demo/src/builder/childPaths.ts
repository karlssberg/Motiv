import {
  binaryOperator, higherOrderKey, isBinaryNode, isHigherOrderNode, isNotNode,
  operandsOf, type RuleNode,
} from '@motiv/rules-core';

/** The child node paths of a rule node, in the same order the document walks them. */
export function childPaths(node: RuleNode, path: string): string[] {
  if (isNotNode(node)) return [`${path}.not`];
  if (isBinaryNode(node)) {
    const op = binaryOperator(node);
    return operandsOf(node).map((_, i) => `${path}.${op}[${i}]`);
  }
  if (isHigherOrderNode(node)) return [`${path}.${higherOrderKey(node)}`];
  return [];
}
