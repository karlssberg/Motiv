import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';
import { listPaths } from '../src/paths.js';
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

describe('parse — leaves and grouping', () => {
  it('parses a bare spec into a spec node at the root path', () => {
    const result = parse('is-active');
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual({ rule: { spec: 'is-active' } });
  });

  it('reads `as` as a plain word, not a keyword — there is no inline naming clause', () => {
    expect(parse('as').document).toEqual({ rule: { spec: 'as' } });
  });

  it('no longer parses a trailing `as "name"` clause — it is a stray, unexpected token', () => {
    const result = parse('x as "y"');
    expect(result.document).toBeUndefined();
    expect(result.errors).toContainEqual(
      expect.objectContaining({ code: 'UnexpectedToken' }),
    );
  });

  it('records a span for the root node covering the spec token', () => {
    expect(parse('is-active').spans).toEqual([{ path: '$.rule', from: 0, to: 9 }]);
  });

  it('parses a backtick expression into an expression node, stripping the backticks', () => {
    expect(parse('`n > 0`').document).toEqual({ rule: { expression: 'n > 0' } });
  });

  it('parses negation into a not node', () => {
    expect(parse('!is-flagged').document).toEqual({ rule: { not: { spec: 'is-flagged' } } });
  });

  it('parses double negation', () => {
    expect(parse('!!is-flagged').document).toEqual({
      rule: { not: { not: { spec: 'is-flagged' } } },
    });
  });

  it('spans a not node and its child at distinct paths', () => {
    expect(parse('!is-flagged').spans).toEqual([
      { path: '$.rule', from: 0, to: 11 },
      { path: '$.rule.not', from: 1, to: 11 },
    ]);
  });

  it('parses a parenthesised expression as the inner node, without a wrapper', () => {
    expect(parse('(is-active)').document).toEqual({ rule: { spec: 'is-active' } });
  });

  it('parses a single named argument', () => {
    const result = parse('approver-count-at-least(n = 1)');
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({ spec: 'approver-count-at-least', args: { n: 1 } });
  });

  it('parses every literal kind an argument may take', () => {
    const result = parse('s(count = -2, ratio = 2.5, label = "high", strict = true, note = null)');
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({
      spec: 's',
      args: { count: -2, ratio: 2.5, label: 'high', strict: true, note: null },
    });
  });

  it('parses args on a spec inside a composition', () => {
    const result = parse('s(n = 1) & !t(flag = false)');
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({
      and: [{ spec: 's', args: { n: 1 } }, { not: { spec: 't', args: { flag: false } } }],
    });
  });

  it('does not let an argument name reach the prototype', () => {
    const result = parse('s(__proto__ = 1)');
    expect(result.errors).toEqual([]);
    const args = (result.document?.rule as { args: Record<string, unknown> }).args;
    expect(Object.getPrototypeOf(args)).toBe(Object.prototype);
    expect(Object.keys(args)).toEqual(['__proto__']);
  });

  it('accepts keyword-shaped argument names', () => {
    const result = parse('s(all = 1, string = "x", param = true, in = 2)');
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({
      spec: 's', args: { all: 1, string: 'x', param: true, in: 2 },
    });
  });

  it('accepts a keyword-shaped parameter name', () => {
    const result = parse('param all: integer = 2\n\ns');
    expect(result.errors).toEqual([]);
    expect(result.document?.parameters).toEqual({ all: { type: 'integer', default: 2 } });
  });

  it('accepts a keyword-shaped collection path', () => {
    const result = parse('any in string { is-positive }');
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({ asAnySatisfied: { spec: 'is-positive' }, path: 'string' });
  });

  it('still reads a bare quantifier keyword at expression position as a quantifier', () => {
    const result = parse('all in orders { is-positive }');
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({ asAllSatisfied: { spec: 'is-positive' }, path: 'orders' });
  });

  it('resolves a positional argument to its declared name', () => {
    const result = parse('at-least(2)', { catalog: CATALOG });
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({ spec: 'at-least', args: { floor: 2 } });
  });

  it('resolves positional arguments before named ones', () => {
    const result = parse('at-least(2, label = "high")', { catalog: CATALOG });
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({ spec: 'at-least', args: { floor: 2, label: 'high' } });
  });

  it('resolves a positional boolean argument to its declared name', () => {
    const result = parse('at-least(true)', { catalog: CATALOG });
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({ spec: 'at-least', args: { floor: true } });
  });

  it('resolves positional null and string arguments to their declared names', () => {
    const result = parse('at-least(null, "x")', { catalog: CATALOG });
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({ spec: 'at-least', args: { floor: null, label: 'x' } });
  });
});

