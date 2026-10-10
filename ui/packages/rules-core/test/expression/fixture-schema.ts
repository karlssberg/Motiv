import type { JsonSchema } from '../../src/index.js';

/**
 * The model shapes the conformance corpus (`./corpus.json`) checks against. Mirrors
 * `test/Motiv.Serialization.Tests/Expressions/CorpusFixtures.cs`'s `Order`/`Customer`/`Shipment`
 * records — both sides must agree on every case in the corpus.
 */
export const ORDER: JsonSchema = {
  type: 'object',
  properties: {
    status: { type: 'string', enum: ['paid', 'pending', 'refunded'] },
    total: { type: 'number', format: 'decimal' },
    daysSinceShipped: { type: 'integer', format: 'int32' },
    kind: { type: 'string', enum: ['Retail', 'Wholesale'] },
  },
  required: ['status', 'total', 'daysSinceShipped', 'kind'],
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

/**
 * What the catalog publishes for `Shipment` when its host names enums through registered converters
 * (`CorpusFixtures.ShipmentJson`) rather than attributes — `priority` under a camel-case policy.
 */
export const SHIPMENT: JsonSchema = {
  type: 'object',
  properties: {
    channel: { type: 'string', enum: ['Retail', 'Wholesale'] },
    priority: { type: 'string', enum: ['standard', 'nextDay'] },
  },
  required: ['channel', 'priority'],
};
