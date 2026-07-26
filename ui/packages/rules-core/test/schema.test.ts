import { describe, it, expect, beforeAll } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import Ajv2020, { type ValidateFunction } from 'ajv/dist/2020.js';
import type { RuleDocument } from '../src/document.js';
import type { Catalog } from '../src/contracts.js';
import { validateAgainstSchema, type JsonSchema } from '../src/schema.js';

const schemaPath = fileURLToPath(
  new URL('../../../../schemas/rule.v1.json', import.meta.url),
);

let validate: ValidateFunction;

beforeAll(() => {
  const schema = JSON.parse(readFileSync(schemaPath, 'utf8'));
  validate = new Ajv2020({ strict: false }).compile(schema);
});

const documents: RuleDocument[] = [
  { rule: { spec: 'is-positive' } },
  { rule: { spec: 'is-positive', whenTrue: 'ok', whenFalse: 'bad' } },
  { rule: { spec: 'is-positive', whenTrue: 'ok', whenFalse: 'bad', name: 'check' } },
  { rule: { not: { spec: 'is-positive' } } },
  { rule: { and: [{ spec: 'a' }, { spec: 'b' }] } },
  { rule: { orElse: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] } },
  { name: 'doc', rule: { xor: [{ spec: 'a' }, { not: { spec: 'b' } }] } },
];

describe('rule.v1.json drift', () => {
  it.each(documents)('accepts a well-formed document (%#)', (document) => {
    const ok = validate(document);
    expect(validate.errors ?? []).toEqual([]);
    expect(ok).toBe(true);
  });

  it('rejects a malformed document (mixed payload kinds)', () => {
    const bad = { rule: { spec: 'a', whenTrue: 'string', whenFalse: { obj: true } } };
    expect(validate(bad)).toBe(false);
  });
});

// Real exporter shapes from GET /catalog (pinned by CatalogEndpointTests).
const stringMetadataSchema: JsonSchema = { type: ['string', 'null'] };
const verdictMetadataSchema: JsonSchema = {
  type: ['object', 'null'],
  properties: { Code: { type: 'string' } },
  required: ['Code'],
};
const customerModelSchema: JsonSchema = {
  type: ['object', 'null'],
  properties: {
    age: { type: ['string', 'integer'], pattern: '^-?(?:0|[1-9]\\d*)$' },
    name: { type: ['string', 'null'] },
  },
};

describe('Catalog schema maps typing', () => {
  it('a catalog carries metadataTypes and modelTypes keyed by existing names', () => {
    const catalog: Catalog = {
      specs: [{ name: 'is-adult', modelType: 'customer', metadataType: 'Verdict', isAsync: false }],
      collections: [],
      metadataTypes: { String: stringMetadataSchema, Verdict: verdictMetadataSchema },
      modelTypes: { customer: customerModelSchema },
    };
    expect(catalog.metadataTypes?.['Verdict']).toBe(verdictMetadataSchema);
    expect(catalog.modelTypes?.['customer']).toBe(customerModelSchema);
  });
});