describe('parse — binary operators', () => {
  it('maps each operator to its node kind', () => {
    expect(parse('a & b').document).toEqual({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    expect(parse('a | b').document).toEqual({ rule: { or: [{ spec: 'a' }, { spec: 'b' }] } });
    expect(parse('a ^ b').document).toEqual({ rule: { xor: [{ spec: 'a' }, { spec: 'b' }] } });
    expect(parse('a && b').document).toEqual({ rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }] } });
    expect(parse('a || b').document).toEqual({ rule: { orElse: [{ spec: 'a' }, { spec: 'b' }] } });
  });

  it('flattens a run of the same operator into one n-ary node', () => {
    expect(parse('a && b && c').document).toEqual({
      rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] },
    });
  });

  it('binds & tighter than &&', () => {
    expect(parse('a & b && c').document).toEqual({
      rule: { andAlso: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] },
    });
  });

  it('binds | tighter than || and &&', () => {
    expect(parse('a | b && c').document).toEqual({
      rule: { andAlso: [{ or: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] },
    });
  });

  it('binds ^ tighter than | and looser than &', () => {
    expect(parse('a & b ^ c | d').document).toEqual({
      rule: { or: [{ xor: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }, { spec: 'd' }] },
    });
  });

  it('binds ! tighter than any binary operator', () => {
    expect(parse('!a && b').document).toEqual({
      rule: { andAlso: [{ not: { spec: 'a' } }, { spec: 'b' }] },
    });
  });

  it('lets parentheses override precedence', () => {
    expect(parse('a && (b | c)').document).toEqual({
      rule: { andAlso: [{ spec: 'a' }, { or: [{ spec: 'b' }, { spec: 'c' }] }] },
    });
  });

  it('paths operands by operator and index', () => {
    const spans = parse('a && b').spans;
    expect(spans.map((s) => s.path)).toEqual([
      '$.rule', '$.rule.andAlso[0]', '$.rule.andAlso[1]',
    ]);
  });
});

describe('parse — quantifiers', () => {
  it('parses all/any into asAllSatisfied/asAnySatisfied with a path', () => {
    expect(parse('all in orders { is-positive }').document).toEqual({
      rule: { asAllSatisfied: { spec: 'is-positive' }, path: 'orders' },
    });
    expect(parse('any in orders { is-positive }').document).toEqual({
      rule: { asAnySatisfied: { spec: 'is-positive' }, path: 'orders' },
    });
  });

  it('parses counted quantifiers with a literal n', () => {
    expect(parse('exactly(2) in orders { is-positive }').document).toEqual({
      rule: { asNSatisfied: { spec: 'is-positive' }, n: 2, path: 'orders' },
    });
    expect(parse('atLeast(3) in orders { is-positive }').document).toEqual({
      rule: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: 3, path: 'orders' },
    });
    expect(parse('atMost(1) in orders { is-positive }').document).toEqual({
      rule: { asAtMostNSatisfied: { spec: 'is-positive' }, n: 1, path: 'orders' },
    });
  });

  it('parses a param reference as the countable n, keeping the @ sigil', () => {
    expect(parse('atLeast(@minOrders) in orders { is-positive }').document).toEqual({
      rule: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: '@minOrders', path: 'orders' },
    });
  });

  it('parses a compound quantifier body', () => {
    expect(parse('atLeast(2) in orders { is-positive && is-recent }').document).toEqual({
      rule: {
        asAtLeastNSatisfied: { andAlso: [{ spec: 'is-positive' }, { spec: 'is-recent' }] },
        n: 2,
        path: 'orders',
      },
    });
  });

  it('paths the quantifier body under its node key', () => {
    const spans = parse('all in orders { is-positive }').spans;
    expect(spans.map((s) => s.path)).toEqual(['$.rule', '$.rule.asAllSatisfied']);
  });
});

