import { isSpecNode, nodeKind, type RuleDocument, type RuleNode } from '../document.js';
import { definitionNameOf, listPaths } from '../paths.js';

/** True when two nodes are the same kind and, for specs, the same spec — so payloads still apply. */
function isCompatible(a: RuleNode, b: RuleNode): boolean {
  if (nodeKind(a) !== nodeKind(b)) return false;
  if (isSpecNode(a) && isSpecNode(b)) return a.spec === b.spec;
  return true;
}

/**
 * Re-attaches `whenTrue`/`whenFalse` payloads from a prior document onto a freshly parsed
 * one, matching nodes by path. `name` is not carried over — it comes from the DSL text. A
 * payload is carried over only when the node at that path is compatible (same kind, and
 * same spec for spec nodes); otherwise it is dropped, so a structural edit never
 * mis-assigns a payload to an unrelated node. Neither input is mutated.
 */
export function mergeDecorations(parsed: RuleDocument, prior: RuleDocument): RuleDocument {
  const priorNodes = new Map(listPaths(prior).map(({ path, node }) => [path, node]));
  const merged = structuredClone(parsed);

  for (const { path, node } of listPaths(merged)) {
    const previous = priorNodes.get(path);
    if (!previous || !isCompatible(node, previous)) continue;

    if (previous.whenTrue !== undefined) node.whenTrue = structuredClone(previous.whenTrue);
    if (previous.whenFalse !== undefined) node.whenFalse = structuredClone(previous.whenFalse);

    // A definition's own whenTrue/whenFalse live on the Definition, not its body node, so they
    // are carried separately when the body root at this path is still compatible.
    const definitionName = definitionNameOf(path);
    if (definitionName && path.endsWith('.rule')) {
      const priorDefinition = prior.definitions?.[definitionName];
      const mergedDefinition = merged.definitions?.[definitionName];
      if (priorDefinition && mergedDefinition) {
        if (priorDefinition.whenTrue !== undefined) {
          mergedDefinition.whenTrue = structuredClone(priorDefinition.whenTrue);
        }
        if (priorDefinition.whenFalse !== undefined) {
          mergedDefinition.whenFalse = structuredClone(priorDefinition.whenFalse);
        }
      }
    }
  }

  return merged;
}
