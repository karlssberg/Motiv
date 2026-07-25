import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';

describe('parse — errors', () => {
  it('reports an unclosed group on the opening paren', () => {
    const result = parse('(is-active');
    expect(result.document).toBeUndefined();
    expect(result.errors).toContainEqual(
      expect.objectContaining({ code: 'UnclosedGroup', from: 0, to: 1 }),
    );
  });

  it('reports a stray closing paren', () => {
    const result = parse('is-active)');
    expect(result.document).toBeUndefined();
    expect(result.errors[0]).toMatchObject({ code: 'UnexpectedToken', from: 9, to: 10 });
  });

  it('reports a missing quantifier body', () => {
    const result = parse('all in orders is-positive');
    expect(result.errors[0]).toMatchObject({ code: 'ExpectedBody' });
  });

  it('reports an unclosed quantifier body on the opening brace', () => {
    const result = parse('all in orders { is-positive');
    expect(result.errors).toContainEqual(
      expect.objectContaining({ code: 'UnclosedBody', from: 14, to: 15 }),
    );
  });

  it('reports a missing collection path', () => {
    expect(parse('all in { is-positive }').errors[0]).toMatchObject({ code: 'ExpectedCollection' });
  });

  it('reports a missing count for a counted quantifier', () => {
    expect(parse('atLeast in orders { is-positive }').errors[0]).toMatchObject({
      code: 'ExpectedCount',
    });
  });

  it('reports a non-numeric count', () => {
    expect(parse('atLeast(x) in orders { is-positive }').errors[0]).toMatchObject({
      code: 'ExpectedCount',
    });
  });

  it('reports a missing name after as', () => {
    expect(parse('is-active as').errors[0]).toMatchObject({ code: 'ExpectedName' });
  });

  it('reports an empty document', () => {
    expect(parse('').errors[0]).toMatchObject({ code: 'UnexpectedEnd' });
  });

  it('reports a dangling binary operator', () => {
    expect(parse('is-active &&').errors[0]).toMatchObject({ code: 'UnexpectedEnd' });
  });

  it('reports an unrecognised character on its own range', () => {
    expect(parse('is-active # b').errors[0]).toMatchObject({
      code: 'UnexpectedToken', from: 10, to: 11,
    });
  });

  it('still returns spans for the part it understood', () => {
    expect(parse('(is-active').spans.length).toBeGreaterThan(0);
  });
});
