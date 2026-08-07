import type { Decoration } from '@motiv-rules/core';

/**
 * The patch shape accepted by `setDecoration`, widened so a field can be cleared.
 *
 * The store's Decoration type declares whenTrue/whenFalse as optional Payload fields (no
 * `| undefined` in their type), but exactOptionalPropertyTypes then rejects an object literal that
 * explicitly assigns `undefined` to clear one. Casting a patch to this type is the intentional
 * escape hatch for that clear-to-undefined case.
 */
export type DecorationPatch = Partial<Pick<Decoration, 'whenTrue' | 'whenFalse'>>;
