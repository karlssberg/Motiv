import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';
import type { Catalog } from '../src/contracts.js';

const CATALOG: Catalog = {
  specs: [
    {
      name: 'at-least', modelType: 'customer', metadataType: 'String', isAsync: false,
      origin: 'Compiled',
      parameters: [
        { name: 'floor', type: 'integer' },
        { name: 'label', type: 'string' },
      ],
    },
    {
      name: 'plain', modelType: 'customer', metadataType: 'String', isAsync: false,
      origin: 'Compiled',
    },
  ],
  collections: [],
};

/**
 * The backend sends an explicit JSON `null` for a plain spec's `parameters` rather than omitting
 * the property, so `CatalogEntry.parameters` is `CatalogParameter[] | null | undefined`. A guard
 * written as `=== undefined` would silently fail to fire against this literal `null` — pinned
 * separately from `plain` above (which has `parameters` merely absent) to catch that exact bug.
 */
const CATALOG_WITH_NULL_PARAMETERS: Catalog = {
  specs: [
    {
      name: 'plain-null', modelType: 'customer', metadataType: 'String', isAsync: false,
      origin: 'Compiled', parameters: null,
    },
  ],
  collections: [],
};

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

  it('reports an empty argument list', () => {
    expect(parse('s()').errors[0]).toMatchObject({ code: 'ExpectedArgName' });
  });

  it('reports a missing `=` after an argument name', () => {
    expect(parse('s(n 1)').errors[0]).toMatchObject({ code: 'ExpectedArgValue' });
  });

  it('reports a missing argument value', () => {
    expect(parse('s(n = )').errors[0]).toMatchObject({ code: 'ExpectedArgValue' });
  });

  it('reports an unclosed argument list', () => {
    expect(parse('s(n = 1').errors[0]).toMatchObject({ code: 'UnclosedArgs' });
  });

  it('reports a duplicate argument name', () => {
    expect(parse('s(n = 1, n = 2)').errors[0]).toMatchObject({ code: 'DuplicateArg' });
  });

  it('rejects a bare word as an argument value', () => {
    expect(parse('s(n = all)').errors[0]).toMatchObject({ code: 'ExpectedArgValue' });
  });

  it('refuses a positional argument with no catalog', () => {
    expect(parse('at-least(2)').errors[0]).toMatchObject({ code: 'ExpectedArgName' });
  });

  it('refuses a positional argument for a spec the catalog does not name', () => {
    expect(parse('unknown(2)', { catalog: CATALOG }).errors[0])
      .toMatchObject({ code: 'UnknownParameterisedSpec' });
  });

  it('refuses arguments for a spec with no declared parameters', () => {
    expect(parse('plain(2)', { catalog: CATALOG }).errors[0])
      .toMatchObject({ code: 'UnexpectedArguments' });
  });

  it('refuses more positional arguments than declared parameters', () => {
    expect(parse('at-least(1, 2, 3)', { catalog: CATALOG }).errors[0])
      .toMatchObject({ code: 'TooManyArguments' });
  });

  it('refuses a positional argument after a named one', () => {
    expect(parse('at-least(floor = 1, 2)', { catalog: CATALOG }).errors[0])
      .toMatchObject({ code: 'PositionalAfterNamed' });
  });

  it('refuses arguments for a spec whose catalog parameters are literal null', () => {
    expect(parse('plain-null(1)', { catalog: CATALOG_WITH_NULL_PARAMETERS }).errors[0])
      .toMatchObject({ code: 'UnexpectedArguments' });
  });
});

describe('parse — unterminated literals', () => {
  it('reports an unterminated name string over the opening quote to end-of-input', () => {
    const result = parse('is-active as "x');
    expect(result.document).toBeUndefined();
    expect(result.errors[0]).toMatchObject({ code: 'UnterminatedString', from: 13, to: 15 });
  });

  it('reports a lone opening quote', () => {
    expect(parse('is-active as "').errors[0]).toMatchObject({
      code: 'UnterminatedString', from: 13, to: 14,
    });
  });

  it('reports an unterminated backtick expression', () => {
    const result = parse('`n > 0');
    expect(result.document).toBeUndefined();
    expect(result.errors[0]).toMatchObject({ code: 'UnterminatedExpression', from: 0, to: 6 });
  });

  it('reports a lone opening backtick', () => {
    expect(parse('`').errors[0]).toMatchObject({
      code: 'UnterminatedExpression', from: 0, to: 1,
    });
  });

  it('reports an unterminated parameter default string', () => {
    const result = parse('param a: string = "gold\n\nis-active');
    expect(result.document).toBeUndefined();
    expect(result.errors[0]).toMatchObject({ code: 'UnterminatedString', from: 18 });
  });

  it('rejects a negative count, which the schema forbids', () => {
    expect(parse('atLeast(-1) in orders { a }').errors[0]).toMatchObject({ code: 'ExpectedCount' });
  });

  it('rejects a fractional count, which the schema forbids', () => {
    expect(parse('atLeast(2.5) in orders { a }').errors[0]).toMatchObject({ code: 'ExpectedCount' });
  });
});
