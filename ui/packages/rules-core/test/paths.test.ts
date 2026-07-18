import { describe, it, expect } from 'vitest';
import { getNode, setNode, listPaths } from '../src/paths.js';
import type { RuleDocument } from '../src/document.js';

const doc: RuleDocument = {
  rule: { and: [{ spec: 'a' }, { not: { spec: 'b' } }] },
};

describe('listPaths', () => {
  it('emits backend-shaped paths for every node, root first', () => {
    expect(listPaths(doc).map((p) => p.path)).toEqual([
      '$.rule',
      '$.rule.and[0]',
      '$.rule.and[1]',
      '$.rule.and[1].not',
    ]);
  });
});

describe('getNode', () => {
  it('resolves the root and nested nodes', () => {
    expect(getNode(doc, '$.rule')).toBe(doc.rule);
    expect(getNode(doc, '$.rule.and[0]')).toEqual({ spec: 'a' });
    expect(getNode(doc, '$.rule.and[1].not')).toEqual({ spec: 'b' });
  });
  it('returns undefined for a missing path', () => {
    expect(getNode(doc, '$.rule.and[9]')).toBeUndefined();
  });
});

describe('setNode', () => {
  it('replaces a nested node without mutating the original', () => {
    const next = setNode(doc, '$.rule.and[0]', { spec: 'z' });
    expect(getNode(next, '$.rule.and[0]')).toEqual({ spec: 'z' });
    expect(getNode(doc, '$.rule.and[0]')).toEqual({ spec: 'a' });
    expect(next).not.toBe(doc);
  });
  it('replaces the root node', () => {
    const next = setNode(doc, '$.rule', { spec: 'only' });
    expect(next.rule).toEqual({ spec: 'only' });
  });
});
