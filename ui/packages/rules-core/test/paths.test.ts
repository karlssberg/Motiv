import { describe, it, expect } from 'vitest';
import {
  childPaths, getNode, isNodePath, setNode, listPaths, localReferences,
  DEFINITIONS_ROOT, definitionPath, definitionBodyPath, definitionNameOf,
} from '../src/paths.js';
import type { RuleDocument } from '../src/document.js';

const doc: RuleDocument = {
  rule: { and: [{ spec: 'a' }, { not: { spec: 'b' } }] },
};

describe('forbidden keys', () => {
  it('rejects prototype-polluting path keys in getNode and setNode', () => {
    for (const key of ['__proto__', 'constructor', 'prototype']) {
      expect(() => getNode(doc, `$.rule.${key}`)).toThrow(/[Ff]orbidden/);
      expect(() => setNode(doc, `$.rule.${key}`, { spec: 'x' })).toThrow(/[Ff]orbidden/);
    }
    // Object.prototype was not polluted.
    expect(({} as Record<string, unknown>)['polluted']).toBeUndefined();
  });
});

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

describe('definition paths', () => {
  const docWithDefinition: RuleDocument = {
    rule: { spec: 'a' },
    definitions: {
      'quota-check': { rule: { and: [{ spec: 'x' }, { spec: 'y' }] } },
    },
  };

  it('builds a definition path and its body path', () => {
    expect(definitionPath('quota-check')).toBe(`${DEFINITIONS_ROOT}.quota-check`);
    expect(definitionBodyPath('quota-check')).toBe(`${DEFINITIONS_ROOT}.quota-check.rule`);
  });

  it('getNode resolves nested nodes inside a definition body', () => {
    expect(getNode(docWithDefinition, '$.definitions.quota-check.rule.and[1]')).toEqual({ spec: 'y' });
  });

  it('setNode inside a definition body returns a new document with the rule untouched', () => {
    const next = setNode(docWithDefinition, '$.definitions.quota-check.rule.and[1]', { spec: 'z' });
    expect(getNode(next, '$.definitions.quota-check.rule.and[1]')).toEqual({ spec: 'z' });
    expect(getNode(docWithDefinition, '$.definitions.quota-check.rule.and[1]')).toEqual({ spec: 'y' });
    expect(docWithDefinition.rule).toEqual({ spec: 'a' });
    expect(next.rule).toEqual({ spec: 'a' });
  });

  it('listPaths includes every definition body after the rule', () => {
    expect(listPaths(docWithDefinition).map((p) => p.path)).toEqual([
      '$.rule',
      '$.definitions.quota-check.rule',
      '$.definitions.quota-check.rule.and[0]',
      '$.definitions.quota-check.rule.and[1]',
    ]);
  });

  it('rejects a definitions path with an extra segment before .rule', () => {
    expect(() => getNode(docWithDefinition, '$.definitions.a.b.rule')).toThrow();
  });

  it('rejects a definitions path with an empty name', () => {
    expect(() => getNode(docWithDefinition, '$.definitions..rule')).toThrow();
  });

  it('rejects a definition path without .rule — a definition is not a node', () => {
    expect(() => getNode(docWithDefinition, '$.definitions.quota-check')).toThrow();
  });

  it('definitionNameOf extracts the name under a definition path', () => {
    expect(definitionNameOf('$.definitions.quota-check.rule.and[0]')).toBe('quota-check');
    expect(definitionNameOf('$.definitions.quota-check')).toBe('quota-check');
  });

  it('definitionNameOf is undefined for a plain rule path', () => {
    expect(definitionNameOf('$.rule')).toBeUndefined();
    expect(definitionNameOf('$.rule.and[0]')).toBeUndefined();
  });
});

describe('childPaths', () => {
  it('lists binary operands under their operator key, in order', () => {
    expect(childPaths({ and: [{ spec: 'a' }, { spec: 'b' }] }, '$.rule'))
      .toEqual(['$.rule.and[0]', '$.rule.and[1]']);
  });

  it('lists the single child of not and of a higher-order node', () => {
    expect(childPaths({ not: { spec: 'a' } }, '$.rule')).toEqual(['$.rule.not']);
    expect(childPaths({ asAllSatisfied: { spec: 'a' }, path: '$.orders' }, '$.rule.and[1]'))
      .toEqual(['$.rule.and[1].asAllSatisfied']);
  });

  it('gives a leaf no children', () => {
    expect(childPaths({ spec: 'a' }, '$.rule')).toEqual([]);
    expect(childPaths({ expression: 'n > 0' }, '$.rule')).toEqual([]);
  });
});

describe('localReferences', () => {
  it('finds references in the rule and in definitions, rule first then key order', () => {
    const document: RuleDocument = {
      rule: { and: [{ local: 'quota-check' }, { spec: 'b' }] },
      definitions: {
        'quota-check': { rule: { spec: 'a' } },
        other: { rule: { local: 'quota-check' } },
      },
    };
    expect(localReferences(document, 'quota-check')).toEqual([
      '$.rule.and[0]',
      '$.definitions.other.rule',
    ]);
  });

  it('returns an empty array when there are no references', () => {
    expect(localReferences({ rule: { spec: 'a' } }, 'quota-check')).toEqual([]);
  });
});

describe('isNodePath', () => {
  it('accepts the paths a node can actually stand at', () => {
    for (const path of ['$.rule', '$.rule.and[0]', '$.definitions.a.rule', '$.definitions.a.rule.not']) {
      expect(isNodePath(path)).toBe(true);
    }
  });

  it('rejects a definition itself, its non-rule fields and anything outside the two roots', () => {
    // `$.definitions.a` is where a `let` declaration's span sits: real, addressable, and not a
    // node — so every consumer that resolves a span to a node has to be able to ask.
    for (const path of ['$.definitions.a', '$.definitions.a.whenTrue', '$.x', '$.definitions', '']) {
      expect(isNodePath(path)).toBe(false);
    }
  });

  it('agrees with getNode, which throws for exactly the paths it rejects', () => {
    const document = { rule: { spec: 'a' }, definitions: { a: { rule: { spec: 'b' } } } };
    expect(() => getNode(document, '$.definitions.a')).toThrow();
    expect(isNodePath('$.definitions.a')).toBe(false);
    expect(getNode(document, '$.definitions.a.rule')).toEqual({ spec: 'b' });
    expect(isNodePath('$.definitions.a.rule')).toBe(true);
  });
});
