import { isNotNode, type Catalog, type RuleEditorStore, type RuleNode } from '@motiv/rules-core';

/** Wraps a node in `not`, or unwraps it if it's already negated. */
export function toggleNot(store: RuleEditorStore, path: string, node: RuleNode): void {
  if (isNotNode(node)) store.unwrap(path);
  else store.replaceNode(path, { not: node });
}

/** The five higher-order quantifier keys, in canonical order. */
export const KINDS = [
  'asAllSatisfied', 'asAnySatisfied', 'asNSatisfied',
  'asAtLeastNSatisfied', 'asAtMostNSatisfied',
] as const;
export type QuantifierKind = (typeof KINDS)[number];

/** The quantifier kinds that carry an `n` count. */
export const N_KINDS: readonly QuantifierKind[] = ['asNSatisfied', 'asAtLeastNSatisfied', 'asAtMostNSatisfied'];

/** Loosely-typed view of a higher-order quantifier node, for reading `path`/`n` and mutation helpers. */
export type QuantifierLike = Record<string, unknown> & { path: string; n?: number };

/** The quantifier kind key present on a higher-order node. */
export function quantifierKindOf(node: QuantifierLike): QuantifierKind {
  const kind = KINDS.find((candidate) => candidate in node);
  if (!kind) throw new Error('Node is not a higher-order quantifier node.');
  return kind;
}

/** The single child rule node wrapped by a higher-order quantifier node. */
export function quantifierChild(node: QuantifierLike): RuleNode {
  return node[quantifierKindOf(node)] as RuleNode;
}

/** Adds a new `asAllSatisfied` quantifier operand over the catalog's first registered collection. */
export function insertQuantifier(store: RuleEditorStore, operatorPath: string, catalog: Catalog): void {
  const col = catalog.collections[0];
  if (!col) return;
  const childSpec = catalog.specs.find((s) => s.modelType === col.elementModelType)?.name ?? 'spec';
  store.addOperand(operatorPath, { asAllSatisfied: { spec: childSpec }, path: col.path } as unknown as RuleNode);
}

/** Rebuilds a quantifier node under a new kind, preserving its child, collection path, and `n` where relevant. */
export function setQuantifierKind(store: RuleEditorStore, path: string, node: QuantifierLike, kind: QuantifierKind): void {
  const rebuilt: Record<string, unknown> = {
    [kind]: quantifierChild(node),
    path: node.path,
    ...(N_KINDS.includes(kind) ? { n: typeof node.n === 'number' ? node.n : 1 } : {}),
  };
  store.replaceNode(path, rebuilt as unknown as RuleNode);
}

/** Repoints a quantifier node at a different registered collection. */
export function setQuantifierCollection(store: RuleEditorStore, path: string, node: QuantifierLike, collectionPath: string): void {
  store.replaceNode(path, { ...node, path: collectionPath } as unknown as RuleNode);
}

/** Updates the `n` count on an N-kind quantifier node. */
export function setQuantifierN(store: RuleEditorStore, path: string, node: QuantifierLike, n: number): void {
  store.replaceNode(path, { ...node, n } as unknown as RuleNode);
}
