import { DSL_KEYWORDS, DSL_QUANTIFIERS, DSL_TYPES } from './dsl/lexer.js';

/** A valid definition name: a leading letter or underscore, then letters, digits, `_` or `-`. */
export const LOCAL_NAME_PATTERN = /^[A-Za-z_][A-Za-z0-9_-]*$/;

/**
 * Every DSL reserved word — keywords, types and quantifiers — none of which may name a local. A
 * declared local is read back by `parsePrimary`'s bare-word branch, which only ever sees a `spec`
 * kind token; a reserved word always lexes as `keyword`/`type`/`quantifier` instead, so a local
 * named `all` or `param` could declare and print but could never resolve on reparse.
 */
export const RESERVED_LOCAL_NAMES: ReadonlySet<string> = new Set<string>([
  ...DSL_KEYWORDS, ...DSL_TYPES, ...DSL_QUANTIFIERS,
]);

/** Whether `name` is a valid definition name in full — matches the pattern and is not reserved. */
export function isValidLocalName(name: string): boolean {
  return LOCAL_NAME_PATTERN.test(name) && !RESERVED_LOCAL_NAMES.has(name);
}

/**
 * The as-you-type rule applied on every keystroke while authoring a definition name: each space
 * becomes `-`, each remaining character outside `[A-Za-z0-9_-]` is dropped, then a leading run of
 * characters outside `[A-Za-z_]` is dropped so the result is either empty or valid-prefixed.
 * Length-preserving for the space-only case — every other rejection shortens the string.
 */
export function normalizeLocalName(typed: string): string {
  const spaced = typed.replace(/ /g, '-');
  const stripped = spaced.replace(/[^A-Za-z0-9_-]/g, '');
  return stripped.replace(/^[^A-Za-z_]+/, '');
}

/**
 * The on-blur rule: `normalizeLocalName`, then runs of `-` collapse to one and a trailing `-` is
 * trimmed — so `finishLocalName` always returns either `''` or a name matching {@link LOCAL_NAME_PATTERN}.
 */
export function finishLocalName(typed: string): string {
  return normalizeLocalName(typed).replace(/-+/g, '-').replace(/-+$/, '');
}
