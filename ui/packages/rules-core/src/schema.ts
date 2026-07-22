import type { JsonSchema } from './contracts.js';

export type { JsonSchema } from './contracts.js';

/**
 * One structural mismatch between a value and a {@link JsonSchema}.
 * Paths use the RuleError idiom: `$` is the root, `.key` steps into object
 * properties, and `[i]` indexes array elements (e.g. `$.orders[0].total`).
 */
export interface SchemaViolation {
  path: string;
  message: string;
}

const jsonTypeOf = (value: unknown): string => {
  if (value === null) return 'null';
  if (Array.isArray(value)) return 'array';
  const t = typeof value;
  if (t === 'number') return Number.isInteger(value) ? 'integer' : 'number';
  return t;
};

function matchesType(value: unknown, type: string): boolean {
  switch (type) {
    case 'null': return value === null;
    case 'string': return typeof value === 'string';
    case 'boolean': return typeof value === 'boolean';
    case 'number': return typeof value === 'number';
    case 'integer': return typeof value === 'number' && Number.isInteger(value);
    case 'object': return typeof value === 'object' && value !== null && !Array.isArray(value);
    case 'array': return Array.isArray(value);
    default: return true; // Unknown type names are ignored (permissive-by-default).
  }
}

function compileSafely(pattern: string): RegExp | undefined {
  try {
    return new RegExp(pattern);
  } catch {
    return undefined; // Invalid patterns are ignored (permissive-by-default).
  }
}

function validate(value: unknown, schema: JsonSchema, path: string, out: SchemaViolation[]): void {
  const types = schema.type === undefined ? undefined
    : Array.isArray(schema.type) ? schema.type : [schema.type];
  if (types && !types.some((t) => matchesType(value, t))) {
    out.push({ path, message: `expected ${types.join(' | ')}, got ${jsonTypeOf(value)}` });
    return;
  }

  // Web-binder parity: a schema like {"type":["string","integer"],"pattern":"^-?\d+$"}
  // means numeric strings are accepted by the backend — the pattern constrains the
  // string form only, so it never applies to values that matched a numeric type.
  if (typeof value === 'string' && schema.pattern !== undefined) {
    const regex = compileSafely(schema.pattern);
    if (regex && !regex.test(value)) {
      out.push({ path, message: `does not match pattern ${schema.pattern}` });
    }
  }

  if (schema.enum !== undefined) {
    const encoded = JSON.stringify(value);
    if (!schema.enum.some((member) => JSON.stringify(member) === encoded)) {
      out.push({ path, message: `not one of ${JSON.stringify(schema.enum)}` });
    }
  }

  if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
    const record = value as Record<string, unknown>;
    for (const key of schema.required ?? []) {
      if (!(key in record)) out.push({ path, message: `missing required property "${key}"` });
    }
    for (const [key, propertySchema] of Object.entries(schema.properties ?? {})) {
      if (key in record) validate(record[key], propertySchema, `${path}.${key}`, out);
    }
  }

  if (Array.isArray(value) && schema.items !== undefined) {
    value.forEach((element, i) => validate(element, schema.items!, `${path}[${i}]`, out));
  }
}

/**
 * Structurally validates a value against the exporter's POCO subset of JSON
 * Schema: `type` (string or union, incl. `"null"` and `"integer"`), `pattern`
 * on strings, `enum`, `properties`, `required`, and `items`. Unknown keywords
 * are ignored. Returns an empty array when the value conforms.
 */
export function validateAgainstSchema(value: unknown, schema: JsonSchema): SchemaViolation[] {
  const violations: SchemaViolation[] = [];
  validate(value, schema, '$', violations);
  return violations;
}
