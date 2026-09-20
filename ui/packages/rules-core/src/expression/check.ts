import type { RuleErrorCode } from '../contracts.js';
import type { JsonSchema } from '../contracts.js';
import { isIntegral, join, kindName, kindOf, type NumericKind } from './lattice.js';
import { parseLeaf, printLeaf, type LeafAst } from './parse.js';
import { elementOf, isCollection, isNullable, typeName, withVar, type LeafScope } from './scope.js';
import type { LeafProblem } from './tokenize.js';

const COLLECTION_METHODS = ['where', 'any', 'all', 'count', 'sum', 'min', 'max'];

/** A type during checking: a resolved schema-ish description, or a numeric type variable. */
interface TypeVar { parent?: TypeVar; fractional: boolean; paramKind?: 'integer' | 'number'; resolved?: NumericKind; resolvedBy?: string; members: LeafAst[] }
type Known = { kind: 'bool' | 'string' | 'null' | 'object'; nullable: boolean; schema?: JsonSchema }
  | { kind: 'numeric'; numeric: NumericKind; nullable: boolean; schema?: JsonSchema }
  | { kind: 'collection'; nullable: boolean; schema: JsonSchema };
type LeafType = { known: Known } | { var: TypeVar } | { unknown: true };

export interface LeafFactLocal { node: LeafAst; type: string; from: string | null }
export interface LeafAnalysis { problems: LeafProblem[]; types: Map<LeafAst, string>; facts: LeafFactLocal[]; valid: boolean }

function root(v: TypeVar): TypeVar { return v.parent ? root(v.parent) : v; }
function allows(v: TypeVar, kind: NumericKind): boolean {
  if (v.fractional && isIntegral(kind)) return false;
  if (v.paramKind === 'integer') return kind === 'int32' || kind === 'int64' || kind === 'decimal';
  if (v.paramKind === 'number') return kind === 'decimal' || kind === 'double' || kind === 'single';
  return true;
}

/** Whether a node is a numeric literal whose value is zero, in any spelling. */
function isZeroLiteral(node: LeafAst): boolean {
  return node.kind === 'num' && Number(node.text) === 0;
}

function describe(t: Known): string {
  const base = t.kind === 'numeric' ? kindName(t.numeric)
    : t.kind === 'bool' ? 'a condition'
    : t.kind === 'collection' ? `a collection of ${typeName(t.schema.items)}`
    : t.kind;
  return base;
}

/**
 * The type string the corpus compares: `decimal`, `bool`, `string`, `collection`… Mirrors C#'s
 * `DescribeType`, which only lifts value types to nullable form (`Nullable<T>`) — a reference
 * type (string, object, collection) is always nullable and never carries a `?` suffix.
 */
function typeString(t: Known): string {
  const base = t.kind === 'numeric' ? kindName(t.numeric) : t.kind;
  const isValueKind = t.kind === 'numeric' || t.kind === 'bool';
  return isValueKind && t.nullable ? `${base}?` : base;
}

function fromSchema(schema: JsonSchema, throughNullable = false): Known {
  const nullable = throughNullable || isNullable(schema);
  if (isCollection(schema)) return { kind: 'collection', nullable, schema };
  const numeric = kindOf(schema);
  if (numeric) return { kind: 'numeric', numeric, nullable, schema };
  const type = Array.isArray(schema.type) ? schema.type.find((x) => x !== 'null') : schema.type;
  if (type === 'boolean') return { kind: 'bool', nullable, schema };
  if (type === 'string') return { kind: 'string', nullable, schema };
  return { kind: 'object', nullable, schema };
}

class Checker {
  readonly problems: LeafProblem[] = [];
  readonly types = new Map<LeafAst, LeafType>();
  readonly vars: TypeVar[] = [];
  /**
   * Vars whose type was never pinned down because an *unrelated* error already fired on this
   * expression — mirrors C#'s `_errored`. `finish` skips these when defaulting unresolved vars,
   * so a var with no genuine anchor to blame doesn't also draw a second "no model field fixes
   * this" warning on top of the real problem.
   */
  readonly errored = new Set<TypeVar>();