describe('parse — parameters', () => {
  it('parses a declaration with a default', () => {
    expect(parse('param minOrders: integer = 3\n\nis-active').document).toEqual({
      parameters: { minOrders: { type: 'integer', default: 3 } },
      rule: { spec: 'is-active' },
    });
  });

  it('parses a declaration without a default', () => {
    expect(parse('param label: string\n\nis-active').document).toEqual({
      parameters: { label: { type: 'string' } },
      rule: { spec: 'is-active' },
    });
  });

  it('parses several declarations', () => {
    const document = parse('param a: integer = 1\nparam b: boolean = true\n\nis-active').document;
    expect(document?.parameters).toEqual({
      a: { type: 'integer', default: 1 },
      b: { type: 'boolean', default: true },
    });
  });

  it('parses string and number defaults', () => {
    const document = parse('param a: string = "gold"\nparam b: number = 2\n\nis-active').document;
    expect(document?.parameters).toEqual({
      a: { type: 'string', default: 'gold' },
      b: { type: 'number', default: 2 },
    });
  });

  it('offsets node spans past the parameter block', () => {
    expect(parse('param n: integer = 1\n\nis-active').spans).toEqual([
      { path: '$.rule', from: 22, to: 31 },
    ]);
  });
});

describe('parse — span uniqueness', () => {
  it('records one span per path, the group superseding the node it wraps', () => {
    expect(parse('(is-active)').spans).toEqual([{ path: '$.rule', from: 0, to: 11 }]);
  });

  it('collapses redundant nested groups to a single span', () => {
    expect(parse('((a))').spans).toEqual([{ path: '$.rule', from: 0, to: 5 }]);
  });
});

describe('parse — span invariants', () => {
  const sources = [
    'a', '!a', '!!a', '(a)', '((a))', 'a && b && c', 'a & b ^ c | d',
    'a || b && c | d ^ e & !f', '(a && b) || c', 'a && (b | c)',
    '`n > 0` && a', 'all in orders { is-positive }', 'atLeast(2) in orders { a && b }',
    'atLeast(@m) in o { a }', 'any in o { all in p { a || b } } && c', '!(a && b)',
    'param n: integer = 1\n\na && b',
  ];

  it.each(sources)('spans %j with exactly one entry per node path', (source) => {
    const result = parse(source);
    expect(result.errors).toEqual([]);
    const nodePaths = listPaths(result.document!).map((entry) => entry.path).sort();
    expect(result.spans.map((span) => span.path).sort()).toEqual(nodePaths);
  });

  it.each(sources)('nests every child span inside its parent for %j', (source) => {
    const byPath = new Map(parse(source).spans.map((span) => [span.path, span]));
    for (const [path, span] of byPath) {
      const parent = byPath.get(path.replace(/\.[A-Za-z]+(\[\d+\])?$/, ''));
      if (!parent || parent === span) continue;
      expect({ path, from: parent.from <= span.from, to: parent.to >= span.to })
        .toEqual({ path, from: true, to: true });
    }
  });
});

describe('parse — prototype-unsafe parameter names', () => {
  it('keeps a `__proto__` parameter as an own property without mutating the prototype', () => {
    const parameters = parse('param __proto__: integer = 1\n\nis-active').document?.parameters;
    expect(Object.keys(parameters ?? {})).toEqual(['__proto__']);
    expect(Object.getOwnPropertyDescriptor(parameters, '__proto__')?.value).toEqual({
      type: 'integer',
      default: 1,
    });
    expect(Object.getPrototypeOf(parameters ?? {})).toBe(Object.prototype);
  });
});

