import { summarize, type RuleNode } from '@motiv-rules/core';

/**
 * The name to offer for a node about to become a definition, before it is run through
 * `finishLocalName` (#234).
 *
 * A node's own `name` is always the better answer and is the caller's first choice; this is what
 * is left when there is none. The summary's description reads well for a composition (`both must
 * hold`), and is empty for a leaf — whose badge is the spec's own name, which is the best seed of
 * all. An empty result is left empty rather than invented: the prompt refuses to create an unnamed
 * definition, and a generated `local-3` would only look like a name the author had chosen.
 */
export function extractionSeed(node: RuleNode): string {
  const summary = summarize(node);
  return summary.description || summary.badge;
}
