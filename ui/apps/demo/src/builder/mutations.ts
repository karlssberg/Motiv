import { isNotNode, type RuleEditorStore, type RuleNode } from '@motiv/rules-core';

/** Wraps a node in `not`, or unwraps it if it's already negated. */
export function toggleNot(store: RuleEditorStore, path: string, node: RuleNode): void {
  if (isNotNode(node)) store.unwrap(path);
  else store.replaceNode(path, { not: node });
}
