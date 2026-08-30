import { describe, it, expect } from 'vitest';
import { ACCESSIBLE_NAME_LIMIT, accessibleExpression } from '../src/a11y.js';
import type { RuleNode } from '../src/document.js';

describe('accessibleExpression', () => {
  it('names a composition by the DSL text the product already generates for it', () => {
    const node: RuleNode = { and: [{ spec: 'customer.is-active' }, { spec: 'customer.is-adult' }] };
    expect(accessibleExpression(node)).toBe('customer.is-active & customer.is-adult');
  });

  it('names a leaf by itself, so every group in a tree is named the same way', () => {
    expect(accessibleExpression({ spec: 'customer.is-active' })).toBe('customer.is-active');
  });

  it('cuts a subtree too large to be spoken, and says that it did', () => {
    const wide: RuleNode = { and: Array.from({ length: 40 }, (_, i) => ({ spec: `spec.number-${i}` })) };
    const name = accessibleExpression(wide);

    // Counted in code points, which is what the limit is in. `name.length` counts UTF-16 units,
    // so it agrees only for the ASCII this fixture happens to be — and would be measuring the
    // wrong thing for the astral cases below, which are the whole reason the cut is code-point-wise.
    expect([...name]).toHaveLength(ACCESSIBLE_NAME_LIMIT + 1);
    expect(name.endsWith('…')).toBe(true);
    expect(name.startsWith('spec.number-0 & spec.number-1')).toBe(true);
  });

  it('leaves an expression that fits exactly at the limit uncut', () => {
    const exact: RuleNode = { spec: 'x'.repeat(ACCESSIBLE_NAME_LIMIT) };
    expect(accessibleExpression(exact)).toBe('x'.repeat(ACCESSIBLE_NAME_LIMIT));
  });

  it('takes a caller-chosen limit, since one name may have room another does not', () => {
    const node: RuleNode = { and: [{ spec: 'customer.is-active' }, { spec: 'customer.is-adult' }] };
    expect(accessibleExpression(node, 10)).toBe('customer.i…');
  });

  /**
   * `limit` is exported API, so a caller can pass anything the `number` type allows — a width
   * divided by a character width, say, which is rarely an integer. The guarantee this function
   * makes is that its result is bounded, and a limit it cannot interpret must not be allowed to
   * silently withdraw that.
   */
  describe('a limit that is not a whole count', () => {
    const long: RuleNode = { spec: 'x'.repeat(300) };

    it('floors a fractional limit rather than never reaching it', () => {
      expect(accessibleExpression(long, 10.5)).toBe(`${'x'.repeat(10)}…`);
    });

    it('falls back to the default when the limit is not a number at all', () => {
      expect(accessibleExpression(long, Number.NaN)).toBe(`${'x'.repeat(ACCESSIBLE_NAME_LIMIT)}…`);
    });

    it('treats a negative limit as no room, not as room measured from the end', () => {
      expect(accessibleExpression(long, -5)).toBe('…');
    });

    it('leaves an infinite limit meaning what it says: no limit', () => {
      expect(accessibleExpression(long, Number.POSITIVE_INFINITY)).toBe('x'.repeat(300));
    });
  });

  /**
   * A decoration can carry arbitrary text, so an expression can carry characters outside the BMP.
   * Each of those is two UTF-16 units, which is why the limit counts code points: measured in
   * units, a name of 80 emoji would be cut at 60 of them — and cut *through* the 61st, ending the
   * name in half a character.
   */
  describe('outside the BMP', () => {
    /** 80 code points, 160 UTF-16 units: over the limit by the wrong measure, under it by the right one. */
    const wide: RuleNode = { spec: '😀'.repeat(80) };

    it('leaves a name under the limit alone, however many UTF-16 units it occupies', () => {
      const name = accessibleExpression(wide);

      expect(name).toBe('😀'.repeat(80));
      expect(name.length).toBeGreaterThan(ACCESSIBLE_NAME_LIMIT);
    });

    it('cuts between characters, never through one', () => {
      const name = accessibleExpression(wide, 10);

      expect(name).toBe(`${'😀'.repeat(10)}…`);
      // A cut through a surrogate pair leaves a lone surrogate, which renders as U+FFFD. Asserting
      // its absence is what says the cut respected character boundaries rather than byte ones.
      expect(name).not.toContain('\uFFFD');
      expect([...name]).toHaveLength(11);
    });
  });
});
