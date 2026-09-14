import { describe, it, expect } from 'vitest';
import type { RuleDocument } from '@motiv-rules/core';
import { catalogSeedFor, promotionSeedFor } from '../../src/shell/catalogSeeds.js';

const DOCUMENT: RuleDocument = {
  name: 'customer.can-checkout',
  rule: {
    and: [
      { local: 'activity' },
      { spec: 'customer.is-adult', name: 'adulthood', whenTrue: 'old enough' },
    ],
  },
  definitions: {
    activity: { rule: { spec: 'customer.is-active' }, whenTrue: 'active', whenFalse: 'dormant' },
    plain: { rule: { not: { spec: 'customer.is-banned' } } },
  },
};

describe('catalogSeedFor', () => {
  it('seeds the create with the subtree, keeping its name and decoration on the root', () => {
    expect(catalogSeedFor(DOCUMENT, '$.rule.and[1]')).toEqual({
      rule: { spec: 'customer.is-adult', name: 'adulthood', whenTrue: 'old enough' },
    });
  });

  it('carries nothing from the document around the node — not its name, nor its definitions', () => {
    const seed = catalogSeedFor(DOCUMENT, '$.rule')!;
    expect(seed.name).toBeUndefined();
    expect(seed.definitions).toBeUndefined();
  });

  it('copies rather than shares, so an edit to the seed cannot reach the document', () => {
    const seed = catalogSeedFor(DOCUMENT, '$.rule.and[1]')!;
    expect(seed.rule).not.toBe((DOCUMENT.rule as { and: unknown[] }).and[1]);
  });

  it('answers nothing for a path no node stands at', () => {
    expect(catalogSeedFor(DOCUMENT, '$.rule.and[7]')).toBeNull();
  });
});

describe('promotionSeedFor', () => {
  it('seeds the create with the definition’s body, its key and payloads on the root node', () => {
    expect(promotionSeedFor(DOCUMENT, 'activity')).toEqual({
      rule: { spec: 'customer.is-active', name: 'activity', whenTrue: 'active', whenFalse: 'dormant' },
    });
  });

  it('leaves out the payloads a definition never carried, rather than writing them undefined', () => {
    const seed = promotionSeedFor(DOCUMENT, 'plain')!;
    expect(seed).toEqual({ rule: { not: { spec: 'customer.is-banned' }, name: 'plain' } });
    expect(Object.keys(seed.rule)).toEqual(['not', 'name']);
  });

  it('answers nothing for a name no definition stands under', () => {
    expect(promotionSeedFor(DOCUMENT, 'absent')).toBeNull();
  });
});

/** A document whose definitions reference one another, and whose payloads are objects. */
const NESTED: RuleDocument = {
  rule: { and: [{ spec: 'a' }, { local: 'myLocal' }] },
  definitions: {
    myLocal: { rule: { and: [{ spec: 'b' }, { local: 'inner' }] }, whenTrue: { code: 'T' } },
    inner: { rule: { spec: 'c' }, whenFalse: { code: 'F' } },
  },
};

describe('seeds resolve the locals they carry', () => {
  it('posts no local node when the extracted subtree references one', () => {
    const seed = catalogSeedFor(NESTED, '$.rule')!;
    expect(JSON.stringify(seed)).not.toContain('"local"');
    expect(seed.rule).toEqual({
      and: [
        { spec: 'a' },
        {
          and: [{ spec: 'b' }, { spec: 'c', name: 'inner', whenFalse: { code: 'F' } }],
          name: 'myLocal',
          whenTrue: { code: 'T' },
        },
      ],
    });
  });

  it('inlines a definition body that references another definition', () => {
    const seed = promotionSeedFor(NESTED, 'myLocal')!;
    expect(JSON.stringify(seed)).not.toContain('"local"');
    expect(seed.rule).toEqual({
      and: [{ spec: 'b' }, { spec: 'c', name: 'inner', whenFalse: { code: 'F' } }],
      name: 'myLocal',
      whenTrue: { code: 'T' },
    });
  });

  it('copies the payloads it carries down rather than sharing the document’s objects', () => {
    const seed = promotionSeedFor(NESTED, 'myLocal')!;
    const payload = (seed.rule as { whenTrue: unknown }).whenTrue;
    expect(payload).toEqual({ code: 'T' });
    expect(payload).not.toBe(NESTED.definitions!.myLocal!.whenTrue);

    const inner = (seed.rule as { and: Array<{ whenFalse?: unknown }> }).and[1]!;
    expect(inner.whenFalse).not.toBe(NESTED.definitions!.inner!.whenFalse);
  });
});
