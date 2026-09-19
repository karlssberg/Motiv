import type { JsonSchema } from '../contracts.js';

export type NumericKind = 'int32' | 'int64' | 'single' | 'double' | 'decimal';

const WIDENINGS: ReadonlyArray<readonly [NumericKind, NumericKind]> = [
  ['int32', 'int64'], ['int32', 'decimal'], ['int64', 'decimal'], ['int32', 'double'], ['single', 'double'],
];

const NAMES: Record<NumericKind, string> = { int32: 'int', int64: 'long', single: 'float', double: 'double', decimal: 'decimal' };

export function kindName(kind: NumericKind): string { return NAMES[kind]; }
export function isIntegral(kind: NumericKind): boolean { return kind === 'int32' || kind === 'int64'; }

/** The CLR numeric kind of a schema: the host's `format` stamp, else the JSON type's conventional default. */
export function kindOf(schema: JsonSchema | undefined): NumericKind | undefined {
  if (!schema) return undefined;
  const format = schema.format;
  if (format === 'int32' || format === 'int64' || format === 'single' || format === 'double' || format === 'decimal') return format;
  const type = Array.isArray(schema.type) ? schema.type.find((t) => t !== 'null') : schema.type;
  if (type === 'integer') return 'int32';
  if (type === 'number') return 'double';
  return undefined;
}

export function canWiden(from: NumericKind, to: NumericKind): boolean {
  return from === to || WIDENINGS.some(([f, t]) => f === from && t === to);
}

export function join(a: NumericKind, b: NumericKind): NumericKind | undefined {
  if (canWiden(a, b)) return b;
  if (canWiden(b, a)) return a;
  return undefined;
}
