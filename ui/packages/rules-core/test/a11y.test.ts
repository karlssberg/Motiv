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

    expect(name.length).toBeLessThanOrEqual(ACCESSIBLE_NAME_LIMIT + 1);
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
});
