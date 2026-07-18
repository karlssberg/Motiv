import { describe, it, expect, beforeAll } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import Ajv2020, { type ValidateFunction } from 'ajv/dist/2020.js';
import type { RuleDocument } from '../src/document.js';

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
