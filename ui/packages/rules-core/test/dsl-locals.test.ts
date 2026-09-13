import { describe, it, expect } from 'vitest';
import { tokenize } from '../src/dsl/lexer.js';
import { collectLocalNames, declaredLocals } from '../src/dsl/locals.js';

describe('collectLocalNames', () => {
  it('collects a real preamble let declaration', () => {
    const declarations = collectLocalNames(tokenize('let a = x\n\na'));
    expect(declarations.map((d) => d.name)).toEqual(['a']);
  });

  it('collects multiple preamble declarations in order, deduplicated', () => {
    const declarations = collectLocalNames(tokenize('let a = x\n\nlet a = y\n\nlet b = z\n\na & b'));
    expect(declarations.map((d) => d.name)).toEqual(['a', 'b']);
  });

  it('does not treat a `let` inside a quoted `as` string as a declaration', () => {
    const declarations = collectLocalNames(tokenize('x && (is-active as "let x = 1")'));
    expect(declarations).toEqual([]);
  });

  it('does not treat a `let` after the rule body as a declaration', () => {
    const declarations = collectLocalNames(tokenize('is-active\n\nlet trap = y'));
    expect(declarations).toEqual([]);
  });

  it('does not treat a `let` nested inside a quantifier body brace as a new preamble declaration', () => {
    const declarations = collectLocalNames(
      tokenize('let a = any in orders { let x = 1 }\n\na'),
    );
    expect(declarations.map((d) => d.name)).toEqual(['a']);
  });

  it('returns the declaring name token, not just the name', () => {
    const text = 'let a = x\n\na';
    const [declaration] = collectLocalNames(tokenize(text));
    expect(declaration!.token.from).toBe(text.indexOf('a'));
    expect(declaration!.token.to).toBe(text.indexOf('a') + 1);
  });
});

describe('declaredLocals', () => {
  it('agrees with collectLocalNames on ordinary text', () => {
    expect(declaredLocals('let a = x\n\na')).toEqual(new Set(['a']));
  });

  it('does not match a `let` inside a quoted `as` string', () => {
    expect(declaredLocals('x && (is-active as "let x = 1")')).toEqual(new Set());
  });

  it('does not match a `let` after the rule body', () => {
    expect(declaredLocals('is-active\n\nlet trap = y')).toEqual(new Set());
  });
});
