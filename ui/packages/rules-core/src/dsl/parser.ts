import type { ArgValue, Definition, ParameterDeclaration, RuleDocument, RuleNode } from '../document.js';
import type { Catalog, CatalogEntry, CatalogParameter } from '../contracts.js';
import { RESERVED_LOCAL_NAMES, isValidLocalName } from '../localNames.js';
import { definitionBodyPath } from '../paths.js';
import { tokenize } from './lexer.js';
import type { DslError, NodeSpan, ParseResult, Token, TokenKind } from './types.js';

const ROOT = '$.rule';

/** Options for {@link parse}. */
export interface ParseOptions {
  /**
   * The spec catalog, used only to resolve *positional* arguments to their declared names.
   * Absent, positional arguments are an error rather than a guess — so parsing stays a pure
   * function of the text for every document the printer can produce.
   */
  catalog?: Catalog;
  /**
   * Names to treat as declared locals in addition to whatever the text's own `let` preamble
   * declares. A full document's text always carries its own preamble, so this is only needed
   * when parsing a *fragment* — most notably `printInline`'s output, which renders a single node
   * with no preamble at all. Pass the owning document's `definitions` keys to make a printed
   * local reference (`{ local: 'a' }` → `'a'`) read back as itself rather than being demoted to a
   * `{ spec: 'a' }` reference.
   */
  locals?: ReadonlySet<string>;
}

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
  /**
   * Every declared local name: seeded from `ParseOptions.locals` (for a fragment parsed without
   * its owning preamble, e.g. `printInline`'s output), then added to by {@link collectLocalNames}
   * before parsing begins — so a bare word anywhere in the rule or a later `let` body can resolve
   * against a `let` declared after it, not just before.
   */
  readonly locals: Set<string>;
  /** Names already consumed by a `let` declaration during the actual parse — distinct from
   * {@link locals}, which is the forward-looking pre-scan and never shrinks or reports duplicates. */
  readonly declaredDefinitions = new Set<string>();
  index = 0;

  constructor(readonly text: string, readonly catalog?: Catalog, locals?: ReadonlySet<string>) {
    this.tokens = tokenize(text);
    this.locals = new Set(locals);
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

/**
 * The token kinds a word can lex as. The lexer classifies words context-free, so `all` is a
 * `quantifier` and `string` a `type` wherever they appear.
 */
const WORD_KINDS: ReadonlySet<TokenKind> = new Set<TokenKind>(['spec', 'keyword', 'type', 'quantifier']);

/**
 * Scans the whole token stream for every `let NAME = …` in the preamble, before any parsing
 * happens — so a `let` may be referenced by a `let` declared *after* it, and `parsePrimary` can
 * tell a bare word declared as a local apart from an ordinary spec reference regardless of which
 * one it reaches first. Tracks paren/brace depth so a group or quantifier body inside a `let`'s
 * expression is never mistaken for the following `param`/`let` statement.
 */
function collectLocalNames(state: ParserState): void {
  let i = 0;
  while (i < state.tokens.length) {
    const token = state.tokens[i]!;
    if (token.kind !== 'keyword' || (token.value !== 'param' && token.value !== 'let')) break;

    if (token.value === 'let') {
      const nameToken = state.tokens[i + 1];
      if (nameToken && WORD_KINDS.has(nameToken.kind)) state.locals.add(nameToken.value);
    }

    // Skip past this statement's body to the next one, tracking bracket depth so a nested
    // group or quantifier body isn't mistaken for the following statement.
    i++;
    let depth = 0;
    while (i < state.tokens.length) {
      const inner = state.tokens[i]!;
      if (inner.kind === 'paren' || inner.kind === 'brace') {
        depth += inner.value === '(' || inner.value === '{' ? 1 : -1;
      }
      if (depth <= 0 && inner.kind === 'keyword' && (inner.value === 'param' || inner.value === 'let')) {
        break;
      }
      i++;
    }
  }
}

/**
 * Consumes an identifier in a position where the grammar admits nothing but an identifier, so any
 * word is simply a name. Used for argument names, `param` declaration names, and collection paths.
 *
 * Deliberately NOT used for a spec reference at expression position: a bare `all` there could open
 * `all in orders { … }`, so the lexer's classification is load-bearing and must be respected.
 */
function parseIdentifier(state: ParserState, code: string, message: string): Token | undefined {
  const token = state.peek();
  if (!token || !WORD_KINDS.has(token.kind)) {
    state.error(code, message, token);
    return undefined;
  }
  state.next();
  return token;
}

/** The catalog's entry for `spec`, absent when there is no catalog or it does not name `spec`. */
function catalogEntry(state: ParserState, spec: string): CatalogEntry | undefined {
  return state.catalog?.specs.find((entry) => entry.name === spec);
}

/**
 * The declared parameters of `spec`, or `null`/`undefined` when the catalog cannot say — either
 * because there is no catalog, the spec is unknown to it, or its entry declares no parameters
 * (the server sends an explicit `null` for a plain spec's `parameters`, rather than omitting the
 * property, so this must not be narrowed to `undefined` alone).
 */
function declaredParameters(
  state: ParserState, spec: string,
): readonly CatalogParameter[] | null | undefined {
  return catalogEntry(state, spec)?.parameters;
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

  const pathToken = parseIdentifier(
    state, 'ExpectedCollection', 'expected a collection path after `in`',
  );
  if (!pathToken) return undefined;

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

/**
 * Whether `token` opens a literal an argument value could be — the same set `parseArgValue`
 * accepts. A bare identifier (e.g. `n`) is never one of these, even though it lexes with a
 * `spec`-like kind, so it is read as an attempted argument *name* rather than a positional value.
 */
function looksLikeArgValue(token: Token | undefined): boolean {
  if (!token) return false;
  if (token.kind === 'number' || token.kind === 'string') return true;
  return token.value === 'true' || token.value === 'false' || token.value === 'null';
}

/**
 * The name `spec` declares for its parameter at `index`, which a positional argument there takes.
 * Reports why the catalog cannot supply one — no catalog, a spec it does not name, or more
 * arguments than parameters — and returns undefined.
 */
function positionalName(
  state: ParserState, spec: string, index: number, token: Token | undefined,
): string | undefined {
  if (state.catalog == null) {
    state.error('ExpectedArgName', 'expected an argument name', token);
    return undefined;
  }
  const declared = declaredParameters(state, spec);
  if (!declared) {
    state.error('UnknownParameterisedSpec',
      `\`${spec}\` declares no parameters to supply positionally`, token);
    return undefined;
  }
  if (index >= declared.length) {
    state.error('TooManyArguments', `\`${spec}\` declares ${declared.length} parameter(s)`, token);
    return undefined;
  }
  return declared[index]!.name;
}

/**
 * args := '(' arg (',' arg)* ')' where arg := literal | NAME '=' literal — absent when no `(`
 * follows. Positional args resolve to the declared name of `spec`'s catalog entry at that
 * position, so what lands in the document is always named. Positional args must precede named
 * ones, mirroring C#.
 */
function parseArgs(state: ParserState, spec: string): Record<string, ArgValue> | undefined {
  if (state.peek()?.value !== '(') return undefined;
  const open = state.next()!;
  const args: Record<string, ArgValue> = {};
  let positional = 0;
  let sawNamed = false;

  for (;;) {
    // Anything that opens a value literal is positional; everything else (including a bare
    // identifier with no following `=`) is an attempted name, so `s(n 1)` still reports the
    // missing `=` rather than being mistaken for a positional argument. A one-token lookahead
    // for `=` keeps `true`/`false`/`null` usable as argument NAMES (`gate(true = 1)`) even
    // though they lex the same as their literal-value forms. A quoted name (`s("x" = 1)`) takes
    // this same named branch — the lookahead sees its `=` too — but is still rejected, because
    // `parseIdentifier` below only accepts `WORD_KINDS`, which excludes `'string'`.
    const argToken = state.peek();
    let name: string | undefined;

    if (looksLikeArgValue(argToken) && state.peek(1)?.kind !== 'equals') {
      if (sawNamed) {
        state.error('PositionalAfterNamed',
          'a positional argument cannot follow a named one', argToken);
        return undefined;
      }
      name = positionalName(state, spec, positional, argToken);
      if (name === undefined) return undefined;
      positional++;
    } else {
      const nameToken = parseIdentifier(state, 'ExpectedArgName', 'expected an argument name');
      if (!nameToken) return undefined;
      if (state.peek()?.kind !== 'equals') {
        state.error('ExpectedArgValue', 'expected `=` and an argument value', state.peek());
        return undefined;
      }
      state.next();
      sawNamed = true;
      name = nameToken.value;
    }

    const value = parseArgValue(state);
    if (value === undefined) return undefined;

    if (Object.prototype.hasOwnProperty.call(args, name)) {
      state.error('DuplicateArg', `duplicate argument \`${name}\``, argToken);
      return undefined;
    }
    // `defineProperty`, not assignment: an argument named `__proto__` would otherwise hit the
    // prototype setter, silently dropping the value and mutating the object.
    Object.defineProperty(args, name, {
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

  if (token.kind === 'spec' && state.locals.has(token.value)) {
    state.next();
    const openArgs = state.peek();
    if (openArgs?.value === '(') {
      state.error('UnexpectedArguments', `\`${token.value}\` is a local reference and takes no arguments`, openArgs);
      state.next();
      return undefined;
    }
    return { local: token.value };
  }

  if (token.kind === 'spec') {
    state.next();
    // A spec the catalog names but gives no parameters takes no argument list at all, so say so
    // outright rather than leaving `parseArgs` to complain about the first argument. `parameters`
    // is an explicit `null` for a plain spec, so this must not test `undefined` alone — and an
    // empty declaration list (`[]`) is the same "no parameters" case, since `!= null` alone would
    // let it slip through to `parseArgs` and only be rejected later by the server.
    const entry = catalogEntry(state, token.value);
    const openArgs = state.peek();
    const declaresNoParameters = entry !== undefined
      && (entry.parameters == null || entry.parameters.length === 0);
    if (openArgs?.value === '(' && declaresNoParameters) {
      state.error('UnexpectedArguments', `\`${token.value}\` takes no arguments`, openArgs);
      state.next();
      return undefined;
    }
    const args = parseArgs(state, token.value);
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

/**
 * Consumes one `param NAME: TYPE ('=' DEFAULT)?` declaration — one iteration of the preamble loop
 * in {@link parsePreamble}, which owns repeating this and accumulating the results. Returns
 * `undefined` on error, having already reported it.
 */
function parseParameters(
  state: ParserState,
): { name: string; declaration: ParameterDeclaration } | undefined {
  state.next(); // 'param'
  const nameToken = parseIdentifier(state, 'ExpectedParameterName', 'expected a parameter name');
  if (!nameToken) return undefined;

  if (state.peek()?.kind !== 'colon') {
    state.error('ExpectedParameterType', 'expected `:` and a type', state.peek());
    return undefined;
  }
  state.next();

  // The lexer already classifies the four type words as `type` tokens.
  const typeToken = state.peek();
  if (!typeToken || typeToken.kind !== 'type') {
    state.error('ExpectedParameterType', 'expected integer, number, string or boolean', typeToken);
    return undefined;
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

  return { name: nameToken.value, declaration };
}

/**
 * Consumes one `let NAME '=' expr` declaration — one iteration of the preamble loop in
 * {@link parsePreamble}. The body is parsed with `parseExpression`, so it may itself reference any
 * declared local, including one declared later — {@link collectLocalNames} has already populated
 * `state.locals` from the whole token stream before parsing starts. Returns `undefined` on error,
 * having already reported it.
 */
function parseLet(state: ParserState): { name: string; definition: Definition } | undefined {
  state.next(); // 'let'
  const nameToken = parseIdentifier(state, 'ExpectedLocalName', 'expected a name after `let`');
  if (!nameToken) return undefined;

  if (nameToken.value.includes('.')) {
    state.error('DottedLocalName', 'a local name cannot contain a dot', nameToken);
    return undefined;
  }
  // Checked ahead of the generic InvalidLocalName case so a reserved word gets a message that
  // names the actual problem: `all`/`param`/`integer` etc. are pattern-valid but can never be
  // read back by parsePrimary's `spec`-kind local branch, since a reserved word never lexes
  // as `spec`.
  if (RESERVED_LOCAL_NAMES.has(nameToken.value)) {
    state.error(
      'ReservedLocalName',
      `\`${nameToken.value}\` is a reserved word and cannot name a local`,
      nameToken,
    );
    return undefined;
  }
  if (!isValidLocalName(nameToken.value)) {
    state.error('InvalidLocalName', `\`${nameToken.value}\` is not a valid local name`, nameToken);
    return undefined;
  }
  if (state.declaredDefinitions.has(nameToken.value)) {
    state.error('DuplicateLocal', `\`${nameToken.value}\` is already declared`, nameToken);
    return undefined;
  }

  if (state.peek()?.kind !== 'equals') {
    state.error('ExpectedEquals', 'expected `=` after the local name', state.peek());
    return undefined;
  }
  state.next();

  const rule = parseExpression(state, definitionBodyPath(nameToken.value));
  if (!rule) return undefined;

  state.declaredDefinitions.add(nameToken.value);
  return { name: nameToken.value, definition: { rule } };
}

/**
 * Consumes the leading run of `param` and `let` declarations, if any, one at a time via
 * {@link parseParameters} and {@link parseLet}. A `param` may not follow a `let` — once a `let`
 * has been seen, a subsequent `param` is left for `parseExpression` to reject as an unexpected
 * token, rather than silently re-opening the parameter block.
 */
function parsePreamble(
  state: ParserState,
): { parameters: RuleDocument['parameters']; definitions: Record<string, Definition> | undefined } {
  const parameters: NonNullable<RuleDocument['parameters']> = {};
  const definitions: Record<string, Definition> = {};
  let sawParameter = false;
  let sawDefinition = false;

  for (;;) {
    const token = state.peek();
    if (!token) break;

    if (token.value === 'param' && !sawDefinition) {
      const result = parseParameters(state);
      if (!result) break;
      // `defineProperty`, not assignment: a parameter named `__proto__` would otherwise hit the
      // prototype setter, silently dropping the declaration and mutating the object.
      Object.defineProperty(parameters, result.name, {
        value: result.declaration, enumerable: true, writable: true, configurable: true,
      });
      sawParameter = true;
    } else if (token.value === 'let') {
      const result = parseLet(state);
      if (!result) break;
      // `defineProperty`, not assignment: the same prototype-pollution hazard as `param` above.
      Object.defineProperty(definitions, result.name, {
        value: result.definition, enumerable: true, writable: true, configurable: true,
      });
      sawDefinition = true;
    } else {
      break;
    }
  }

  return {
    parameters: sawParameter ? parameters : undefined,
    definitions: sawDefinition ? definitions : undefined,
  };
}

/**
 * Parses DSL text into a rule document, along with the source range of every node and
 * any errors found. Never throws; a fatal error leaves `document` undefined.
 */
export function parse(text: string, options?: ParseOptions): ParseResult {
  const state = new ParserState(text, options?.catalog, options?.locals);
  collectLocalNames(state);
  const { parameters, definitions } = parsePreamble(state);
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
  const document: RuleDocument = {
    ...(parameters ? { parameters } : {}),
    ...(definitions ? { definitions } : {}),
    rule,
  };
  return { document, errors: state.errors, spans };
}
