import { describe, it, expect } from 'vitest';
import {
  parse, print, tokenize, mergeDecorations,
  WORD_START_CHARS, WORD_REST_CHARS, PARAM_REST_CHARS,
} from '../src/index.js';

describe('DSL public exports', () => {
  it('exposes the language layer from the package root', () => {
    expect(typeof tokenize).toBe('function');
    expect(typeof parse).toBe('function');
    expect(typeof print).toBe('function');
    expect(typeof mergeDecorations).toBe('function');
  });

  // Consumers outside this package (the demo's CodeMirror stream parser and completion source)
  // build their own word-matching regexes from these character classes rather than hand-copying
  // the lexer's — a hand-copy is exactly what let them drift out of sync when dots were admitted
  // to spec words. If these stop being exported, that duplication silently comes back.
  it('exposes the word character classes so callers cannot fall back to hand-copying them', () => {
    expect(typeof WORD_START_CHARS).toBe('string');
    expect(typeof WORD_REST_CHARS).toBe('string');
    expect(typeof PARAM_REST_CHARS).toBe('string');
  });

  it('round-trips through the public entry point', () => {
    expect(parse(print({ rule: { spec: 'is-active' } })).document)
      .toEqual({ rule: { spec: 'is-active' } });
  });
});
