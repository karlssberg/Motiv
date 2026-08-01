import type { PropositionListEntry } from './contracts.js';

/**
 * A node in the namespace tree. The tree is a pure projection of the dotted names — there is no
 * stored hierarchy to keep in sync, so a rename is a move and nothing else has to know.
 */
export interface NamespaceNode {
  /** This node's own name segment. */
  segment: string;
  /** The dotted path from the root to here. */
  path: string;
  children: NamespaceNode[];
  /**
   * The proposition living at exactly this path, when there is one. A node can have both an entry
   * and children: `customer` may be a proposition *and* a namespace.
   */
  entry?: PropositionListEntry;
}

/** Builds the namespace tree from a flat listing, sorting namespaces before leaves. */
export function buildNamespaceTree(entries: PropositionListEntry[]): NamespaceNode[] {
  const roots: NamespaceNode[] = [];

  for (const entry of entries) {
    const segments = entry.name.split('.');
    let siblings = roots;

    for (const [index, segment] of segments.entries()) {
      // Built from the segments directly rather than accumulated onto a `''`-means-"nothing-yet"
      // sentinel — that sentinel is indistinguishable from a genuinely empty leading segment (a
      // name like `.foo`, reachable via a quarantined hand-edited store), which would otherwise
      // collide with an unrelated root-level `foo`.
      const path = segments.slice(0, index + 1).join('.');
      let node = siblings.find((candidate) => candidate.segment === segment);
      if (!node) {
        node = { segment, path, children: [] };
        siblings.push(node);
      }
      // Last entry for a given name wins if the listing ever contains duplicates.
      if (index === segments.length - 1) node.entry = entry;
      siblings = node.children;
    }
  }

  return sort(roots);
}

/**
 * Narrows the tree to nodes matching `query` (substring of the full dotted path, case-insensitive)
 * and, when `models` is non-empty, to leaves of those model types. A matching leaf keeps its
 * ancestors so its position stays legible; a namespace with no surviving descendant is dropped.
 *
 * When there is no filter to apply (empty `query` and no `models`), this returns the input array
 * *by reference* rather than a copy — every other path allocates fresh nodes. A caller that holds
 * onto a previously filtered tree across renders should be aware `filterTree(tree, '') === tree`
 * while any other call produces a new structure.
 */
export function filterTree(
  nodes: NamespaceNode[], query: string, models: string[] = [],
): NamespaceNode[] {
  const needle = query.trim().toLowerCase();
  if (needle === '' && models.length === 0) return nodes;

  return keepMatching(nodes, needle, models);
}

/**
 * The walk itself, over an already-normalized needle. Split from `filterTree` so the query is
 * trimmed and lower-cased once for the whole tree rather than once per node, and so the
 * return-by-reference shortcut stays a property of the entry point alone.
 */
function keepMatching(
  nodes: NamespaceNode[], needle: string, models: string[],
): NamespaceNode[] {
  const kept: NamespaceNode[] = [];

  for (const node of nodes) {
    const children = keepMatching(node.children, needle, models);
    const selfMatches = node.entry !== undefined
      && node.path.toLowerCase().includes(needle)
      && (models.length === 0 || models.includes(node.entry.modelType));

    // A namespace survives only for the sake of a descendant that matched.
    if (!selfMatches && children.length === 0) continue;

    kept.push({
      segment: node.segment,
      path: node.path,
      children,
      ...(selfMatches && node.entry ? { entry: node.entry } : {}),
    });
  }

  return kept;
}

/** How many propositions the tree holds — what a "N matches" count reports. */
export function countLeaves(nodes: NamespaceNode[]): number {
  return nodes.reduce(
    (total, node) => total + (node.entry ? 1 : 0) + countLeaves(node.children),
    0,
  );
}

/** Namespaces first, then leaves, each group alphabetical — depth reads before detail. */
function sort(nodes: NamespaceNode[]): NamespaceNode[] {
  for (const node of nodes) sort(node.children);
  nodes.sort((left, right) => {
    const leftIsNamespace = left.children.length > 0;
    const rightIsNamespace = right.children.length > 0;
    if (leftIsNamespace !== rightIsNamespace) return leftIsNamespace ? -1 : 1;
    return left.segment.localeCompare(right.segment);
  });
  return nodes;
}
