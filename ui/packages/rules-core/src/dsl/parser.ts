import type { ArgValue, ParameterDeclaration, RuleDocument, RuleNode } from '../document.js';
import { tokenize } from './lexer.js';
import type { DslError, NodeSpan, ParseResult, Token } from './types.js';

const ROOT = '$.rule';

/**
 * Strips the delimiters off a consumed string or expression token, reporting a literal that ran
 * to end-of-input without its closing delimiter. A lone delimiter opens a literal it never
 * closes, so it is unterminated despite ending in the delimiter.
 */
function literalValue(state: ParserState, token: Token, delimiter: string, code: string): string {
  const terminated = token.value.length > 1 && token.value.endsWith(delimiter);
  if (!terminated) {
    state.error(code, `expected a closing \`${delimiter}\``, token);
  }
  return token.value.slice(1, terminated ? -1 : undefined);
}

/** A parse in progress: the token cursor plus the accumulating spans and errors. */
class ParserState {
  readonly tokens: Token[];
  readonly spans: NodeSpan[] = [];
  readonly errors: DslError[] = [];
  index = 0;

  constructor(readonly text: string) {
    this.tokens = tokenize(text);
  }

  peek(offset = 0): Token | undefined { return this.tokens[this.index + offset]; }
  next(): Token | undefined { return this.tokens[this.index++]; }
  get atEnd(): boolean { return this.index >= this.tokens.length; }

  /** The offset just past the last consumed token — the end of whatever was parsed. */
  get lastEnd(): number { return this.tokens[this.index - 1]?.to ?? this.text.length; }

  error(code: string, message: string, token?: Token): void {
    const from = token?.from ?? this.lastEnd;
    const to = token?.to ?? this.text.length;
    this.errors.push({ from, to, code, message });
  }

  span(path: string, from: number, to: number): void {
    this.spans.push({ path, from, to });
  }

  /** Removes spans recorded at exactly `path` since `mark`, so a wider one can supersede them. */
  dropSpansAt(path: string, mark: number): void {
    for (let i = this.spans.length - 1; i >= mark; i--) {
      if (this.spans[i]!.path === path) this.spans.splice(i, 1);
    }
  }
}

/** Consumes a trailing `as "name"` clause, returning the name when present. */
function parseAsClause(state: ParserState): string | undefined {
  const token = state.peek();
  if (!token || token.kind !== 'keyword' || token.value !== 'as') return undefined;
  state.next();
  const nameToken = state.peek();
  if (!nameToken || nameToken.kind !== 'string') {
    state.error('ExpectedName', 'expected a quoted name after `as`', nameToken);
    return undefined;
  }
  state.next();
  return literalValue(state, nameToken, '"', 'UnterminatedString');
}

/** DSL quantifier keyword → higher-order node key. Counted forms take an `(n)` argument. */
const QUANTIFIER_KEYS = {
  all: { key: 'asAllSatisfied', counted: false },
  any: { key: 'asAnySatisfied', counted: false },
  exactly: { key: 'asNSatisfied', counted: true },
  atLeast: { key: 'asAtLeastNSatisfied', counted: true },
  atMost: { key: 'asAtMostNSatisfied', counted: true },
} as const;

type QuantifierWord = keyof typeof QUANTIFIER_KEYS;

/** Consumes `( INT | '@' IDENT )` for a counted quantifier, returning the countable. */
function parseCount(state: ParserState): number | string | undefined {
  const open = state.peek();
  if (!open || open.value !== '(') {
    state.error('ExpectedCount', 'expected `(` and a count', open);
    return undefined;
  }
  state.next();

  const value = state.peek();
  let count: number | string | undefined;
  if (value?.kind === 'number') {
    const parsed = Number(value.value);
    // A count must be a whole number of items: the schema's `countable` is `integer, minimum 0`.
    if (Number.isInteger(parsed) && parsed >= 0) count = parsed;
    else state.error('ExpectedCount', 'expected a whole number of zero or more', value);
    state.next();
  }
  else if (value?.kind === 'paramRef') { count = value.value; state.next(); }
  else state.error('ExpectedCount', 'expected a number or `@parameter`', value);

  const close = state.peek();
  if (!close || close.value !== ')') state.error('ExpectedCount', 'expected `)` after the count', close);
  else state.next();

  return count;
}

