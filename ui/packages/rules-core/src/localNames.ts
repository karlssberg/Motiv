/** A valid definition name: a leading letter or underscore, then letters, digits, `_` or `-`. */
export const LOCAL_NAME_PATTERN = /^[A-Za-z_][A-Za-z0-9_-]*$/;

/** Whether `name` is a valid definition name in full. */
export function isValidLocalName(name: string): boolean {
  return LOCAL_NAME_PATTERN.test(name);
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
