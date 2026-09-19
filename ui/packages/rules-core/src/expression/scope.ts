import type { Catalog, JsonSchema } from '../contracts.js';
import { isHigherOrderNode, type RuleDocument } from '../document.js';
import { getNode } from '../paths.js';

/** What a name means at a point in a leaf: the model in scope, its parameters, and any lambda variables. */
export interface LeafScope {
  modelName: string;
  model: JsonSchema;
  parameters: Record<string, { type: 'integer' | 'number' | 'string' | 'boolean' }>;
  vars: Record<string, { schema: JsonSchema; of: string }>;
}

/** The JSON type of a schema, ignoring `null`. */
function primaryType(schema: JsonSchema | undefined): string | undefined {
  if (!schema) return undefined;
  const type = Array.isArray(schema.type) ? schema.type.find((t) => t !== 'null') : schema.type;
  return type ?? (schema.properties ? 'object' : undefined);
}

/** Whether a schema's `type` union admits `null`. */
export function isNullable(schema: JsonSchema | undefined): boolean {
  return Array.isArray(schema?.type) && schema.type.includes('null');
}

/** Whether a schema describes an array (a quantifier's `path` must resolve to one of these). */
export function isCollection(schema: JsonSchema | undefined): boolean {
  return primaryType(schema) === 'array';
}

/** The element schema of a collection schema, or undefined when it is not one. */
export function elementOf(schema: JsonSchema | undefined): JsonSchema | undefined {
  return isCollection(schema) ? schema?.items : undefined;
}

/** The declared fields of an object schema, as name/schema pairs. */
export function fieldsOf(schema: JsonSchema | undefined): Array<{ name: string; schema: JsonSchema }> {
  return Object.entries(schema?.properties ?? {}).map(([name, s]) => ({ name, schema: s }));
}

const FORMAT_NAMES: Record<string, string> = { int32: 'int', int64: 'long', single: 'float', double: 'double', decimal: 'decimal' };

/** The name shown to authors: the CLR kind when the host stamped one, else the JSON type. */
export function typeName(schema: JsonSchema | undefined): string {
  if (!schema) return 'unknown';
  if (schema.format && FORMAT_NAMES[schema.format]) return FORMAT_NAMES[schema.format]!;
  const type = primaryType(schema);
  if (type === 'array') return `collection of ${typeName(schema.items)}`;
  return type ?? 'object';
}

/** Returns a new scope with a lambda variable added, leaving `scope` untouched. */
export function withVar(scope: LeafScope, name: string, schema: JsonSchema, of: string): LeafScope {
  return { ...scope, vars: { ...scope.vars, [name]: { schema, of } } };
}

/** Every ancestor path of `path`, nearest first, including `path` itself. */
function ancestors(path: string): string[] {
  const out: string[] = [];
  for (let current = path; current.length > 1; ) {
    out.push(current);
    const cut = Math.max(current.lastIndexOf('.'), current.lastIndexOf('['));
    if (cut <= 0) break;
    current = current.slice(0, cut);
  }
  return out;
}

/**
 * The model a leaf at `path` sees: the rule's model, or — inside a quantifier body — the element
 * of the collection the quantifier walks, resolved through the schema by the node's `path`.
 */
export function scopeAt(document: RuleDocument, path: string, modelType: string, catalog: Catalog): LeafScope | null {
  const root = catalog.modelTypes?.[modelType];
  if (!root) return null;
  const parameters = Object.fromEntries(
    Object.entries(document.parameters ?? {}).map(([name, p]) => [name, { type: p.type }]),
  ) as LeafScope['parameters'];

  let model = root;
  let modelName = modelType;
  // Walk from the root down, so nested quantifiers each narrow the model in turn.
  for (const ancestor of ancestors(path).reverse()) {
    if (ancestor === path) continue; // the quantifier itself is not inside its own body
    const node = getNode(document, ancestor);
    if (!node || !isHigherOrderNode(node)) continue;
    const collection = node.path.split('.').reduce<JsonSchema | undefined>((s, segment) => s?.properties?.[segment], model);
    const element = elementOf(collection);
    if (!element) return null;
    model = element;
    modelName = `each of ${node.path}`;
  }
  return { modelName, model, parameters, vars: {} };
}