describe('validateAgainstSchema', () => {
  describe('type keyword', () => {
    it('accepts a matching single string type', () => {
      expect(validateAgainstSchema('hello', { type: 'string' })).toEqual([]);
    });

    it('rejects a mismatching single string type at the root path', () => {
      const violations = validateAgainstSchema(42, { type: 'string' });
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$');
      expect(violations[0]!.message).toContain('string');
    });

    it('accepts any member of a type union including null', () => {
      expect(validateAgainstSchema('hi', stringMetadataSchema)).toEqual([]);
      expect(validateAgainstSchema(null, stringMetadataSchema)).toEqual([]);
      expect(validateAgainstSchema(null, customerModelSchema)).toEqual([]);
    });

    it('rejects a value matching no member of the union', () => {
      const violations = validateAgainstSchema(true, stringMetadataSchema);
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$');
    });

    it('treats integers as integers but rejects floats for "integer"', () => {
      expect(validateAgainstSchema(3, { type: 'integer' })).toEqual([]);
      expect(validateAgainstSchema(-7, { type: 'integer' })).toEqual([]);
      expect(validateAgainstSchema(3.5, { type: 'integer' })).toHaveLength(1);
    });

    it('accepts floats for "number"', () => {
      expect(validateAgainstSchema(3.5, { type: 'number' })).toEqual([]);
      expect(validateAgainstSchema(3, { type: 'number' })).toEqual([]);
    });

    it('checks "boolean"', () => {
      expect(validateAgainstSchema(false, { type: 'boolean' })).toEqual([]);
      expect(validateAgainstSchema('false', { type: 'boolean' })).toHaveLength(1);
    });

    it('distinguishes objects from arrays and null', () => {
      expect(validateAgainstSchema({}, { type: 'object' })).toEqual([]);
      expect(validateAgainstSchema([], { type: 'object' })).toHaveLength(1);
      expect(validateAgainstSchema(null, { type: 'object' })).toHaveLength(1);
      expect(validateAgainstSchema([], { type: 'array' })).toEqual([]);
      expect(validateAgainstSchema({}, { type: 'array' })).toHaveLength(1);
    });

    it('accepts anything when no type is declared', () => {
      expect(validateAgainstSchema({ anything: true }, {})).toEqual([]);
    });
  });

  describe('web-options number-as-string', () => {
    it('accepts a JS number for ["string","integer"] without applying the pattern', () => {
      expect(validateAgainstSchema({ age: 30 }, customerModelSchema)).toEqual([]);
    });

    it('accepts a numeric string matching the pattern', () => {
      expect(validateAgainstSchema({ age: '30' }, customerModelSchema)).toEqual([]);
      expect(validateAgainstSchema({ age: '-4' }, customerModelSchema)).toEqual([]);
    });

    it('rejects a non-numeric string via the pattern at the property path', () => {
      const violations = validateAgainstSchema({ age: 'thirty' }, customerModelSchema);
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$.age');
    });

    it('rejects a float where an integral value is required', () => {
      const violations = validateAgainstSchema({ age: 30.5 }, customerModelSchema);
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$.age');
    });
  });

  describe('pattern keyword', () => {
    it('applies pattern to plain string types', () => {
      const schema: JsonSchema = { type: 'string', pattern: '^[a-z]+$' };
      expect(validateAgainstSchema('abc', schema)).toEqual([]);
      expect(validateAgainstSchema('ABC', schema)).toHaveLength(1);
    });

    it('ignores an invalid pattern (permissive-by-default)', () => {
      expect(validateAgainstSchema('abc', { type: 'string', pattern: '[' })).toEqual([]);
    });
  });

  describe('properties and required', () => {
    it('recurses into properties with dotted paths', () => {
      const schema: JsonSchema = {
        type: 'object',
        properties: { address: { type: 'object', properties: { city: { type: 'string' } } } },
      };
      const violations = validateAgainstSchema({ address: { city: 42 } }, schema);
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$.address.city');
    });

    it('ignores properties not declared in the schema (permissive)', () => {
      expect(validateAgainstSchema({ Code: 'X', extra: 1 }, verdictMetadataSchema)).toEqual([]);
    });

    it('reports missing required properties at the owning object path', () => {
      const violations = validateAgainstSchema({}, verdictMetadataSchema);
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$');
      expect(violations[0]!.message).toContain('Code');
    });

    it('reports nested missing required properties at the nested path', () => {
      const schema: JsonSchema = {
        type: 'object',
        properties: { verdict: verdictMetadataSchema },
      };
      const violations = validateAgainstSchema({ verdict: {} }, schema);
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$.verdict');
    });

    it('does not apply object keywords to non-object values', () => {
      // null is a legal member of the union; required/properties only apply to objects.
      expect(validateAgainstSchema(null, verdictMetadataSchema)).toEqual([]);
    });
  });

  describe('items keyword', () => {
    it('recurses per element with indexed paths', () => {
      const schema: JsonSchema = { type: 'array', items: { type: 'integer' } };
      expect(validateAgainstSchema([1, 2, 3], schema)).toEqual([]);
      const violations = validateAgainstSchema([1, 'x', 3], schema);
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$[1]');
    });

    it('builds indexed paths under a property', () => {
      const schema: JsonSchema = {
        type: 'object',
        properties: { tags: { type: 'array', items: { type: 'string' } } },
      };
      const violations = validateAgainstSchema({ tags: ['a', 1] }, schema);
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$.tags[1]');
    });
  });

  describe('enum keyword', () => {
    it('accepts a member and rejects a non-member', () => {
      const schema: JsonSchema = { type: 'string', enum: ['a', 'b'] };
      expect(validateAgainstSchema('a', schema)).toEqual([]);
      const violations = validateAgainstSchema('c', schema);
      expect(violations).toHaveLength(1);
      expect(violations[0]!.path).toBe('$');
    });
  });

  describe('unknown keywords', () => {
    it('ignores keywords the validator does not support', () => {
      const schema: JsonSchema = {
        type: 'string',
        format: 'email',
        minLength: 99,
        'x-vendor': { anything: true },
      };
      expect(validateAgainstSchema('not-an-email', schema)).toEqual([]);
    });
  });
});
