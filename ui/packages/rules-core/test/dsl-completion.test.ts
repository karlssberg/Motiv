import { describe, it, expect } from 'vitest';
import { completeDsl } from '../src/dsl/completion.js';
import type { Catalog, CatalogEntry } from '../src/contracts.js';

function spec(name: string, overrides: Partial<CatalogEntry> = {}): CatalogEntry {
  return {
    name, modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Compiled', parameters: null, ...overrides,
  };
}

const catalog: Catalog = {
  specs: [
    spec('is-active', { description: 'account is active' }),
    spec('orders.has-recent', { isAsync: true }),
  ],
  collections: [{ path: '$.orders', parentModelType: 'customer', elementModelType: 'Order' }],
};

/** Completion at the end of `text`. */
function completeAtEnd(text: string) {
  return completeDsl(text, text.length, catalog);
}

describe('completeDsl', () => {
  it('offers specs, collections and the fixed vocabulary for a word prefix', () => {
    const result = completeAtEnd('is');
    expect(result).not.toBeNull();
    expect(result!.from).toBe(0);
    expect(result!.options.map((option) => option.label)).toEqual(['is-active']);
    expect(result!.options[0]).toMatchObject({ kind: 'spec', boost: 1, detail: 'account is active' });
  });

  it('marks an async spec in its detail', () => {
    const result = completeAtEnd('orders.h');
    expect(result!.options).toEqual([
      { label: 'orders.has-recent', kind: 'spec', detail: 'async', boost: 1 },
    ]);
  });

  it('completes past a namespace dot, because spec words admit dots', () => {
    const result = completeAtEnd('orders.');
    expect(result!.from).toBe(0);
    expect(result!.options.map((option) => option.label)).toContain('orders.has-recent');
  });

  it('offers quantifiers, keywords and types from the single vocabulary definition', () => {
    const all = completeAtEnd('a');
    const labels = all!.options.map((option) => option.label);
    expect(labels).toEqual(expect.arrayContaining(['all', 'any', 'atLeast', 'atMost', 'as']));
    expect(all!.options.find((option) => option.label === 'all')).toMatchObject({ kind: 'quantifier', detail: 'quantifier' });
    expect(all!.options.find((option) => option.label === 'as')).toMatchObject({ kind: 'keyword', detail: 'keyword' });

    const types = completeAtEnd('int');
    expect(types!.options).toEqual([{ label: 'integer', kind: 'type', detail: 'type' }]);
  });

  it('offers only declared parameters after @, scanning the whole document', () => {
    const text = 'param minOrders: integer = 3\nparam maxRisk: number = 0.5\natLeast(@m';
    const result = completeDsl(text, text.length, catalog);
    expect(result!.options).toEqual([
      { label: '@minOrders', kind: 'parameter', detail: 'parameter' },
      { label: '@maxRisk', kind: 'parameter', detail: 'parameter' },
    ]);
    expect(result!.from).toBe(text.length - 2);
  });

  it('matches the word on the current line only', () => {
    const text = 'is-active\nand or';
    const result = completeDsl(text, text.length, catalog);
    expect(result!.from).toBe(text.length - 2);
    expect(result!.options.map((option) => option.label)).toEqual(['orders.has-recent']);
  });

  it('narrows case-insensitively and reports validity for further typing', () => {
    const result = completeAtEnd('IS');
    expect(result!.options.map((option) => option.label)).toEqual(['is-active']);
    expect(result!.isValidFor('IS-a')).toBe(true);
    expect(result!.isValidFor('QQ')).toBe(false);
  });

  it('returns null when there is no word before the cursor', () => {
    expect(completeAtEnd('')).toBeNull();
    expect(completeAtEnd('is-active ')).toBeNull();
  });

  it('returns null when nothing matches the prefix', () => {
    expect(completeAtEnd('zzz')).toBeNull();
  });

  it('treats a word reached through an @-less scan as a parameter reference only after @', () => {
    const declared = 'param minOrders: integer = 3\n@';
    const result = completeDsl(declared, declared.length, catalog);
    expect(result!.options).toEqual([{ label: '@minOrders', kind: 'parameter', detail: 'parameter' }]);
  });
});
