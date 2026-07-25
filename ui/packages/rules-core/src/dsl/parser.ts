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

/** expr := the full precedence chain. */
function parseExpression(state: ParserState, path: string): RuleNode | undefined {
  return parseUnary(state, path);
}

/**
 * Parses DSL text into a rule document, along with the source range of every node and
 * any errors found. Never throws; a fatal error leaves `document` undefined.
 */
export function parse(text: string): ParseResult {
  const state = new ParserState(text);
  const rule = parseExpression(state, ROOT);

  if (!state.atEnd) {
    const token = state.peek()!;
    state.error('UnexpectedToken', `unexpected \`${token.value}\``, token);
  }

  const spans = [...state.spans].sort((a, b) => a.from - b.from || a.path.length - b.path.length);
  if (!rule || state.errors.length > 0) {
    return { errors: state.errors, spans };
  }
  const document: RuleDocument = { rule };
  return { document, errors: state.errors, spans };
}
