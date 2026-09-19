import type { JsonSchema } from '@motiv-rules/core';

/**
 * A sample model an author can evaluate straight away, shaped by the model's schema: every
 * declared property with a representative value of its type. Derived rather than curated, so a
 * proposition on any registered model — not only the one Studio started with — opens on a sample
 * its own schema accepts. `{}` until the catalog has said what the model looks like.
 */
export function sampleModelFor(schema: JsonSchema | undefined): string {
  return JSON.stringify(sampleValue(schema, 0), null, 2);
}

/**
 * The type a value should take. A union names `null` for an optional member, and the host's
 * number handling publishes every numeric as `["string", "number"]` — so the string member of a
 * union is the last resort, never the first pick.
 */
function typeOf(schema: JsonSchema | undefined): string | undefined {
  const types = (Array.isArray(schema?.type) ? schema.type : [schema?.type]).filter((t) => t !== undefined && t !== 'null');
  return types.find((t) => t !== 'string') ?? types[0] ?? (schema?.properties ? 'object' : undefined);
}

function sampleValue(schema: JsonSchema | undefined, depth: number): unknown {
  switch (typeOf(schema)) {
    case 'integer':
    case 'number': return 1;
    case 'string': return '';
    case 'boolean': return true;
    case 'array': return [];
    case 'object':
      // One level of nesting is enough to show the shape; deeper objects stay empty.
      return Object.fromEntries(
        Object.entries(schema?.properties ?? {}).map(([name, s]) => [name, depth < 1 ? sampleValue(s, depth + 1) : {}]),
      );
    default: return {};
  }
}