  constructor(private readonly scope: LeafScope) {}

  private report(at: { from: number; to: number }, code: RuleErrorCode, message: string, warning = false): void {
    this.problems.push({ code, message, from: at.from, to: at.to, ...(warning ? { warning: true as const } : {}) });
  }
  private set(node: LeafAst, type: LeafType): LeafType { this.types.set(node, type); return type; }
  private fail(node: LeafAst, code: RuleErrorCode, message: string, at: { from: number; to: number } = node): LeafType {
    this.report(at, code, message);
    return this.set(node, { unknown: true });
  }
  private newVar(node: LeafAst, fractional: boolean, paramKind?: 'integer' | 'number'): LeafType {
    const v: TypeVar = { fractional, members: [node], ...(paramKind ? { paramKind } : {}) };
    this.vars.push(v);
    return this.set(node, { var: v });
  }
  private known(t: LeafType): Known | undefined {
    if ('known' in t) return t.known;
    const resolved = 'var' in t ? root(t.var).resolved : undefined;
    return resolved ? { kind: 'numeric', numeric: resolved, nullable: false } : undefined;
  }

  visit(node: LeafAst, scope: LeafScope): LeafType {
    switch (node.kind) {
      case 'num': return this.newVar(node, node.text.includes('.'));
      case 'str': return this.set(node, { known: { kind: 'string', nullable: false } });
      case 'bool': return this.set(node, { known: { kind: 'bool', nullable: false } });
      case 'null': return this.set(node, { known: { kind: 'null', nullable: true } });
      case 'param': {
        const p = scope.parameters[node.name];
        if (!p) return this.fail(node, 'UnknownField', `unknown parameter '@${node.name}'`);
        if (p.type === 'integer' || p.type === 'number') return this.newVar(node, false, p.type);
        return this.set(node, { known: { kind: p.type === 'boolean' ? 'bool' : 'string', nullable: false } });
      }
      case 'ident': {
        const v = scope.vars[node.name];
        if (v) return this.set(node, { known: fromSchema(v.schema) });
        const field = scope.model.properties?.[node.name];
        if (!field) return this.fail(node, 'UnknownField', `'${node.name}' is not a field of ${scope.modelName}`);
        return this.set(node, { known: fromSchema(field) });
      }
      case 'member': {
        const target = this.known(this.visit(node.target, scope));
        if (!target) return this.set(node, { unknown: true });
        const at = { from: node.nameFrom, to: node.nameTo };
        if (target.kind === 'collection') return this.fail(node, 'UnknownMethod', `'${printLeaf(node.target)}' is a collection — use .where(…), .any(…), .all(…), .count(), .sum(…), .min(…) or .max(…) on it`, at);
        if (target.kind !== 'object') return this.fail(node, 'UnknownField', `'${node.name}' is not a field of ${describe(target)}`, at);
        const field = target.schema?.properties?.[node.name];
        if (!field) return this.fail(node, 'UnknownField', `'${node.name}' is not a field of ${typeName(target.schema)}`, at);
        return this.set(node, { known: fromSchema(field, target.nullable) });
      }
      case 'call': return this.visitCall(node, scope);
      case 'lambda': return this.fail(node, 'InvalidExpression', 'a lambda is only allowed as a method argument');
      case 'unary': {
        const operand = this.visit(node.operand, scope);
        const k = this.known(operand);
        if (node.op === '!') {
          if (k && k.kind !== 'bool') this.report(node, 'ExpressionTypeMismatch', `'!' needs a condition; this is ${describe(k)}`);
          return this.set(node, { known: { kind: 'bool', nullable: false } });
        }
        if (k && k.kind !== 'numeric') this.report(node, 'ExpressionTypeMismatch', `'-' needs a number; this is ${describe(k)}`);
        return this.set(node, operand);
      }
      case 'binary': return this.visitBinary(node, scope);
      default: return this.set(node, { unknown: true });
    }
  }

