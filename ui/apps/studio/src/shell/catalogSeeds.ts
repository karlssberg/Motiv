import {
  binaryOperator, getNode, higherOrderBody, higherOrderKey, isBinaryNode, isHigherOrderNode,
  isLocalNode, isNotNode, operandsOf,
  type Decoration, type RuleDocument, type RuleNode,
} from '@motiv-rules/core';

/**
 * The documents the two widening flows — *Extract to catalog* from a builder row, *Promote to
 * catalog* from a definition — hand the create dialog (#234).
 *
 * Both answer a whole `RuleDocument` rather than a source to start from, because what is being
 * created already exists: it is the subtree, or the definition body, standing where it stands.
 * Pure, and separate from the shell that wires them, so what a create posts is stated once and
 * checked without a dialog around it.
 *
 * Each names the new proposition on the **root node** of `rule` rather than on the document
 * envelope: the envelope's `name` names the rule the document *is*, and a proposition's name is
 * the one it is registered under — which the create request carries beside the document.
 */

/**
 * What extracting the node at `path` should create: that subtree, and nothing of the document
 * around it. The node's own `name` and decoration ride along on the root, so what the catalog
 * gets says exactly what the row said. `null` when no node stands at the path.
 */

/**
 * The same node with every `{ local: n }` reference replaced by the definition's body — carrying
 * the definition's `whenTrue`/`whenFalse` and its key as `name`, exactly the shape
 * `RuleEditorStore.inlineLocal` produces.
 *
 * A seed is a document of its own: it has no `definitions`, so a `local` left standing in it names
 * nothing the server could resolve and the create is refused with `UnknownLocal`. Inlining is what
 * makes the extracted subtree mean, on its own, what it meant where it stood.
 *
 * An inlined body may itself reference another definition, so the result is walked again.
 * `inlining` is the chain of names currently being resolved: a document the store accepts cannot
 * contain a cycle, so a repeat is a corrupt document rather than a case to render, and throwing
 * beats recursing forever.
 */
export function inlineLocalsIn(
  node: RuleNode,
  definitions: RuleDocument['definitions'],
  inlining: readonly string[] = [],
): RuleNode {
  if (isLocalNode(node)) {
    const name = node.local;
    if (inlining.includes(name)) {
      throw new Error(`Definitions form a cycle: ${[...inlining, name].join(' → ')}.`);
    }
    const definition = definitions?.[name];
    // Nothing better to offer than the reference itself: an undefined local is a broken document,
    // and the server's own `UnknownLocal` says so more usefully than a silent omission would.
    if (definition === undefined) return node;
    const inlined = { ...structuredClone(definition.rule), name } as RuleNode & Decoration;
    if (definition.whenTrue !== undefined) inlined.whenTrue = structuredClone(definition.whenTrue);
    if (definition.whenFalse !== undefined) inlined.whenFalse = structuredClone(definition.whenFalse);
    return inlineLocalsIn(inlined, definitions, [...inlining, name]);
  }
  if (isNotNode(node)) return { ...node, not: inlineLocalsIn(node.not, definitions, inlining) };
  if (isBinaryNode(node)) {
    const operator = binaryOperator(node);
    return {
      ...node,
      [operator]: operandsOf(node).map((operand) => inlineLocalsIn(operand, definitions, inlining)),
    } as RuleNode;
  }
  if (isHigherOrderNode(node)) {
    return {
      ...node,
      [higherOrderKey(node)]: inlineLocalsIn(higherOrderBody(node), definitions, inlining),
    } as RuleNode;
  }
  return node;
}

export function catalogSeedFor(document: RuleDocument, path: string): RuleDocument | null {
  const node = getNode(document, path);
  if (node === undefined) return null;
  // Copied, not shared: the seed outlives the dialog, and the document underneath it goes on
  // being edited while the create is in flight.
  return { rule: inlineLocalsIn(structuredClone(node), document.definitions) };
}

/**
 * What promoting the definition `name` should create: its body, carrying the definition's key as
 * the root node's name and its payloads as that node's decoration — the document format keeps
 * decoration on nodes, so a definition promoted is a decorated node. `null` when no definition
 * stands under the name.
 *
 * A payload the definition never had is left out rather than written as `undefined`, which would
 * otherwise be posted as a `null` key the server has no reading for.
 */
export function promotionSeedFor(document: RuleDocument, name: string): RuleDocument | null {
  const definition = document.definitions?.[name];
  if (definition === undefined) return null;
  const rule: RuleNode = {
    ...inlineLocalsIn(structuredClone(definition.rule), document.definitions, [name]),
    name,
    // Cloned like the body: spread by reference, a payload edited in the definition afterwards
    // would change what the in-flight create posts.
    ...(definition.whenTrue !== undefined ? { whenTrue: structuredClone(definition.whenTrue) } : {}),
    ...(definition.whenFalse !== undefined ? { whenFalse: structuredClone(definition.whenFalse) } : {}),
  };
  return { rule };
}
