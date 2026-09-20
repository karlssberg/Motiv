import { describe, it, expect } from 'vitest';
import { sampleModelFor } from '../../src/panes/sampleModel.js';

describe('sampleModelFor', () => {
  it('is an empty object until the catalog has described the model', () => {
    expect(sampleModelFor(undefined)).toBe('{}');
  });

  it('gives every declared property a representative value of its type, one level deep', () => {
    const text = sampleModelFor({
      type: ['object', 'null'],
      properties: {
        age: { type: 'integer' },
        name: { type: ['string', 'null'] },
        isActive: { type: 'boolean' },
        orders: { type: ['array', 'null'], items: { type: 'object', properties: { total: { type: 'number' } } } },
        address: { type: 'object', properties: { city: { type: 'object', properties: { code: { type: 'string' } } } } },
      },
    });
    expect(JSON.parse(text)).toEqual({ age: 1, name: '', isActive: true, orders: [], address: { city: {} } });
  });

  it('reads a numeric published as a string-or-number union as the number it is', () => {
    // System.Text.Json's number handling exports numerics as `["string", "number"]` with a format
    // stamp; the sample must not take the first member and offer `""` for a decimal.
    const text = sampleModelFor({
      type: 'object',
      properties: { total: { type: ['string', 'number'], format: 'decimal' }, age: { type: ['string', 'integer'], format: 'int32' } },
    });
    expect(JSON.parse(text)).toEqual({ total: 1, age: 1 });
  });
});
