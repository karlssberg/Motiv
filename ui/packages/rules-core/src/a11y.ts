import { printInline } from './dsl/printer.js';
import type { RuleNode } from './document.js';

/**
 * How much generated text an accessible name carries before it is cut short, in code points.
 *
 * A cap is needed because the name of a group is announced on *entering* it, and a rule's root
 * group holds the whole rule: without one, arriving at the builder reads out the entire
 * composition before the user has moved anywhere. 120 is about a spoken sentence — long enough
 * for the compositions a person actually authors by hand, short enough that a generated
 * thousand-operand document does not have to be sat through.
 */
export const ACCESSIBLE_NAME_LIMIT = 120;

/**
 * A rule node's own generated DSL text, bounded for use as an accessible name.
 *
 * This is the product's thesis turned into an affordance. Motiv exists to linearise boolean
 * structure into readable text; a screen reader needs exactly that, because the indentation and
 * connecting lines a sighted reader gets the structure from are not conveyed at all. So the
 * accessible name of the group holding a subtree is the subtree *as the DSL prints it* — the same
 * string the strip above the tree shows, and the same one the engine's `Reason` is built from.
 *
 * Naming a group by its expression and nothing else is deliberate: the `group` role supplies the
 * noun when it is announced, so no English glue has to be invented — and glue would have to differ
 * per node kind anyway, since an operator has operands where a quantifier has a body.
 *
 * Cut by code point rather than by `slice`, so a truncation cannot land inside a surrogate pair
 * and end the name with half a character. The ellipsis is part of the name on purpose: a name that
 * stops mid-expression without saying so reads as a complete, and wrong, expression.
 */
export function accessibleExpression(node: RuleNode, limit: number = ACCESSIBLE_NAME_LIMIT): string {
  const text = printInline(node);
  const points = [...text];
  return points.length <= limit ? text : `${points.slice(0, limit).join('')}…`;
}
