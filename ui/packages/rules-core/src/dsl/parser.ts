import type { RuleDocument, RuleNode } from '../document.js';
import { tokenize } from './lexer.js';
import type { DslError, NodeSpan, ParseResult, Token } from './types.js';

const ROOT = '$.rule';

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
  return nameToken.value.slice(1, nameToken.value.endsWith('"') ? -1 : undefined);
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
  if (value?.kind === 'number') { count = Number(value.value); state.next(); }
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

/** primary := SPEC | `expr` | '(' expr ')' | quantifier */
function parsePrimary(state: ParserState, path: string): RuleNode | undefined {
  const token = state.peek();
  if (!token) {
    state.error('UnexpectedEnd', 'expected an expression');
    return undefined;
  }

  if (token.kind === 'spec') {
    state.next();
    return { spec: token.value };
  }

  if (token.kind === 'expression') {
    state.next();
    const raw = token.value;
    const inner = raw.slice(1, raw.endsWith('`') && raw.length > 1 ? -1 : undefined);
    return { expression: inner };
  }

  if (token.kind === 'paren' && token.value === '(') {
    state.next();
    const inner = parseExpression(state, path);
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

const PARAM_TYPES = new Set(['integer', 'number', 'string', 'boolean']);

/** Reads a parameter default literal: number, quoted string, or boolean. */
function parseDefault(state: ParserState): number | string | boolean | undefined {
  const token = state.peek();
  if (!token) { state.error('ExpectedDefault', 'expected a default value'); return undefined; }
  state.next();
  if (token.kind === 'number') return Number(token.value);
  if (token.kind === 'string') return token.value.slice(1, token.value.endsWith('"') ? -1 : undefined);
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
      return found ? parameters : undefined;
    }
    state.next();

    if (state.peek()?.kind !== 'colon') {
      state.error('ExpectedParameterType', 'expected `:` and a type', state.peek());
      return found ? parameters : undefined;
    }
    state.next();

    const typeToken = state.peek();
    if (!typeToken || !PARAM_TYPES.has(typeToken.value)) {
      state.error('ExpectedParameterType', 'expected integer, number, string or boolean', typeToken);
      return found ? parameters : undefined;
    }
    state.next();

    const declaration: { type: 'integer' | 'number' | 'string' | 'boolean'; default?: number | string | boolean } = {
      type: typeToken.value as 'integer' | 'number' | 'string' | 'boolean',
    };
    if (state.peek()?.kind === 'equals') {
      state.next();
      const value = parseDefault(state);
      if (value !== undefined) declaration.default = value;
    }

    parameters[nameToken.value] = declaration;
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

  const spans = [...state.spans].sort((a, b) => a.from - b.from || a.path.length - b.path.length);
  if (!rule || state.errors.length > 0) {
    return { errors: state.errors, spans };
  }
  const document: RuleDocument = parameters ? { parameters, rule } : { rule };
  return { document, errors: state.errors, spans };
}
