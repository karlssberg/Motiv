import { describe, it, expect } from 'vitest';
import { parseLeaf, printLeaf, tokenizeLeaf } from '../src/expression/index.js';

describe('tokenizeLeaf', () => {
  it('classifies every token kind with offsets', () => {
    const { tokens, problems } = tokenizeLeaf('orders.where(o => o.status == "paid").sum(o => o.total) > @vip');
    expect(problems).toEqual([]);
    expect(tokens.map((t) => t.kind)).toEqual([
      'ident', 'punct', 'ident', 'punct', 'ident', 'op', 'ident', 'punct', 'ident', 'op', 'string', 'punct',
      'punct', 'ident', 'punct', 'ident', 'op', 'ident', 'punct', 'ident', 'punct', 'op', 'param', 'end',
    ]);
    expect(tokens[22]).toEqual({ kind: 'param', value: '@vip', from: 58, to: 62 });
  });

  it('reports an unterminated string and an unknown character', () => {
    expect(tokenizeLeaf('country == "SE').problems[0]).toMatchObject({ code: 'InvalidExpression', from: 11, to: 14 });
    expect(tokenizeLeaf('age # 3').problems[0]).toMatchObject({ from: 4, to: 5 });
  });
});

describe('parseLeaf', () => {
  it('parses precedence and ranges', () => {
    const { ast, problems } = parseLeaf('age * 12 + 1 >= @min && isActive');
    expect(problems).toEqual([]);
    expect(ast).toMatchObject({
      kind: 'binary', op: '&&', from: 0, to: 32,
      left: { kind: 'binary', op: '>=', left: { kind: 'binary', op: '+', left: { kind: 'binary', op: '*' } }, right: { kind: 'param', name: 'min' } },
      right: { kind: 'ident', name: 'isActive' },
    });
  });

  it('parses method chains with lambdas', () => {
    const { ast } = parseLeaf('orders.where(o => o.status == "paid").sum(o => o.total) > 1000');
    expect(ast).toMatchObject({
      kind: 'binary', op: '>',
      left: { kind: 'call', method: 'sum', methodFrom: 38, methodTo: 41, args: [{ kind: 'lambda', param: 'o', body: { kind: 'member', name: 'total' } }],
        target: { kind: 'call', method: 'where', target: { kind: 'ident', name: 'orders' } } },
      right: { kind: 'num', text: '1000' },
    });
  });

  it.each([
    ['', 'empty expression', 0, 0],
    ['age >', 'unexpected end of expression', 5, 5],
    ['age > > 1', "unexpected '>'", 6, 7],
    ['orders.', "expected a field or method name after '.'", 7, 7],
    ['a == b == c', "unexpected '=='", 7, 9],
  ])('reports %j at its range', (text, message, from, to) => {
    const { ast, problems } = parseLeaf(text);
    expect(ast).toBeUndefined();
    expect(problems).toEqual([{ code: 'InvalidExpression', message, from, to }]);
  });

  it('prints the canonical text', () => {
    const { ast } = parseLeaf('orders.where( o=>o.status=="paid" ).sum(o => o.total)>@vip');
    expect(printLeaf(ast!)).toBe('orders.where(o => o.status == "paid").sum(o => o.total) > @vip');
  });
});
