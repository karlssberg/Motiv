import { describe, it, expect } from 'vitest';
import { toExplanationView, flattenExplanation } from '../src/explanation.js';
import type { ExplanationNode } from '../src/contracts.js';

const tree: ExplanationNode = {
  assertions: ['AND'],
  underlying: [
    { assertions: ['is positive'], underlying: [] },
    { assertions: ['is even'], underlying: [{ assertions: ['divisible by 2'], underlying: [] }] },
  ],
};

describe('toExplanationView', () => {
  it('assigns stable ids and depth', () => {
    const view = toExplanationView(tree);
    expect(view.id).toBe('0');
    expect(view.depth).toBe(0);
    expect(view.children[0]!.id).toBe('0.0');
    expect(view.children[1]!.children[0]!.id).toBe('0.1.0');
    expect(view.children[1]!.children[0]!.depth).toBe(2);
  });
});

describe('flattenExplanation', () => {
  it('lists all rows when nothing is collapsed', () => {
    const rows = flattenExplanation(toExplanationView(tree), new Set());
    expect(rows.map((r) => r.id)).toEqual(['0', '0.0', '0.1', '0.1.0']);
    expect(rows[1]!.hasChildren).toBe(false);
    expect(rows[2]!.hasChildren).toBe(true);
  });
  it('hides descendants of a collapsed node', () => {
    const rows = flattenExplanation(toExplanationView(tree), new Set(['0.1']));
    expect(rows.map((r) => r.id)).toEqual(['0', '0.0', '0.1']);
    expect(rows[2]!.collapsed).toBe(true);
  });
});
