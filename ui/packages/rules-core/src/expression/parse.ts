import { tokenizeLeaf, type LeafProblem, type LeafToken } from './tokenize.js';

interface Span { from: number; to: number }

/** The parsed shape of a leaf expression — a discriminated union over `kind`, each variant carrying its source range. */
export type LeafAst = Span & (
  | { kind: 'num'; text: string }
  | { kind: 'str'; value: string }
  | { kind: 'bool'; value: boolean }
  | { kind: 'null' }
  | { kind: 'param'; name: string }
  | { kind: 'ident'; name: string }
  | { kind: 'member'; target: LeafAst; name: string; nameFrom: number; nameTo: number }
  | { kind: 'call'; target: LeafAst; method: string; args: LeafAst[]; methodFrom: number; methodTo: number }
  | { kind: 'lambda'; param: string; body: LeafAst }
  | { kind: 'binary'; op: string; left: LeafAst; right: LeafAst }
  | { kind: 'unary'; op: string; operand: LeafAst }
);

class LeafSyntaxError extends Error {
  constructor(message: string, public readonly token: LeafToken) { super(message); }
}

class Parser {
  private index = 0;
  constructor(private readonly tokens: LeafToken[]) {}
  private peek(offset = 0): LeafToken { return this.tokens[Math.min(this.index + offset, this.tokens.length - 1)]!; }
  private at(value: string): boolean { const t = this.peek(); return t.kind !== 'string' && t.value === value; }
  private next(): LeafToken {
    const t = this.peek();
    if (t.kind === 'end') throw new LeafSyntaxError('unexpected end of expression', t);
    this.index++;
    return t;
  }
  private expect(value: string): LeafToken {
    const t = this.peek();
    if (t.kind === 'end') throw new LeafSyntaxError('unexpected end of expression', t);
    if (t.value !== value) throw new LeafSyntaxError(`expected '${value}'`, t);
    return this.next();
  }
  parse(): LeafAst {
    const ast = this.or();
    const rest = this.peek();
    if (rest.kind !== 'end') throw new LeafSyntaxError(`unexpected '${rest.value}'`, rest);
    return ast;
  }
  private level(ops: string[], below: () => LeafAst, once = false): LeafAst {
    let left = below();
    while (this.peek().kind === 'op' && ops.includes(this.peek().value)) {
      const op = this.next().value;
      const right = below();
      left = { kind: 'binary', op, left, right, from: left.from, to: right.to };
      if (once) break;
    }
    return left;
  }
  private or(): LeafAst { return this.level(['||'], () => this.and()); }
  private and(): LeafAst { return this.level(['&&'], () => this.eq()); }
  private eq(): LeafAst { return this.level(['==', '!='], () => this.cmp(), true); }
  private cmp(): LeafAst { return this.level(['<', '<=', '>', '>='], () => this.add(), true); }
  private add(): LeafAst { return this.level(['+', '-'], () => this.mul()); }
  private mul(): LeafAst { return this.level(['*', '/'], () => this.unary()); }
  private unary(): LeafAst {
    if (this.at('!') || this.at('-')) {
      const op = this.next();
      const operand = this.unary();
      return { kind: 'unary', op: op.value, operand, from: op.from, to: operand.to };
    }
    return this.postfix();
  }
  private postfix(): LeafAst {
    let node = this.primary();
    while (this.at('.')) {
      this.next();
      const name = this.peek();
      if (name.kind !== 'ident') throw new LeafSyntaxError("expected a field or method name after '.'", name);
      this.next();
      if (this.at('(')) {
        this.next();
        const args: LeafAst[] = [];
        while (!this.at(')')) {
          args.push(this.lambdaOrExpr());
          if (this.at(',')) this.next(); else break;
        }
        const close = this.expect(')');
        node = { kind: 'call', target: node, method: name.value, args, methodFrom: name.from, methodTo: name.to, from: node.from, to: close.to };
      } else {
        node = { kind: 'member', target: node, name: name.value, nameFrom: name.from, nameTo: name.to, from: node.from, to: name.to };
      }
    }
    return node;
  }
  private lambdaOrExpr(): LeafAst {
    const t = this.peek();
    if (t.kind === 'ident' && this.peek(1).value === '=>') {
      this.next(); this.next();
      const body = this.or();
      return { kind: 'lambda', param: t.value, body, from: t.from, to: body.to };
    }
    return this.or();
  }
  private primary(): LeafAst {
    const t = this.next();
    switch (t.kind) {
      case 'number': return { kind: 'num', text: t.value, from: t.from, to: t.to };
      case 'string': return { kind: 'str', value: t.value.slice(1, -1), from: t.from, to: t.to };
      case 'keyword': return t.value === 'null' ? { kind: 'null', from: t.from, to: t.to } : { kind: 'bool', value: t.value === 'true', from: t.from, to: t.to };
      case 'param': return { kind: 'param', name: t.value.slice(1), from: t.from, to: t.to };
      case 'ident': return { kind: 'ident', name: t.value, from: t.from, to: t.to };
      case 'punct':
        if (t.value === '(') {
          const inner = this.or();
          const close = this.expect(')');
          return { ...inner, from: t.from, to: close.to };
        }
        break;
      default: break;
    }
    throw new LeafSyntaxError(`unexpected '${t.value}'`, t);
  }
}

/** Parses a leaf; on a syntax problem `ast` is absent and `problems` has exactly one entry. */
export function parseLeaf(text: string): { ast?: LeafAst; problems: LeafProblem[] } {
  const { tokens, problems } = tokenizeLeaf(text);
  if (problems.length) return { problems };
  if (tokens.length === 1) return { problems: [{ code: 'InvalidExpression', message: 'empty expression', from: 0, to: 0 }] };
  try {
    return { ast: new Parser(tokens).parse(), problems: [] };
  } catch (error) {
    if (error instanceof LeafSyntaxError) return { problems: [{ code: 'InvalidExpression', message: error.message, from: error.token.from, to: error.token.to }] };
    throw error;
  }
}

/** The canonical text of a leaf — what the server names as a fact's anchor. */
export function printLeaf(node: LeafAst): string {
  switch (node.kind) {
    case 'num': return node.text;
    case 'str': return `"${node.value}"`;
    case 'bool': return String(node.value);
    case 'null': return 'null';
    case 'param': return `@${node.name}`;
    case 'ident': return node.name;
    case 'member': return `${printLeaf(node.target)}.${node.name}`;
    case 'call': return `${printLeaf(node.target)}.${node.method}(${node.args.map(printLeaf).join(', ')})`;
    case 'lambda': return `${node.param} => ${printLeaf(node.body)}`;
    case 'unary': return `${node.op}${printLeaf(node.operand)}`;
    case 'binary': return `${printLeaf(node.left)} ${node.op} ${printLeaf(node.right)}`;
    default: return '';
  }
}
