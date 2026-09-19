import type { JsonSchema } from '../../src/index.js';

/**
 * The two model shapes the conformance corpus (`./corpus.json`) checks against. Mirrors
 * `src/Motiv.Serialization.Tests/Expressions/CorpusFixtures.cs`'s `Order`/`Customer` records —
 * both sides must agree on every case in the corpus.
 */
export const ORDER: JsonSchema = {
  type: 'object',
  properties: {
    status: { type: 'string', enum: ['paid', 'pending', 'refunded'] },
    total: { type: 'number', format: 'decimal' },
    daysSinceShipped: { type: 'integer', format: 'int32' },
  },
  required: ['status', 'total', 'daysSinceShipped'],
};

export const CUSTOMER: JsonSchema = {
  type: 'object',
  properties: {
    age: { type: 'integer', format: 'int32' },
    points: { type: 'integer', format: 'int64' },
    score: { type: 'number', format: 'double' },
    isActive: { type: 'boolean' },
    creditLimit: { type: 'number', format: 'decimal' },
    country: { type: ['string', 'null'] },
    orders: { type: ['array', 'null'], items: ORDER },
    shippedAt: { type: ['string', 'null'], format: 'date-time' },
  },
  required: ['age', 'points', 'score', 'isActive', 'creditLimit'],
};
