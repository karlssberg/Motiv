import { getNode, type RuleDocument, type RuleNode } from '@motiv-rules/core';

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
export function catalogSeedFor(document: RuleDocument, path: string): RuleDocument | null {
  const node = getNode(document, path);
  // Copied, not shared: the seed outlives the dialog, and the document underneath it goes on
  // being edited while the create is in flight.
  return node === undefined ? null : { rule: structuredClone(node) };
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
    ...structuredClone(definition.rule),
    name,
    ...(definition.whenTrue !== undefined ? { whenTrue: definition.whenTrue } : {}),
    ...(definition.whenFalse !== undefined ? { whenFalse: definition.whenFalse } : {}),
  };
  return { rule };
}