/** quantifier := ('all'|'any') 'in' PATH '{' expr '}' | counted '(' N ')' 'in' PATH '{' expr '}' */
function parseQuantifier(state: ParserState, path: string, word: QuantifierWord): RuleNode | undefined {
  const { key, counted } = QUANTIFIER_KEYS[word];
  state.next(); // the quantifier keyword

  const count = counted ? parseCount(state) : undefined;
  if (counted && count === undefined) return undefined;

  const inToken = state.peek();
  if (!inToken || inToken.value !== 'in') {
    state.error('ExpectedIn', 'expected `in` and a collection path', inToken);
    return undefined;
  }
  state.next();

  const pathToken = state.peek();
  if (!pathToken || pathToken.kind !== 'spec') {
    state.error('ExpectedCollection', 'expected a collection path after `in`', pathToken);
    return undefined;
  }
  state.next();

  const open = state.peek();
  if (!open || open.value !== '{') {
    state.error('ExpectedBody', 'expected `{` to open the quantifier body', open);
    return undefined;
  }
  state.next();

  const body = parseExpression(state, `${path}.${key}`);
  if (!body) return undefined;

  const close = state.peek();
  if (!close || close.value !== '}') {
    state.error('UnclosedBody', 'expected `}` to close the quantifier body', open);
  } else {
    state.next();
  }

  const node = counted
    ? { [key]: body, n: count, path: pathToken.value }
    : { [key]: body, path: pathToken.value };
  return node as unknown as RuleNode;
}

/**
 * Reads an argument value literal. Returns `undefined` for an error — distinct from a legitimate
 * `null` value, which is why the caller tests `=== undefined` rather than falsiness.
 */
function parseArgValue(state: ParserState): ArgValue | undefined {
  const token = state.peek();
  if (!token) { state.error('ExpectedArgValue', 'expected an argument value'); return undefined; }
  if (token.kind === 'number') { state.next(); return Number(token.value); }
  if (token.kind === 'string') {
    state.next();
    return literalValue(state, token, '"', 'UnterminatedString');
  }
  if (token.value === 'true') { state.next(); return true; }
  if (token.value === 'false') { state.next(); return false; }
  if (token.value === 'null') { state.next(); return null; }
  state.error('ExpectedArgValue', `\`${token.value}\` is not a valid argument value`, token);
  return undefined;
}

/** args := '(' NAME '=' literal (',' NAME '=' literal)* ')' — absent when no `(` follows. */
function parseArgs(state: ParserState): Record<string, ArgValue> | undefined {
  if (state.peek()?.value !== '(') return undefined;
  const open = state.next()!;
  const args: Record<string, ArgValue> = {};

  for (;;) {
    const name = state.peek();
    if (!name || name.kind !== 'spec') {
      state.error('ExpectedArgName', 'expected an argument name', name);
      return undefined;
    }
    state.next();

    if (state.peek()?.kind !== 'equals') {
      state.error('ExpectedArgValue', 'expected `=` and an argument value', state.peek());
      return undefined;
    }
    state.next();

    const value = parseArgValue(state);
    if (value === undefined) return undefined;

    if (Object.prototype.hasOwnProperty.call(args, name.value)) {
      state.error('DuplicateArg', `duplicate argument \`${name.value}\``, name);
      return undefined;
    }
    // `defineProperty`, not assignment: an argument named `__proto__` would otherwise hit the
    // prototype setter, silently dropping the value and mutating the object.
    Object.defineProperty(args, name.value, {
      value, enumerable: true, writable: true, configurable: true,
    });

    if (state.peek()?.kind === 'comma') { state.next(); continue; }
    break;
  }

  const close = state.peek();
  if (!close || close.value !== ')') {
    state.error('UnclosedArgs', 'expected `)` to close the argument list', open);
  } else {
    state.next();
  }

  return args;
}