  private visitCall(node: Extract<LeafAst, { kind: 'call' }>, scope: LeafScope): LeafType {
    const target = this.known(this.visit(node.target, scope));
    if (!target) return this.set(node, { unknown: true });
    const at = { from: node.methodFrom, to: node.methodTo };

    if (node.method === 'equalsIgnoreCase') {
      if (target.kind !== 'string') return this.fail(node, 'UnknownMethod', `'equalsIgnoreCase' needs a string; this is ${describe(target)}`, at);
      const arg = node.args[0];
      if (node.args.length !== 1 || !arg || arg.kind === 'lambda') return this.fail(node, 'UnknownMethod', "'equalsIgnoreCase' takes one string argument", at);
      const argument = this.visit(arg, scope);
      const argType = this.known(argument);
      if (argType && argType.kind !== 'string') this.report(arg, 'ExpressionTypeMismatch', `'equalsIgnoreCase' expects a string; this is ${describe(argType)}`);
      else if ('var' in argument) {
        // A numeric literal or parameter has no anchor here; it is wrong, not merely unresolved.
        this.report(arg, 'ExpressionTypeMismatch', "'equalsIgnoreCase' expects a string; this is a number");
        this.errored.add(root(argument.var));
      }
      return this.set(node, { known: { kind: 'bool', nullable: false } });
    }

    if (target.kind !== 'collection') return this.fail(node, 'UnknownMethod', `'.${node.method}()' needs a collection; this is ${describe(target)}`, at);
    if (!COLLECTION_METHODS.includes(node.method)) return this.fail(node, 'UnknownMethod', `unknown method '${node.method}'; the collection methods are where, any, all, count, sum, min, max`, at);
    const element = elementOf(target.schema)!;

    if (node.method === 'count') {
      if (node.args[0]) this.report(node.args[0], 'UnknownMethod', "'count()' takes no arguments — filter with .where(…) first");
      return this.set(node, { known: { kind: 'numeric', numeric: 'int32', nullable: target.nullable } });
    }

    const lambda = node.args[0];
    if (node.args.length !== 1 || !lambda || lambda.kind !== 'lambda') return this.fail(node, 'UnknownMethod', `'${node.method}' takes a lambda: ${node.method}(x => …)`, at);
    const inner = withVar(scope, lambda.param, element, printLeaf(node.target));
    this.set(lambda, { known: fromSchema(element) });
    const body = this.visit(lambda.body, inner);
    const bodyType = this.known(body);

    if (node.method === 'where' || node.method === 'any' || node.method === 'all') {
      if (bodyType && bodyType.kind !== 'bool') {
        this.report(lambda.body, 'ExpressionTypeMismatch', `'${node.method}' expects a condition; this is ${describe(bodyType)}`);
        return this.set(node, { unknown: true });
      }
      return this.set(node, node.method === 'where' ? { known: target } : { known: { kind: 'bool', nullable: target.nullable } });
    }
    if (bodyType && bodyType.kind !== 'numeric') {
      this.report(lambda.body, 'ExpressionTypeMismatch', `'${node.method}' expects a number; this is ${describe(bodyType)}`);
      return this.set(node, { unknown: true });
    }
    if ('var' in body) return this.set(node, body);
    if (!bodyType) return this.set(node, { unknown: true });
    return this.set(node, { known: { ...bodyType, nullable: bodyType.nullable || target.nullable } });
  }

