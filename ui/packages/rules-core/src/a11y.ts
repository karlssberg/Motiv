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
 * `limit` as a whole count of characters, since that is the only thing the walk below can compare
 * against.
 *
 * This exists because the walk stops on `kept === max`, and an integer counter never equals a
 * fractional limit and never equals `NaN` at all — so an unvalidated limit runs the loop to
 * exhaustion and returns the *whole* string, withdrawing the one guarantee this function makes.
 * `limit` is exported API and a caller may well compute it (a pixel width over a character width
 * is rarely an integer), so the value is normalised rather than trusted.
 *
 * `Infinity` is deliberately preserved: it is a meaningful limit — "no limit" — and the length
 * check answers it correctly without entering the walk at all.
 */
function boundOf(limit: number): number {
  // NaN is the one value with no sensible reading as a count, so it falls back to the standard
  // bound rather than to zero: a caller who passed it by accident still gets a usable name.
  if (Number.isNaN(limit)) return ACCESSIBLE_NAME_LIMIT;
  // Floored, so 10.5 means ten characters; clamped, so a negative limit means no room at all
  // rather than `slice`'s count-from-the-end reading, which returned nearly the whole string.
  return Math.max(0, Math.floor(limit));
}

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
 *
 * Bounded work, in two steps, because the input is a whole printed subtree and the output is at
 * most `limit` characters of it — so materialising every code point of a large expression to keep
 * the first hundred is work with nothing to show for it:
 *
 * 1. **The UTF-16 length is an upper bound on the code-point count**, so a string short enough by
 *    that cheap measure is short enough by the real one. That returns nearly every expression
 *    without inspecting a single character.
 * 2. Otherwise walk the code points and stop at the limit — at most `limit + 1` iterations,
 *    whatever the length of what follows.
 */
export function accessibleExpression(node: RuleNode, limit: number = ACCESSIBLE_NAME_LIMIT): string {
  const text = printInline(node);
  const max = boundOf(limit);
  if (text.length <= max) return text;

  // `end` tracks the UTF-16 index just past the last code point kept, which is where a cut lands
  // on a character boundary. Advanced by `character.length` — 2 for an astral character, 1 for the
  // rest — rather than by 1, which is the whole difference between cutting between characters and
  // cutting through one.
  let end = 0;
  let kept = 0;
  for (const character of text) {
    if (kept === max) return `${text.slice(0, end)}…`;
    end += character.length;
    kept += 1;
  }
  // Longer than `max` in UTF-16 units but not in code points — the case step 1 cannot rule out.
  return text;
}
