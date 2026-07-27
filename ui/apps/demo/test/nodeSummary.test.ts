import { describe, it, expect } from 'vitest';
import { summarize } from '../src/builder/nodeSummary.js';

describe('summarize', () => {
  it('describes a two-operand xor as exactly one', () => {
    const node = { xor: [{ spec: 'a' }, { spec: 'b' }] };
    expect(summarize(node).description).toBe('exactly one must hold');
  });

  it('describes a three-operand xor as parity, not exactly one', () => {
    const node = { xor: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] };
    expect(summarize(node).description).toBe('an odd number must hold');
  });

  it('leaves other operators unaffected by operand count', () => {
    expect(summarize({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] }).description)
      .toBe('all must hold');
  });
});