  private visitBinary(node: Extract<LeafAst, { kind: 'binary' }>, scope: LeafScope): LeafType {
    const left = this.visit(node.left, scope);
    const right = this.visit(node.right, scope);
    const bool: LeafType = { known: { kind: 'bool', nullable: false } };

    if (node.op === '&&' || node.op === '||') {
      for (const [side, type] of [[node.left, left], [node.right, right]] as const) {
        const k = this.known(type);
        if (k && k.kind !== 'bool') this.report(side, 'ExpressionTypeMismatch', `'${node.op}' needs conditions on both sides; this is ${describe(k)}`);
        else if ('var' in type) this.report(side, 'ExpressionTypeMismatch', `'${node.op}' needs conditions on both sides; this is a number`);
      }
      return this.set(node, bool);
    }

    if (node.op === '==' || node.op === '!=') {
      if (node.left.kind === 'null' || node.right.kind === 'null') {
        const [other, otherNode] = node.left.kind === 'null' ? [right, node.right] : [left, node.left];
        const k = this.known(other);
        if ('var' in other || (k && !k.nullable)) this.report(otherNode, 'ExpressionTypeMismatch', `'${printLeaf(otherNode)}' is never null`, true);
        return this.set(node, bool);
      }
      const l = this.known(left); const r = this.known(right);
      if (l && r && l.kind !== 'numeric' && r.kind !== 'numeric') {
        if (l.kind !== r.kind) this.report(node, 'ExpressionTypeMismatch', `comparing ${describe(l)} with ${describe(r)}`);
        else if (l.schema?.enum && node.right.kind === 'str' && !l.schema.enum.includes(node.right.value)) {
          this.report(node.right, 'ExpressionTypeMismatch', `not one of ${l.schema.enum.map((e) => `"${String(e)}"`).join(', ')}`);
        }
        return this.set(node, bool);
      }
      if ((l && l.kind !== 'numeric') || (r && r.kind !== 'numeric')) {
        const nonNumeric = l && l.kind !== 'numeric' ? l : r!;
        // The other side may still be an unresolved var (e.g. a bare numeric literal) — it has no
        // genuine anchor to blame for this mismatch, so mark it errored like unify() does, or
        // finish() would also pile a spurious "no model field fixes this" default warning on top.
        if ('var' in left) this.errored.add(root(left.var));
        if ('var' in right) this.errored.add(root(right.var));
        this.report(node, 'ExpressionTypeMismatch', `comparing ${describe(nonNumeric)} with a number`);
        return this.set(node, bool);
      }
      this.unify(node, left, right, false);
      return this.set(node, bool);
    }

    if (['<', '<=', '>', '>='].includes(node.op)) {
      this.unify(node, left, right, false);
      return this.set(node, bool);
    }

    const result = this.unify(node, left, right, true);
    // A literal zero divisor is always a DivideByZeroException at evaluation time, so it is an
    // error rather than a warning — and it supersedes the truncation warning, which has nothing
    // to say about a division that can never produce a value.
    if (node.op === '/' && isZeroLiteral(node.right)) {
      this.report(node.right, 'ExpressionTypeMismatch', 'division by zero');
      return this.set(node, result);
    }
    const k = this.known(result);
    const integral = (k && k.kind === 'numeric' && isIntegral(k.numeric)) || ('var' in result && !root(result.var).resolved && !root(result.var).fractional);
    if (node.op === '/' && integral) this.report(node, 'ExpressionTypeMismatch', 'integer division truncates; compare against a fractional value to keep the remainder', true);
    return this.set(node, result);
  }