describe('parse — negative parameter defaults', () => {
  it('parses a negative integer and a negative number default', () => {
    const document = parse('param a: integer = -1\nparam b: number = -2\n\nis-active').document;
    expect(document?.parameters).toEqual({
      a: { type: 'integer', default: -1 },
      b: { type: 'number', default: -2 },
    });
  });
});

describe('parse — let declarations', () => {
  it('ends a let body at the line end, so a rule that opens with a group is not read as its arguments', () => {
    const result = parse('let a = x\n\n(y & z) & a');
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual({
      definitions: { a: { rule: { spec: 'x' } } },
      rule: { and: [{ and: [{ spec: 'y' }, { spec: 'z' }] }, { local: 'a' }] },
    });
  });

  it('reads an argument list only when it opens on the spec\'s own line', () => {
    expect(parse('s(n = 1)').document).toEqual({ rule: { spec: 's', args: { n: 1 } } });
    expect(parse('s (n = 1)').document).toEqual({ rule: { spec: 's', args: { n: 1 } } });
    const split = parse('s\n(n = 1)');
    expect(split.document).toBeUndefined();
    expect(split.errors[0]).toMatchObject({ code: 'UnexpectedToken' });
  });

  it('declares a local and references it in the rule', () => {
    const result = parse('let a = x && y\n\na & z');
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual({
      definitions: { a: { rule: { andAlso: [{ spec: 'x' }, { spec: 'y' }] } } },
      rule: { and: [{ local: 'a' }, { spec: 'z' }] },
    });
  });

  it('allows a let to reference a let declared after it', () => {
    const result = parse('let a = b\n\nlet b = x\n\na');
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual({
      definitions: {
        a: { rule: { local: 'b' } },
        b: { rule: { spec: 'x' } },
      },
      rule: { local: 'a' },
    });
  });

  it('allows param before let', () => {
    const result = parse('param n: integer = 1\n\nlet a = x\n\na');
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual({
      parameters: { n: { type: 'integer', default: 1 } },
      definitions: { a: { rule: { spec: 'x' } } },
      rule: { local: 'a' },
    });
  });

  it('reports param after let as an unexpected token at the param', () => {
    const result = parse('let a = x\n\nparam n: integer = 1\n\na');
    expect(result.document).toBeUndefined();
    const paramIndex = 'let a = x\n\n'.length;
    expect(result.errors).toContainEqual(
      expect.objectContaining({ code: 'UnexpectedToken', from: paramIndex }),
    );
  });

  it('leaves an undeclared bare word as a spec reference', () => {
    const result = parse('let a = x\n\nb');
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual({
      definitions: { a: { rule: { spec: 'x' } } },
      rule: { spec: 'b' },
    });
  });

  it('records a span for a definition body under its definitionBodyPath', () => {
    const result = parse('let a = x && y\n\na');
    expect(result.errors).toEqual([]);
    expect(result.spans).toContainEqual(
      expect.objectContaining({ path: '$.definitions.a.rule' }),
    );
  });
});

describe('parse — warnings', () => {
  it('always carries a warnings array, empty for a plain document', () => {
    const result = parse('is-active');
    expect(result.warnings).toEqual([]);
  });

  it('warns without suppressing the document — warnings are not errors', () => {
    const text = 'let plain = x\n\nplain';
    const result = parse(text, { catalog: CATALOG });
    expect(result.errors).toEqual([]);
    expect(result.document).toBeDefined();
    expect(result.warnings.length).toBeGreaterThan(0);
  });

  it('reports ShadowsCatalog at the local declaration name when it names a catalog spec', () => {
    const text = 'let plain = x\n\nplain';
    const result = parse(text, { catalog: CATALOG });
    const nameFrom = text.indexOf('plain');
    expect(result.warnings).toContainEqual({
      code: 'ShadowsCatalog',
      message: "shadows catalog proposition 'plain'",
      from: nameFrom,
      to: nameFrom + 'plain'.length,
    });
  });

  it('does not warn when a local name is not in the catalog', () => {
    const result = parse('let a = x\n\na', { catalog: CATALOG });
    expect(result.warnings).toEqual([]);
  });

});
