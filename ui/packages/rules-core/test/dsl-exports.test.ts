import { describe, it, expect } from 'vitest';
import { parse, print, tokenize, mergeDecorations } from '../src/index.js';

describe('DSL public exports', () => {
  it('exposes the language layer from the package root', () => {
    expect(typeof tokenize).toBe('function');
    expect(typeof parse).toBe('function');
    expect(typeof print).toBe('function');
    expect(typeof mergeDecorations).toBe('function');
  });

  it('round-trips through the public entry point', () => {
    expect(parse(print({ rule: { spec: 'is-active' } })).document)
      .toEqual({ rule: { spec: 'is-active' } });
  });
});