  private unify(node: Extract<LeafAst, { kind: 'binary' }>, left: LeafType, right: LeafType, arithmetic: boolean): LeafType {
    if ('unknown' in left || 'unknown' in right) {
      // The other side's var has no genuine anchor to blame — an unrelated error already fired
      // on this expression, so don't also pile on a "no model field fixes this" default.
      if ('var' in left) this.errored.add(root(left.var));
      if ('var' in right) this.errored.add(root(right.var));
      return { unknown: true };
    }
    const what = arithmetic ? `'${node.op}' needs numbers` : `'${node.op}' compares numbers`;
    for (const [side, type] of [[node.left, left], [node.right, right]] as const) {
      const k = 'known' in type ? type.known : undefined;
      if (k && k.kind !== 'numeric') { this.report(side, 'ExpressionTypeMismatch', `${what}; this is ${describe(k)}`); return { unknown: true }; }
    }
    const nullable = ('known' in left && left.known.nullable) || ('known' in right && right.known.nullable);

    if ('known' in left && 'known' in right) {
      const lk = (left.known as { numeric: NumericKind }).numeric; const rk = (right.known as { numeric: NumericKind }).numeric;
      const joined = join(lk, rk);
      if (!joined) { this.report(node, 'ExpressionTypeMismatch', `cannot compare ${kindName(lk)} with ${kindName(rk)} without losing precision; use a registered spec for this comparison`); return { unknown: true }; }
      return { known: { kind: 'numeric', numeric: joined, nullable } };
    }

    if ('var' in left && 'var' in right) {
      const a = root(left.var); const c = root(right.var);
      if (a !== c) {
        if (a.resolved && c.resolved) {
          const joined = join(a.resolved, c.resolved);
          if (!joined) {
            this.report(node, 'ExpressionTypeMismatch', `cannot combine ${kindName(a.resolved)} with ${kindName(c.resolved)}`);
            this.errored.add(a); this.errored.add(c);
            return { unknown: true };
          }
          a.resolved = joined;
        } else {
          if (a.resolved === undefined && c.resolved !== undefined) a.resolved = c.resolved;
          if (a.resolvedBy === undefined && c.resolvedBy !== undefined) a.resolvedBy = c.resolvedBy;
        }
        // Two parameters of different declared kinds (integer vs number) have no common integral
        // type that honors both constraints — force the merged var fractional (decimal-only)
        // rather than silently keeping just one side's kind.
        if (a.paramKind && c.paramKind && a.paramKind !== c.paramKind) a.fractional = true;
        a.fractional ||= c.fractional;
        if (a.paramKind === undefined && c.paramKind !== undefined) a.paramKind = c.paramKind;
        a.members.push(...c.members);
        c.parent = a;
      }
      return { var: a };
    }

    const [v, concrete, anchor] = 'var' in left
      ? [root(left.var), (right as { known: { numeric: NumericKind } }).known.numeric, node.right]
      : [root((right as { var: TypeVar }).var), (left as { known: { numeric: NumericKind } }).known.numeric, node.left];
    let target = concrete;
    if (v.fractional && isIntegral(concrete)) target = 'decimal';
    if (!allows(v, target) || (v.resolved && !join(v.resolved, target))) {
      let have = 'this literal';
      if (v.resolved) have = kindName(v.resolved);
      else if (v.paramKind === 'number') have = 'a number parameter';
      this.report(node, 'ExpressionTypeMismatch', `cannot use ${have} with ${kindName(concrete)} without losing precision`);
      this.errored.add(v);
      return { unknown: true };
    }
    v.resolved = v.resolved ? join(v.resolved, target)! : target;
    v.resolvedBy ??= printLeaf(anchor);
    return { known: { kind: 'numeric', numeric: v.resolved, nullable } };
  }

  finish(ast: LeafAst): LeafAnalysis {
    for (const v of new Set(this.vars.map(root))) {
      if (v.resolved) continue;
      if (this.errored.has(v)) continue;
      v.resolved = v.fractional || v.paramKind === 'number' ? 'decimal' : 'int32';
      const first = v.members[0]!;
      this.report(first, 'ExpressionTypeMismatch', `no model field fixes the type of this expression; assuming ${kindName(v.resolved)}`, true);
    }
    const rootType = this.known(this.types.get(ast)!);
    if (rootType && rootType.kind !== 'bool') this.report(ast, 'ExpressionTypeMismatch', `a leaf must be a condition; this is ${describe(rootType)}`);

    const types = new Map<LeafAst, string>();
    const facts: LeafFactLocal[] = [];
    for (const [node, type] of this.types) {
      const k = this.known(type);
      if (!k) continue;
      types.set(node, typeString(k));
      if ((node.kind === 'num' || node.kind === 'param') && 'var' in type) facts.push({ node, type: kindName(root(type.var).resolved!), from: root(type.var).resolvedBy ?? null });
    }
    if (types.has(ast)) facts.push({ node: ast, type: types.get(ast)!, from: null });
    return { problems: this.problems, types, facts, valid: this.problems.every((p) => p.warning) };
  }
}

export function checkLeaf(ast: LeafAst, scope: LeafScope): LeafAnalysis {
  const checker = new Checker(scope);
  checker.visit(ast, scope);
  return checker.finish(ast);
}

/** Parse then check; a parse problem yields an invalid analysis with that one problem. */
export function analyseLeaf(text: string, scope: LeafScope): LeafAnalysis & { ast?: LeafAst } {
  const { ast, problems } = parseLeaf(text);
  if (!ast) return { problems, types: new Map(), facts: [], valid: false };
  return { ast, ...checkLeaf(ast, scope) };
}
