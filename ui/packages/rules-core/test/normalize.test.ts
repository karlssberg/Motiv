import { describe, it, expect } from 'vitest';
import { normalizeAt } from '../src/normalize.js';
import type { RuleDocument } from '../src/document.js';

const doc = (rule: unknown): RuleDocument => ({ rule } as RuleDocument);

describe('normalizeAt', () => {
  it('flattens a left-nested same-operator child', () => {
    const result = normalizeAt(doc({ and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }), '$.rule');
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });
  });

  it('flattens a right-nested same-operator child', () => {
    const result = normalizeAt(doc({ and: [{ spec: 'a' }, { and: [{ spec: 'b' }, { spec: 'c' }] }] }), '$.rule');
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });
  });

  it('flattens recursively, so nesting of any depth collapses in one pass', () => {
    const result = normalizeAt(
      doc({ and: [{ and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }, { spec: 'd' }] }),
      '$.rule',
    );
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }, { spec: 'd' }] });
  });

  it('refuses to dissolve a child carrying a name', () => {
    const rule = { and: [{ and: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('refuses to dissolve a child carrying whenTrue', () => {
    const rule = { and: [{ and: [{ spec: 'a' }, { spec: 'b' }], whenTrue: 'both' }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('refuses to dissolve a child carrying whenFalse', () => {
    const rule = { and: [{ and: [{ spec: 'a' }, { spec: 'b' }], whenFalse: 'neither' }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('never flattens xor, because n-ary xor is parity rather than one-of', () => {
    const rule = { xor: [{ xor: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('never merges and with andAlso', () => {
    const rule = { and: [{ andAlso: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('never merges or with orElse', () => {
    const rule = { or: [{ orElse: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('flattens andAlso into andAlso and orElse into orElse', () => {
    const result = normalizeAt(doc({ andAlso: [{ andAlso: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }), '$.rule');
    expect(result.rule).toEqual({ andAlso: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });
  });

  it('descends through a not body', () => {
    const result = normalizeAt(doc({ not: { and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } }), '$.rule');
    expect(result.rule).toEqual({ not: { and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] } });
  });

  it('descends through a higher-order quantifier body', () => {
    const result = normalizeAt(
      doc({ asAllSatisfied: { and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }, path: '$.orders' }),
      '$.rule',
    );
    expect(result.rule).toEqual({
      asAllSatisfied: { and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] },
      path: '$.orders',
    });
  });

  it('normalizes only the subtree at the given path, leaving siblings untouched', () => {
    const untouched = { and: [{ and: [{ spec: 'x' }, { spec: 'y' }] }, { spec: 'z' }] };
    const result = normalizeAt(
      doc({ or: [{ and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }, untouched] }),
      '$.rule.or[0]',
    );
    expect(result.rule).toEqual({
      or: [{ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] }, untouched],
    });
  });

  it('preserves the parent node own decoration while flattening its children', () => {
    const result = normalizeAt(
      doc({ and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }], name: 'outer' }),
      '$.rule',
    );
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }], name: 'outer' });
  });

  it('leaves a leaf untouched', () => {
    expect(normalizeAt(doc({ spec: 'a' }), '$.rule').rule).toEqual({ spec: 'a' });
  });

  it('returns the document unchanged for a path that does not resolve', () => {
    const document = doc({ spec: 'a' });
    expect(normalizeAt(document, '$.rule.and[3]').rule).toEqual({ spec: 'a' });
  });

  // Pinned deliberately, not as an oversight: `normalizeAt` flattens the *whole* subtree rooted
  // at `path`, including branches a caller's own mutation never touched, because a sibling
  // operand shares the same ancestor as whatever did change. `planInsert` calls this with the
  // mutation's immediate parent path, so a `+` on any root-level row normalizes the entire
  // document, not just the vicinity of the insertion — see the "When it runs" section of
  // `docs/superpowers/specs/2026-07-27-builder-node-insertion-design.md`. This is accepted
  // (flattening is semantics-preserving, and the strip previews it before commit) rather than
  // narrowed. Narrowing `normalizeAt` to only the touched region is a future decision, not an
  // accident this test should let slip through unnoticed — if this test starts failing because
  // scope narrowed, that is the signal to update the design doc alongside it.
  it('flattens an untouched sibling subtree too, because scope is the whole subtree at path, not just what changed', () => {
    const untouchedButFlattened = { and: [{ and: [{ spec: 'x' }, { spec: 'y' }] }, { spec: 'z' }] };
    const result = normalizeAt(
      doc({ or: [{ spec: 'changed' }, untouchedButFlattened] }),
      '$.rule',
    );
    expect(result.rule).toEqual({
      or: [{ spec: 'changed' }, { and: [{ spec: 'x' }, { spec: 'y' }, { spec: 'z' }] }],
    });
  });
});