/** primary := SPEC | `expr` | '(' expr ')' | quantifier */
function parsePrimary(state: ParserState, path: string): RuleNode | undefined {
  const token = state.peek();
  if (!token) {
    state.error('UnexpectedEnd', 'expected an expression');
    return undefined;
  }

  if (token.kind === 'spec') {
    state.next();
    const args = parseArgs(state);
    return args ? { spec: token.value, args } : { spec: token.value };
  }

  if (token.kind === 'expression') {
    state.next();
    return { expression: literalValue(state, token, '`', 'UnterminatedExpression') };
  }

  if (token.kind === 'paren' && token.value === '(') {
    state.next();
    const mark = state.spans.length;
    const inner = parseExpression(state, path);
    // The group is not a node of its own, so the inner node keeps `path`. Drop the span the
    // inner node recorded — `parsePostfix` re-records a wider one covering the parens and any
    // `as` clause — so each path keeps exactly one span.
    state.dropSpansAt(path, mark);
    const closing = state.peek();
    if (!closing || closing.value !== ')') {
      state.error('UnclosedGroup', 'expected `)` to close this group', token);
    } else {
      state.next();
    }
    return inner;
  }

  if (token.kind === 'quantifier') {
    return parseQuantifier(state, path, token.value as QuantifierWord);
  }

  state.error('UnexpectedToken', `unexpected \`${token.value}\``, token);
  state.next();
  return undefined;
}

/** postfix := primary ('as' STRING)? */
function parsePostfix(state: ParserState, path: string): RuleNode | undefined {
  const start = state.peek()?.from ?? state.lastEnd;
  const node = parsePrimary(state, path);
  if (!node) return undefined;
  const name = parseAsClause(state);
  // A group produces no wrapper node, so `(a as "x") as "y"` has only one node to name:
  // the outer name deliberately supersedes the inner one.
  const decorated = name === undefined ? node : { ...node, name };
  state.span(path, start, state.lastEnd);
  return decorated;
}

/** unary := '!' unary | postfix */
function parseUnary(state: ParserState, path: string): RuleNode | undefined {
  const token = state.peek();
  if (token && token.kind === 'operator' && token.value === '!') {
    const start = token.from;
    state.next();
    const operand = parseUnary(state, `${path}.not`);
    if (!operand) return undefined;
    state.span(path, start, state.lastEnd);
    return { not: operand };
  }
  return parsePostfix(state, path);
}

/** Binary levels, loosest first. Each entry maps its DSL operator to the node key it builds. */
const BINARY_LEVELS = [
  { operator: '||', key: 'orElse' },
  { operator: '&&', key: 'andAlso' },
  { operator: '|', key: 'or' },
  { operator: '^', key: 'xor' },
  { operator: '&', key: 'and' },
] as const;

/** Re-keys spans recorded for the first operand of a run from `oldPath` to `newPath`. */
function repath(state: ParserState, oldPath: string, newPath: string, from: number, to: number): void {
  for (const span of state.spans) {
    if (span.from < from || span.to > to) continue;
    if (span.path === oldPath) span.path = newPath;
    else if (span.path.startsWith(`${oldPath}.`)) {
      span.path = `${newPath}${span.path.slice(oldPath.length)}`;
    }
  }
}

/**
 * Parses one precedence level: the tighter level, then a run of same-operator operands.
 * A run flattens into a single n-ary node, so `a && b && c` is one `andAlso` of three.
 */
