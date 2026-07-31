import { describe, it, expect } from 'vitest';
import { buildNamespaceTree, filterTree, countLeaves } from '../src/namespaceTree.js';
import type { NamespaceNode } from '../src/namespaceTree.js';
import type { PropositionListEntry } from '../src/contracts.js';

/** Collects the `path` of every node that carries a proposition entry. */
function entryPaths(nodes: NamespaceNode[]): string[] {
  return nodes.flatMap((node) => [
    ...(node.entry ? [node.path] : []),
    ...entryPaths(node.children),
  ]);
}

function entry(name: string, modelType = 'customer'): PropositionListEntry {
  return {
    name, modelType, metadataType: 'String', isAsync: false,
    origin: 'Authored', version: 1, description: null, quarantine: [],
  };
}

describe('buildNamespaceTree', () => {
  it('puts an undotted name at the root', () => {
    const tree = buildNamespaceTree([entry('is-active')]);

    expect(tree).toHaveLength(1);
    expect(tree[0]!.segment).toBe('is-active');
    expect(tree[0]!.path).toBe('is-active');
    expect(tree[0]!.entry?.name).toBe('is-active');
    expect(tree[0]!.children).toEqual([]);
  });

  it('nests a dotted name under its namespace', () => {
    const tree = buildNamespaceTree([entry('customer.eligibility.is-active')]);

    expect(tree.map((node) => node.segment)).toEqual(['customer']);
    expect(tree[0]!.path).toBe('customer');
    expect(tree[0]!.entry).toBeUndefined();
    const eligibility = tree[0]!.children[0]!;
    expect(eligibility.segment).toBe('eligibility');
    expect(eligibility.path).toBe('customer.eligibility');
    expect(eligibility.children[0]!.path).toBe('customer.eligibility.is-active');
  });

  it('shares a namespace between siblings', () => {
    const tree = buildNamespaceTree([entry('customer.is-active'), entry('customer.is-adult')]);

    expect(tree).toHaveLength(1);
    expect(tree[0]!.children.map((node) => node.segment)).toEqual(['is-active', 'is-adult']);
  });

  it('sorts namespaces before leaves, each alphabetically', () => {
    const tree = buildNamespaceTree([
      entry('customer.zeta'),
      entry('customer.alpha'),
      entry('customer.nested.thing'),
    ]);

    expect(tree[0]!.children.map((node) => node.segment)).toEqual(['nested', 'alpha', 'zeta']);
  });

  it('lets a namespace also be a proposition in its own right', () => {
    // `customer` is both a name and a namespace — the tree must carry the entry and the children
    const tree = buildNamespaceTree([entry('customer'), entry('customer.is-active')]);

    expect(tree).toHaveLength(1);
    expect(tree[0]!.entry?.name).toBe('customer');
    expect(tree[0]!.children).toHaveLength(1);
  });

  it('returns nothing for no entries', () => {
    expect(buildNamespaceTree([])).toEqual([]);
  });

  it('gives a leading-dot name a path distinct from a same-tailed root name', () => {
    // A leading empty segment (from a quarantined, hand-edited `.foo` name) must not collapse
    // onto the unrelated root-level `foo` — each entry's path must uniquely identify it.
    const tree = buildNamespaceTree([entry('.foo'), entry('foo')]);

    const paths = entryPaths(tree);
    expect(paths).toHaveLength(2);
    expect(new Set(paths).size).toBe(2);
  });
});

describe('filterTree', () => {
  const tree = buildNamespaceTree([
    entry('customer.eligibility.is-active'),
    entry('customer.eligibility.is-adult'),
    entry('customer.risk.is-fraudulent'),
    entry('order.is-large', 'order'),
  ]);

  it('returns the whole tree for an empty query', () => {
    expect(countLeaves(filterTree(tree, ''))).toBe(4);
  });

  it('keeps only matching leaves, with their ancestors', () => {
    const filtered = filterTree(tree, 'fraud');

    expect(countLeaves(filtered)).toBe(1);
    expect(filtered[0]!.segment).toBe('customer');
    expect(filtered[0]!.children[0]!.segment).toBe('risk');
    expect(filtered[0]!.children[0]!.children[0]!.path).toBe('customer.risk.is-fraudulent');
  });

  it('matches against the full dotted path, not just the leaf segment', () => {
    // Searching by namespace is the main way a big catalog gets navigated
    expect(countLeaves(filterTree(tree, 'eligibility'))).toBe(2);
  });

  it('matches case-insensitively', () => {
    expect(countLeaves(filterTree(tree, 'FRAUD'))).toBe(1);
  });

  it('returns nothing when nothing matches', () => {
    expect(filterTree(tree, 'nonexistent')).toEqual([]);
  });

  it('filters by model type', () => {
    const filtered = filterTree(tree, '', ['order']);

    expect(countLeaves(filtered)).toBe(1);
    expect(filtered[0]!.children[0]!.path).toBe('order.is-large');
  });

  it('combines a query with a model filter', () => {
    expect(countLeaves(filterTree(tree, 'is-', ['order']))).toBe(1);
  });

  it('treats an empty model list as no model filter', () => {
    expect(countLeaves(filterTree(tree, '', []))).toBe(4);
  });

  it('drops a namespace whose only matching descendant was filtered out by model', () => {
    expect(filterTree(tree, 'fraud', ['order'])).toEqual([]);
  });
});
