import { describe, it, expect } from 'vitest';
import { firstOperandTarget, insertTargetForRow, planInsert } from '../src/plan.js';
import type { RuleDocument } from '../src/document.js';

const doc = (rule: unknown): RuleDocument => ({ rule } as RuleDocument);
const NEW = { spec: 'new' };

describe('insertTargetForRow', () => {
  it('targets the slot after an operand row', () => {
    expect(insertTargetForRow('$.rule.and[1]')).toEqual({ kind: 'slot', parentPath: '$.rule', index: 2 });
  });

  it('wraps a row that is not an operand of a list', () => {
    expect(insertTargetForRow('$.rule')).toEqual({ kind: 'wrap', path: '$.rule' });
    expect(insertTargetForRow('$.rule.not')).toEqual({ kind: 'wrap', path: '$.rule.not' });
    expect(insertTargetForRow('$.rule.asAllSatisfied')).toEqual({ kind: 'wrap', path: '$.rule.asAllSatisfied' });
  });
});

describe('firstOperandTarget', () => {
  it('targets index 0 of the operator own list', () => {
    expect(firstOperandTarget('$.rule.or[2]')).toEqual({ kind: 'slot', parentPath: '$.rule.or[2]', index: 0 });
  });
});

describe('planInsert into a slot', () => {
  it('inserts at the index, shifting later operands right', () => {
    const result = planInsert(doc({ and: [{ spec: 'a' }, { spec: 'b' }] }), { kind: 'slot', parentPath: '$.rule', index: 1 }, NEW);
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, NEW, { spec: 'b' }] });
  });

  it('appends when the index equals the operand count', () => {
    const result = planInsert(doc({ and: [{ spec: 'a' }, { spec: 'b' }] }), { kind: 'slot', parentPath: '$.rule', index: 2 }, NEW);
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, NEW] });
  });

  it('inserts at index 0', () => {
    const result = planInsert(doc({ or: [{ spec: 'a' }, { spec: 'b' }] }), { kind: 'slot', parentPath: '$.rule', index: 0 }, NEW);
    expect(result.rule).toEqual({ or: [NEW, { spec: 'a' }, { spec: 'b' }] });
  });

  it('preserves the parent decoration', () => {
    const result = planInsert(doc({ and: [{ spec: 'a' }, { spec: 'b' }], name: 'outer' }), { kind: 'slot', parentPath: '$.rule', index: 0 }, NEW);
    expect(result.rule).toEqual({ and: [NEW, { spec: 'a' }, { spec: 'b' }], name: 'outer' });
  });

  it('throws when the parent is not an operator node', () => {
    expect(() => planInsert(doc({ spec: 'a' }), { kind: 'slot', parentPath: '$.rule', index: 0 }, NEW))
      .toThrow(/not an operator node/);
  });
});

describe('planInsert wrapping', () => {
  it('wraps a leaf in and', () => {
    expect(planInsert(doc({ spec: 'a' }), { kind: 'wrap', path: '$.rule' }, NEW).rule)
      .toEqual({ and: [{ spec: 'a' }, NEW] });
  });

  it('flattens into an existing undecorated and, so a wrap becomes an append', () => {
    expect(planInsert(doc({ and: [{ spec: 'a' }, { spec: 'b' }] }), { kind: 'wrap', path: '$.rule' }, NEW).rule)
      .toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, NEW] });
  });

  it('stays a genuine wrap when the wrapped node carries a name', () => {
    const named = { and: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' };
    expect(planInsert(doc(named), { kind: 'wrap', path: '$.rule' }, NEW).rule)
      .toEqual({ and: [named, NEW] });
  });

  it('wraps a quantifier body without disturbing the quantifier', () => {
    const result = planInsert(
      doc({ asAllSatisfied: { spec: 'a' }, path: '$.orders' }),
      { kind: 'wrap', path: '$.rule.asAllSatisfied' },
      NEW,
    );
    expect(result.rule).toEqual({ asAllSatisfied: { and: [{ spec: 'a' }, NEW] }, path: '$.orders' });
  });

  it('throws when a wrap target path does not resolve', () => {
    expect(() => planInsert(doc({ spec: 'a' }), { kind: 'wrap', path: '$.rule.and[3]' }, NEW))
      .toThrow(/No node at/);
  });
});

describe('planInsert purity', () => {
  it('leaves the input document untouched when inserting into a slot', () => {
    const document = doc({ and: [{ spec: 'a' }, { spec: 'b' }] });
    const before = structuredClone(document);

    planInsert(document, { kind: 'slot', parentPath: '$.rule', index: 1 }, NEW);

    expect(document).toEqual(before);
  });

  it('leaves the input document untouched when wrapping', () => {
    const document = doc({ and: [{ spec: 'a' }, { spec: 'b' }] });
    const before = structuredClone(document);

    planInsert(document, { kind: 'wrap', path: '$.rule' }, NEW);

    expect(document).toEqual(before);
  });

  // The inserted node is embedded by reference, matching `RuleEditorStore.wrapInOperator`:
  // `setNode` clones the document it is handed but not the replacement written into it. Pinned
  // here so that changing it is a decision rather than an accident. Harmless in practice — the
  // only caller parses a fresh node immediately before inserting it and never retains it — and
  // deep-cloning here would diverge from every other mutation in this package.
  it('embeds the inserted node by reference, as the package idiom does', () => {
    const inserted = { spec: 'new' };

    const result = planInsert(doc({ and: [{ spec: 'a' }, { spec: 'b' }] }), { kind: 'slot', parentPath: '$.rule', index: 0 }, inserted);

    expect((result.rule as { and: unknown[] }).and[0]).toBe(inserted);
  });
});