function parseBinaryLevel(state: ParserState, path: string, level: number): RuleNode | undefined {
  if (level >= BINARY_LEVELS.length) return parseUnary(state, path);
  const { operator, key } = BINARY_LEVELS[level]!;

  const start = state.peek()?.from ?? state.lastEnd;
  const first = parseBinaryLevel(state, path, level + 1);
  if (!first) return undefined;

  const matches = (): boolean => {
    const token = state.peek();
    return !!token && token.kind === 'operator' && token.value === operator;
  };
  if (!matches()) return first;

  const operands: RuleNode[] = [first];
  repath(state, path, `${path}.${key}[0]`, start, state.lastEnd);

  while (matches()) {
    state.next();
    const operandPath = `${path}.${key}[${operands.length}]`;
    const operand = parseBinaryLevel(state, operandPath, level + 1);
    if (!operand) return undefined;
    operands.push(operand);
  }

  state.span(path, start, state.lastEnd);
  return { [key]: operands } as unknown as RuleNode;
}

/** expr := the full precedence chain, loosest level first. */
function parseExpression(state: ParserState, path: string): RuleNode | undefined {
  return parseBinaryLevel(state, path, 0);
}

/** Reads a parameter default literal: number, quoted string, or boolean. */
function parseDefault(state: ParserState): number | string | boolean | undefined {
  const token = state.peek();
  if (!token) { state.error('ExpectedDefault', 'expected a default value'); return undefined; }
  state.next();
  if (token.kind === 'number') return Number(token.value);
  if (token.kind === 'string') return literalValue(state, token, '"', 'UnterminatedString');
  if (token.value === 'true') return true;
  if (token.value === 'false') return false;
  state.error('ExpectedDefault', `\`${token.value}\` is not a valid default`, token);
  return undefined;
}

/** Consumes the leading run of `param` declarations, if any. */
function parseParameters(state: ParserState): RuleDocument['parameters'] {
  const parameters: NonNullable<RuleDocument['parameters']> = {};
  let found = false;

  while (state.peek()?.value === 'param') {
    state.next();
    const nameToken = state.peek();
    if (!nameToken || nameToken.kind !== 'spec') {
      state.error('ExpectedParameterName', 'expected a parameter name', nameToken);
      break;
    }
    state.next();

    if (state.peek()?.kind !== 'colon') {
      state.error('ExpectedParameterType', 'expected `:` and a type', state.peek());
      break;
    }
    state.next();

    // The lexer already classifies the four type words as `type` tokens.
    const typeToken = state.peek();
    if (!typeToken || typeToken.kind !== 'type') {
      state.error('ExpectedParameterType', 'expected integer, number, string or boolean', typeToken);
      break;
    }
    state.next();

    const declaration: ParameterDeclaration = {
      type: typeToken.value as ParameterDeclaration['type'],
    };
    if (state.peek()?.kind === 'equals') {
      state.next();
      const value = parseDefault(state);
      if (value !== undefined) declaration.default = value;
    }

    // `defineProperty`, not assignment: a parameter named `__proto__` would otherwise hit the
    // prototype setter, silently dropping the declaration and mutating the object.
    Object.defineProperty(parameters, nameToken.value, {
      value: declaration, enumerable: true, writable: true, configurable: true,
    });
    found = true;
  }

  return found ? parameters : undefined;
}

/**
 * Parses DSL text into a rule document, along with the source range of every node and
 * any errors found. Never throws; a fatal error leaves `document` undefined.
 */
export function parse(text: string): ParseResult {
  const state = new ParserState(text);
  const parameters = parseParameters(state);
  const rule = parseExpression(state, ROOT);

  if (!state.atEnd) {
    const token = state.peek()!;
    state.error('UnexpectedToken', `unexpected \`${token.value}\``, token);
  }

  // Outermost node first. Two spans sharing a `from` are always ancestor and descendant, and a
  // descendant's path strictly extends its ancestor's, so path length orders them correctly.
  const spans = [...state.spans].sort((a, b) => a.from - b.from || a.path.length - b.path.length);
  if (!rule || state.errors.length > 0) {
    return { errors: state.errors, spans };
  }
  const document: RuleDocument = parameters ? { parameters, rule } : { rule };
  return { document, errors: state.errors, spans };
}
