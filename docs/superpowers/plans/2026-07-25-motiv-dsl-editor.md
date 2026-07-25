# Motiv DSL Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a textual `.motiv` DSL editor to the demo app — a real two-way parser/printer over the whole rule grammar, presented in a CodeMirror 6 editor with highlighting, autocomplete, lint, and a payload popover, toggled against the existing Builder.

**Architecture:** Three layers. (1) A pure, dependency-free language layer in `@motiv/rules-core` (`lexer` → `parser` → `printer` → `decorations`) that is exhaustively unit-tested in isolation. (2) A CodeMirror 6 integration in the demo app (`motivLanguage`, `completion`, `lint`, `hover`, `theme`). (3) React components + a `useDslSync` hook that makes the text the source of truth: debounced parse commits a `RuleDocument` into the shared `RuleEditorStore`, with a conflict banner when the Builder changes the tree under a dirty buffer.

**Tech Stack:** TypeScript (ESM, `.js` import specifiers), React 18, CodeMirror 6, Vitest, Testing Library, Playwright, pnpm workspaces.

**Spec:** `docs/superpowers/specs/2026-07-25-motiv-dsl-editor-design.md`

---

## Conventions (read before starting)

These are established patterns in this repo. Follow them in every task.

- **ESM import specifiers include `.js`** even for TypeScript sources: `import { parse } from '../src/dsl/parser.js'`. This is required by the `tsconfig.base.json` module resolution. Getting this wrong is the single most common build break here.
- **Tests live in a sibling `test/` directory**, not next to sources. `packages/rules-core/test/*.test.ts`, `apps/demo/test/**/*.test.tsx`.
- **`rules-core` has zero runtime dependencies.** Never add one. CodeMirror belongs to the demo app only.
- **Doc comments** use `/** … */` on exported symbols, in the terse declarative style already used across `rules-core` (see `src/document.ts`).
- **Commit after every green test.** Small commits, imperative subject lines (`feat:`, `test:`, `refactor:`).

### Commands

| What | Command |
| --- | --- |
| Core unit tests | `pnpm -C ui/packages/rules-core test` |
| A single core test file | `pnpm -C ui/packages/rules-core exec vitest run test/dsl-parser.test.ts` |
| Demo component tests | `pnpm -C ui/apps/demo test` |
| A single demo test file | `pnpm -C ui/apps/demo exec vitest run test/dsl/DslEditor.test.tsx` |
| Typecheck a package | `pnpm -C ui/packages/rules-core typecheck` |
| Demo e2e | `pnpm -C ui/apps/demo e2e` |
| Everything (final gate) | see Task 21 |

---

## File Structure

**Create — pure language layer (`ui/packages/rules-core/`):**

| File | Responsibility |
| --- | --- |
| `src/dsl/types.ts` | Shared DSL types: `Token`, `TokenKind`, `DslError`, `NodeSpan`, `ParseResult`. No logic. |
| `src/dsl/lexer.ts` | `tokenize(text): Token[]` — text → tokens with source offsets. |
| `src/dsl/parser.ts` | `parse(text): ParseResult` — tokens → `RuleDocument` + `spans` + `errors`. |
| `src/dsl/printer.ts` | `print(document): string` — canonical reprint (the Format button). |
| `src/dsl/decorations.ts` | `mergeDecorations(parsed, prior)` — re-attach `whenTrue`/`whenFalse` by path. |
| `src/dsl/index.ts` | Barrel re-exporting the four modules. |

**Create — tests (`ui/packages/rules-core/test/`):** `dsl-lexer.test.ts`, `dsl-parser.test.ts`, `dsl-parser-errors.test.ts`, `dsl-printer.test.ts`, `dsl-roundtrip.test.ts`, `dsl-decorations.test.ts`.

**Create — editor integration (`ui/apps/demo/src/dsl/`):**

| File | Responsibility |
| --- | --- |
| `motivLanguage.ts` | CodeMirror `StreamLanguage` + `HighlightStyle` for Motiv tokens. |
| `completion.ts` | Catalog-driven autocomplete source. |
| `lint.ts` | Parser errors + backend `RuleError`s → CodeMirror diagnostics. |
| `hover.ts` | Hover tooltip showing kind · code · message · path. |
| `theme.ts` | `EditorView.theme` bound to the demo's CSS custom properties. |
| `useDslSync.ts` | Text ↔ store bridge; owns sync/conflict state machine. |
| `DslEditor.tsx` | Assembles editor, returns-strip, sync pill, banner, popover. |
| `PayloadPopover.tsx` | Per-spec name + whenTrue/whenFalse editor. |

**Create — demo tests (`ui/apps/demo/test/dsl/`):** `useDslSync.test.tsx`, `DslEditor.test.tsx`, `PayloadPopover.test.tsx`, `completion.test.ts`.

**Modify:**

| File | Change |
| --- | --- |
| `ui/packages/rules-core/src/index.ts` | Add `export * from './dsl/index.js';` |
| `ui/apps/demo/package.json` | Add CodeMirror dependencies. |
| `ui/apps/demo/src/panes/BuilderPane.tsx` | Extract body so the pane can host a Builder⇄DSL toggle. |
| `ui/apps/demo/src/App.tsx` | Render the new `EditorPane` in place of `BuilderPane`. |
| `ui/apps/demo/src/styles/app.css` | Styles for toggle, pill, banner, popover, editor. |
| `ui/apps/demo/e2e/` | Add `dsl.spec.ts`. |

---

## Task 1: DSL types

**Files:**
- Create: `ui/packages/rules-core/src/dsl/types.ts`

- [ ] **Step 1: Write the types module**

There is no test for this task — it is types only, exercised by every later task. Create `ui/packages/rules-core/src/dsl/types.ts`:

```typescript
/** The lexical class of a DSL token. */
export type TokenKind =
  | 'spec'        // is-active
  | 'ident'       // bare identifier (param names, collection paths)
  | 'keyword'     // param, in, as
  | 'type'        // integer, number, string, boolean
  | 'quantifier'  // all, any, exactly, atLeast, atMost
  | 'operator'    // && || & | ^ !
  | 'paren'       // ( )
  | 'brace'       // { }
  | 'colon'       // :
  | 'equals'      // =
  | 'string'      // "quota"
  | 'expression'  // `n > 0`
  | 'number'      // 3
  | 'paramRef'    // @minOrders
  | 'error';      // an unrecognised character

/** One lexed token with its half-open source range `[from, to)`. */
export interface Token {
  kind: TokenKind;
  /** Source text of the token, verbatim. */
  value: string;
  from: number;
  to: number;
}

/** A DSL-level error with a source range, mirroring the shape CodeMirror's linter wants. */
export interface DslError {
  from: number;
  to: number;
  /** Stable machine-readable code, e.g. `UnexpectedToken`. */
  code: string;
  message: string;
}

/** Maps a backend node path (e.g. `$.rule.andAlso[0]`) to the text range that produced it. */
export interface NodeSpan {
  path: string;
  from: number;
  to: number;
}

/** The outcome of parsing DSL text. */
export interface ParseResult {
  /** The parsed document; absent when a fatal syntax error prevented a full parse. */
  document?: RuleDocument;
  errors: DslError[];
  spans: NodeSpan[];
}
```

Add the import at the top of the file (the `ParseResult` above references `RuleDocument`):

```typescript
import type { RuleDocument } from '../document.js';
```

- [ ] **Step 2: Typecheck**

Run: `pnpm -C ui/packages/rules-core typecheck`
Expected: PASS (no output, exit 0).

- [ ] **Step 3: Commit**

```bash
git add ui/packages/rules-core/src/dsl/types.ts
git commit -m "feat(dsl): add DSL token and parse-result types"
```

---

## Task 2: Lexer

**Files:**
- Create: `ui/packages/rules-core/src/dsl/lexer.ts`
- Test: `ui/packages/rules-core/test/dsl-lexer.test.ts`

The lexer turns text into tokens with exact offsets. Offsets matter more than anything else here — every squiggle, tooltip, and popover anchor is derived from them.

Classification rule for a bare word: `param`/`in`/`as` → `keyword`; `integer`/`number`/`string`/`boolean` → `type`; `all`/`any`/`exactly`/`atLeast`/`atMost` → `quantifier`; anything else → `spec`. (The parser decides from context whether a `spec` token is really a collection path or a parameter name — the lexer does not need to know.)

- [ ] **Step 1: Write the failing test**

Create `ui/packages/rules-core/test/dsl-lexer.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { tokenize } from '../src/dsl/lexer.js';

describe('tokenize', () => {
  it('lexes a spec name as a single token with exact offsets', () => {
    expect(tokenize('is-active')).toEqual([
      { kind: 'spec', value: 'is-active', from: 0, to: 9 },
    ]);
  });

  it('skips whitespace but keeps offsets absolute', () => {
    expect(tokenize('  is-active')).toEqual([
      { kind: 'spec', value: 'is-active', from: 2, to: 11 },
    ]);
  });

  it('lexes two-character operators before one-character ones', () => {
    expect(tokenize('a && b || c').map((t) => [t.kind, t.value])).toEqual([
      ['spec', 'a'], ['operator', '&&'], ['spec', 'b'], ['operator', '||'], ['spec', 'c'],
    ]);
  });

  it('lexes single-character logical operators', () => {
    expect(tokenize('a & b | c ^ !d').map((t) => t.value)).toEqual([
      'a', '&', 'b', '|', 'c', '^', '!', 'd',
    ]);
  });

  it('classifies keywords, types and quantifiers', () => {
    expect(tokenize('param x: integer atLeast in as').map((t) => t.kind)).toEqual([
      'keyword', 'spec', 'colon', 'type', 'quantifier', 'keyword', 'keyword',
    ]);
  });

  it('lexes a quoted string including its quotes', () => {
    expect(tokenize('as "quota"')[1]).toEqual({
      kind: 'string', value: '"quota"', from: 3, to: 10,
    });
  });

  it('lexes a backtick expression', () => {
    expect(tokenize('`n > 0`')).toEqual([
      { kind: 'expression', value: '`n > 0`', from: 0, to: 7 },
    ]);
  });

  it('lexes a param reference and a number', () => {
    expect(tokenize('@minOrders 3').map((t) => [t.kind, t.value])).toEqual([
      ['paramRef', '@minOrders'], ['number', '3'],
    ]);
  });

  it('lexes braces, parens, colon and equals', () => {
    expect(tokenize('(){}:=').map((t) => t.kind)).toEqual([
      'paren', 'paren', 'brace', 'brace', 'colon', 'equals',
    ]);
  });

  it('emits an error token for an unrecognised character', () => {
    expect(tokenize('#')).toEqual([{ kind: 'error', value: '#', from: 0, to: 1 }]);
  });

  it('lexes an unterminated string up to end of input', () => {
    expect(tokenize('"oops')).toEqual([
      { kind: 'string', value: '"oops', from: 0, to: 5 },
    ]);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-lexer.test.ts`
Expected: FAIL — cannot resolve `../src/dsl/lexer.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/packages/rules-core/src/dsl/lexer.ts`:

```typescript
import type { Token, TokenKind } from './types.js';

const KEYWORDS = new Set(['param', 'in', 'as']);
const TYPES = new Set(['integer', 'number', 'string', 'boolean']);
const QUANTIFIERS = new Set(['all', 'any', 'exactly', 'atLeast', 'atMost']);

/** Words are spec-shaped: a letter followed by letters, digits, hyphens or underscores. */
const WORD_START = /[A-Za-z_]/;
const WORD_REST = /[A-Za-z0-9_-]/;

function wordKind(word: string): TokenKind {
  if (KEYWORDS.has(word)) return 'keyword';
  if (TYPES.has(word)) return 'type';
  if (QUANTIFIERS.has(word)) return 'quantifier';
  return 'spec';
}

/** Reads a delimited run starting at `from`, returning the index just past the closing delimiter. */
function readDelimited(text: string, from: number, delimiter: string): number {
  let i = from + 1;
  while (i < text.length && text[i] !== delimiter) i++;
  return i < text.length ? i + 1 : text.length;
}

/**
 * Lexes DSL text into tokens carrying absolute source offsets. Never throws: an
 * unrecognised character becomes an `error` token so the parser can report it in place.
 */
export function tokenize(text: string): Token[] {
  const tokens: Token[] = [];
  let i = 0;
  const push = (kind: TokenKind, from: number, to: number): void => {
    tokens.push({ kind, value: text.slice(from, to), from, to });
  };

  while (i < text.length) {
    const char = text[i]!;

    if (/\s/.test(char)) { i++; continue; }

    if (char === '&' && text[i + 1] === '&') { push('operator', i, i + 2); i += 2; continue; }
    if (char === '|' && text[i + 1] === '|') { push('operator', i, i + 2); i += 2; continue; }
    if ('&|^!'.includes(char)) { push('operator', i, i + 1); i++; continue; }

    if (char === '(' || char === ')') { push('paren', i, i + 1); i++; continue; }
    if (char === '{' || char === '}') { push('brace', i, i + 1); i++; continue; }
    if (char === ':') { push('colon', i, i + 1); i++; continue; }
    if (char === '=') { push('equals', i, i + 1); i++; continue; }

    if (char === '"') { const end = readDelimited(text, i, '"'); push('string', i, end); i = end; continue; }
    if (char === '`') { const end = readDelimited(text, i, '`'); push('expression', i, end); i = end; continue; }

    if (char === '@') {
      let j = i + 1;
      while (j < text.length && WORD_REST.test(text[j]!)) j++;
      push('paramRef', i, j); i = j; continue;
    }

    if (/[0-9]/.test(char)) {
      let j = i;
      while (j < text.length && /[0-9]/.test(text[j]!)) j++;
      push('number', i, j); i = j; continue;
    }

    if (WORD_START.test(char)) {
      let j = i;
      while (j < text.length && WORD_REST.test(text[j]!)) j++;
      push(wordKind(text.slice(i, j)), i, j); i = j; continue;
    }

    push('error', i, i + 1); i++;
  }

  return tokens;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-lexer.test.ts`
Expected: PASS — 11 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/lexer.ts ui/packages/rules-core/test/dsl-lexer.test.ts
git commit -m "feat(dsl): add DSL lexer with source offsets"
```

---

## Task 3: Parser — leaves, `not`, and grouping

The parser is built over three tasks: leaves/`not`/grouping (this task), binary operators with precedence (Task 4), then parameters and quantifiers (Task 5). Each task adds tests and grows the same file.

**Files:**
- Create: `ui/packages/rules-core/src/dsl/parser.ts`
- Test: `ui/packages/rules-core/test/dsl-parser.test.ts`

Two things every parse must produce alongside the document:
- **`spans`** — one entry per node, keyed by the backend path (`$.rule`, `$.rule.andAlso[0]`, `$.rule.asAtLeastNSatisfied`). Built by threading the path down through the recursive descent.
- **`errors`** — never thrown; collected. When a fatal error prevents a document, `document` is left `undefined`.

- [ ] **Step 1: Write the failing test**

Create `ui/packages/rules-core/test/dsl-parser.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';

describe('parse — leaves and grouping', () => {
  it('parses a bare spec into a spec node at the root path', () => {
    const result = parse('is-active');
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual({ rule: { spec: 'is-active' } });
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

  it('attaches a name from a trailing as-clause', () => {
    expect(parse('is-active as "activity"').document).toEqual({
      rule: { spec: 'is-active', name: 'activity' },
    });
  });

  it('binds as to the group when applied to a parenthesised expression', () => {
    expect(parse('(is-active) as "activity"').document).toEqual({
      rule: { spec: 'is-active', name: 'activity' },
    });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-parser.test.ts`
Expected: FAIL — cannot resolve `../src/dsl/parser.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/packages/rules-core/src/dsl/parser.ts`. This file grows in Tasks 4 and 5; write it now with the binary/quantifier hooks stubbed so the structure is final.

```typescript
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

/** expr := the full precedence chain. Grown in Task 4. */
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
```

Note on the `not` span ordering: the test expects `$.rule` before `$.rule.not`, which the sort achieves because both start at different offsets (`0` vs `1`). For the parenthesised case the child inherits the same path, so no duplicate span is emitted.

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-parser.test.ts`
Expected: PASS — 9 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/parser.ts ui/packages/rules-core/test/dsl-parser.test.ts
git commit -m "feat(dsl): parse spec, expression, not and grouped nodes"
```

---

## Task 4: Parser — binary operators and precedence

Precedence, loosest to tightest: `||` → `&&` → `|` → `^` → `&`. Consecutive uses of the *same* operator flatten into one n-ary node (the schema requires `minItems: 2`, and `a && b && c` must be one `andAlso` of three, not nested pairs).

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/parser.ts`
- Modify: `ui/packages/rules-core/test/dsl-parser.test.ts`

- [ ] **Step 1: Write the failing test**

Append to `ui/packages/rules-core/test/dsl-parser.test.ts`:

```typescript
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

  it('names a compound node when the group carries the as-clause', () => {
    expect(parse('(a && b) as "pair"').document).toEqual({
      rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' },
    });
  });

  it('paths operands by operator and index', () => {
    const spans = parse('a && b').spans;
    expect(spans.map((s) => s.path)).toEqual([
      '$.rule', '$.rule.andAlso[0]', '$.rule.andAlso[1]',
    ]);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-parser.test.ts`
Expected: FAIL — `a & b` yields `{ spec: 'a' }` and an `UnexpectedToken` error, because `parseExpression` only delegates to `parseUnary`.

- [ ] **Step 3: Write the implementation**

In `ui/packages/rules-core/src/dsl/parser.ts`, add the precedence table above `parseExpression`:

```typescript
/** Binary levels, loosest first. Each entry maps its DSL operator to the node key it builds. */
const BINARY_LEVELS = [
  { operator: '||', key: 'orElse' },
  { operator: '&&', key: 'andAlso' },
  { operator: '|', key: 'or' },
  { operator: '^', key: 'xor' },
  { operator: '&', key: 'and' },
] as const;

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

  // A run exists, so operands are re-parsed under indexed paths. The first operand's
  // spans were recorded under `path`; re-pathing them keeps spans consistent.
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
```

Add the `repath` helper just above it — when a run turns out to exist, the first operand's spans were recorded under the parent path and must be moved under `[0]`:

```typescript
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
```

`NodeSpan.path` must be mutable for `repath`. It already is (`interface NodeSpan { path: string; … }`).

Replace the placeholder `parseExpression` body:

```typescript
/** expr := the full precedence chain, loosest level first. */
function parseExpression(state: ParserState, path: string): RuleNode | undefined {
  return parseBinaryLevel(state, path, 0);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-parser.test.ts`
Expected: PASS — 18 tests (9 from Task 3, 9 new).

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/parser.ts ui/packages/rules-core/test/dsl-parser.test.ts
git commit -m "feat(dsl): parse binary operators with C# precedence and n-ary flattening"
```

---

## Task 5: Parser — quantifiers and parameters

Quantifiers map to the five higher-order nodes; `param` declarations populate `document.parameters`.

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/parser.ts`
- Modify: `ui/packages/rules-core/test/dsl-parser.test.ts`

- [ ] **Step 1: Write the failing test**

Append to `ui/packages/rules-core/test/dsl-parser.test.ts`:

```typescript
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

  it('binds a trailing as-clause to the quantifier node', () => {
    expect(parse('atLeast(2) in orders { is-positive } as "quota"').document).toEqual({
      rule: {
        asAtLeastNSatisfied: { spec: 'is-positive' }, n: 2, path: 'orders', name: 'quota',
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-parser.test.ts`
Expected: FAIL — quantifier and `param` inputs produce `UnexpectedToken` errors.

- [ ] **Step 3: Write the implementation**

In `ui/packages/rules-core/src/dsl/parser.ts`, add the quantifier map near `BINARY_LEVELS`:

```typescript
/** DSL quantifier keyword → higher-order node key. Counted forms take an `(n)` argument. */
const QUANTIFIER_KEYS = {
  all: { key: 'asAllSatisfied', counted: false },
  any: { key: 'asAnySatisfied', counted: false },
  exactly: { key: 'asNSatisfied', counted: true },
  atLeast: { key: 'asAtLeastNSatisfied', counted: true },
  atMost: { key: 'asAtMostNSatisfied', counted: true },
} as const;

type QuantifierWord = keyof typeof QUANTIFIER_KEYS;
```

Add the quantifier parser above `parsePrimary`:

```typescript
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
```

Wire it into `parsePrimary` — insert this branch immediately before the final `state.error('UnexpectedToken', …)`:

```typescript
  if (token.kind === 'quantifier') {
    return parseQuantifier(state, path, token.value as QuantifierWord);
  }
```

Add the parameter-block parser above `parse`:

```typescript
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
```

The lexer classifies `true`/`false` as `spec` tokens, which `parseDefault` handles by value — no lexer change needed.

Finally, update `parse` to read parameters first and include them on the document:

```typescript
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-parser.test.ts`
Expected: PASS — 29 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/parser.ts ui/packages/rules-core/test/dsl-parser.test.ts
git commit -m "feat(dsl): parse quantifiers and parameter declarations"
```

---

## Task 6: Parser error reporting

Errors carry exact ranges so CodeMirror can squiggle the offending text. This task pins that contract down.

**Files:**
- Test: `ui/packages/rules-core/test/dsl-parser-errors.test.ts`
- Modify (if needed): `ui/packages/rules-core/src/dsl/parser.ts`

- [ ] **Step 1: Write the failing test**

Create `ui/packages/rules-core/test/dsl-parser-errors.test.ts`:

```typescript
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-parser-errors.test.ts`
Expected: Most pass from Task 3–5 work; any that fail point at a missing error path.

- [ ] **Step 3: Fix any gaps**

Only change `parser.ts` if a test fails. The likely gap is the dangling-operator case: `parseBinaryLevel` calls `parseBinaryLevel(state, operandPath, level + 1)` which bottoms out in `parsePrimary` with no token, producing `UnexpectedEnd` — that is the expected code, so it should already pass. If a test fails for a different reason, adjust the corresponding `state.error(...)` call to emit the code the test names; do not change the test.

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-parser-errors.test.ts`
Expected: PASS — 12 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/test/dsl-parser-errors.test.ts ui/packages/rules-core/src/dsl/parser.ts
git commit -m "test(dsl): pin parser error codes and ranges"
```

---

## Task 7: Printer

`print` produces the canonical form — what the **Format** button emits. The target is the prototype's reference text.

**Files:**
- Create: `ui/packages/rules-core/src/dsl/printer.ts`
- Test: `ui/packages/rules-core/test/dsl-printer.test.ts`

Layout rules:
- Parameters first, one per line, then a blank line before the expression.
- A quantifier prints its body on its own indented lines inside `{ … }`.
- A group breaks across lines only when it contains a quantifier or its single-line form exceeds 60 characters; otherwise it stays inline.
- Parenthesise a child when its precedence is looser than its parent's, or when it carries a `name` that would otherwise bind wrongly.

- [ ] **Step 1: Write the failing test**

Create `ui/packages/rules-core/test/dsl-printer.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { print } from '../src/dsl/printer.js';
import type { RuleDocument } from '../src/document.js';

describe('print', () => {
  it('prints a bare spec', () => {
    expect(print({ rule: { spec: 'is-active' } })).toBe('is-active');
  });

  it('prints an expression node in backticks', () => {
    expect(print({ rule: { expression: 'n > 0' } })).toBe('`n > 0`');
  });

  it('prints negation without a space', () => {
    expect(print({ rule: { not: { spec: 'is-flagged' } } })).toBe('!is-flagged');
  });

  it('prints an n-ary operator with its operands joined', () => {
    expect(print({ rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] } }))
      .toBe('a && b && c');
  });

  it('parenthesises a looser child inside a tighter parent', () => {
    expect(print({ rule: { and: [{ or: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } }))
      .toBe('(a | b) & c');
  });

  it('does not parenthesise a tighter child inside a looser parent', () => {
    expect(print({ rule: { andAlso: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } }))
      .toBe('a & b && c');
  });

  it('prints a name as a trailing as-clause', () => {
    expect(print({ rule: { spec: 'is-active', name: 'activity' } }))
      .toBe('is-active as "activity"');
  });

  it('parenthesises a named compound so the name binds to it', () => {
    expect(print({ rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' } }))
      .toBe('(a && b) as "pair"');
  });

  it('prints an uncounted quantifier with an indented body', () => {
    expect(print({ rule: { asAllSatisfied: { spec: 'is-positive' }, path: 'orders' } }))
      .toBe('all in orders {\n    is-positive\n}');
  });

  it('prints a counted quantifier with a literal n', () => {
    expect(print({ rule: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: 3, path: 'orders' } }))
      .toBe('atLeast(3) in orders {\n    is-positive\n}');
  });

  it('prints a param reference as n', () => {
    expect(print({
      rule: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: '@minOrders', path: 'orders' },
    })).toBe('atLeast(@minOrders) in orders {\n    is-positive\n}');
  });

  it('prints parameter declarations before a blank line', () => {
    const document: RuleDocument = {
      parameters: { minOrders: { type: 'integer', default: 3 } },
      rule: { spec: 'is-active' },
    };
    expect(print(document)).toBe('param minOrders: integer = 3\n\nis-active');
  });

  it('prints a parameter without a default', () => {
    expect(print({ parameters: { label: { type: 'string' } }, rule: { spec: 'is-active' } }))
      .toBe('param label: string\n\nis-active');
  });

  it('quotes a string default and leaves a boolean bare', () => {
    expect(print({
      parameters: { a: { type: 'string', default: 'gold' }, b: { type: 'boolean', default: true } },
      rule: { spec: 'is-active' },
    })).toBe('param a: string = "gold"\nparam b: boolean = true\n\nis-active');
  });

  it('reproduces the reference document verbatim', () => {
    const document: RuleDocument = {
      parameters: { minOrders: { type: 'integer', default: 3 } },
      rule: {
        andAlso: [
          { spec: 'is-active' },
          { or: [{ spec: 'is-verified' }, { not: { spec: 'is-flagged' } }] },
          {
            asAtLeastNSatisfied: { andAlso: [{ spec: 'is-positive' }, { spec: 'is-recent' }] },
            n: '@minOrders',
            path: 'orders',
            name: 'quota',
          },
        ],
      },
    };
    expect(print(document)).toBe(
      'param minOrders: integer = 3\n' +
      '\n' +
      'is-active && (\n' +
      '    is-verified | !is-flagged\n' +
      ') && atLeast(@minOrders) in orders {\n' +
      '    is-positive && is-recent\n' +
      '} as "quota"',
    );
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-printer.test.ts`
Expected: FAIL — cannot resolve `../src/dsl/printer.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/packages/rules-core/src/dsl/printer.ts`:

```typescript
import {
  binaryOperator, higherOrderKey, isBinaryNode, isExpressionNode, isHigherOrderNode,
  isNotNode, isSpecNode, operandsOf,
  type BinaryOperator, type RuleDocument, type RuleNode,
} from '../document.js';

const INDENT = '    ';
/** Above this width a group breaks across lines. Matches the reference layout. */
const WRAP_WIDTH = 60;

/** Node key → DSL operator, ordered loosest to tightest (index doubles as precedence). */
const OPERATOR_TEXT: Record<BinaryOperator, string> = {
  orElse: '||', andAlso: '&&', or: '|', xor: '^', and: '&',
};
const PRECEDENCE: BinaryOperator[] = ['orElse', 'andAlso', 'or', 'xor', 'and'];

const QUANTIFIER_WORDS = {
  asAllSatisfied: 'all', asAnySatisfied: 'any', asNSatisfied: 'exactly',
  asAtLeastNSatisfied: 'atLeast', asAtMostNSatisfied: 'atMost',
} as const;
const COUNTED = new Set(['asNSatisfied', 'asAtLeastNSatisfied', 'asAtMostNSatisfied']);

/** Binding tightness: higher binds tighter. Leaves and quantifiers are atomic. */
function precedenceOf(node: RuleNode): number {
  if (isBinaryNode(node)) return PRECEDENCE.indexOf(binaryOperator(node));
  return PRECEDENCE.length; // atoms bind tightest
}

/** True when the node must be wrapped in parentheses inside a parent of this precedence. */
function needsParens(node: RuleNode, parentPrecedence: number): boolean {
  if (nameOf(node) !== undefined && !isSpecNode(node) && !isExpressionNode(node)) return true;
  return precedenceOf(node) < parentPrecedence;
}

function nameOf(node: RuleNode): string | undefined {
  return (node as { name?: string }).name;
}

/** True when the subtree contains a quantifier, which always forces a multi-line body. */
function hasQuantifier(node: RuleNode): boolean {
  if (isHigherOrderNode(node)) return true;
  if (isNotNode(node)) return hasQuantifier(node.not);
  if (isBinaryNode(node)) return operandsOf(node).some(hasQuantifier);
  return false;
}

function indentLines(text: string, indent: string): string {
  return text.split('\n').map((line) => (line ? `${indent}${line}` : line)).join('\n');
}

function printNode(node: RuleNode, indent: string): string {
  const name = nameOf(node);
  const suffix = name === undefined ? '' : ` as ${JSON.stringify(name)}`;

  if (isSpecNode(node)) return `${node.spec}${suffix}`;
  if (isExpressionNode(node)) return `\`${node.expression}\`${suffix}`;

  if (isNotNode(node)) {
    const inner = printNode(node.not, indent);
    const wrapped = needsParens(node.not, PRECEDENCE.length) ? `(${inner})` : inner;
    return `!${wrapped}${suffix}`;
  }

  if (isHigherOrderNode(node)) {
    const key = higherOrderKey(node);
    const word = QUANTIFIER_WORDS[key];
    const count = COUNTED.has(key) ? `(${String((node as { n: number | string }).n)})` : '';
    const path = (node as unknown as { path: string }).path;
    const body = printNode((node as unknown as Record<string, RuleNode>)[key]!, indent + INDENT);
    return `${word}${count} in ${path} {\n${indentLines(body, indent + INDENT)}\n${indent}}${suffix}`;
  }

  const operator = binaryOperator(node);
  const precedence = PRECEDENCE.indexOf(operator);
  const parts = operandsOf(node).map((operand) => {
    const text = printNode(operand, indent);
    return needsParens(operand, precedence) ? wrapGroup(text, operand, indent) : text;
  });
  return `${parts.join(` ${OPERATOR_TEXT[operator]} `)}${suffix}`;
}

/** Wraps a parenthesised child, breaking across lines when it is long or holds a quantifier. */
function wrapGroup(text: string, node: RuleNode, indent: string): string {
  const inline = `(${text})`;
  if (!text.includes('\n') && inline.length <= WRAP_WIDTH && !hasQuantifier(node)) return inline;
  return `(\n${indentLines(text, indent + INDENT)}\n${indent})`;
}

function printParameters(document: RuleDocument): string {
  const parameters = document.parameters;
  if (!parameters) return '';
  const lines = Object.entries(parameters).map(([name, declaration]) => {
    const suffix = declaration.default === undefined
      ? ''
      : ` = ${typeof declaration.default === 'string' ? JSON.stringify(declaration.default) : String(declaration.default)}`;
    return `param ${name}: ${declaration.type}${suffix}`;
  });
  return lines.length > 0 ? `${lines.join('\n')}\n\n` : '';
}

/** Reprints a rule document in the canonical DSL form produced by the Format action. */
export function print(document: RuleDocument): string {
  return `${printParameters(document)}${printNode(document.rule, '')}`;
}
```

The reference-document test is the demanding one: `is-active && ( … ) && atLeast(…) in orders { … } as "quota"`. The `or` child is looser than `andAlso`, so it parenthesises; it contains no quantifier and is short, but the expected output breaks it across lines — so verify against the test and, if the inline form is produced, lower `WRAP_WIDTH` to `28` so `(is-verified | !is-flagged)` (27 characters plus parens) breaks. Adjust the constant, not the test.

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-printer.test.ts`
Expected: PASS — 15 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/printer.ts ui/packages/rules-core/test/dsl-printer.test.ts
git commit -m "feat(dsl): add canonical DSL printer"
```

---

## Task 8: Round-trip property tests

The parser and printer must agree. This task proves it over the whole grammar.

**Files:**
- Test: `ui/packages/rules-core/test/dsl-roundtrip.test.ts`

- [ ] **Step 1: Write the failing test**

Create `ui/packages/rules-core/test/dsl-roundtrip.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';
import { print } from '../src/dsl/printer.js';
import type { RuleDocument } from '../src/document.js';

/** One document per node kind in rule.v1.json, plus the reference composition. */
const DOCUMENTS: Array<{ label: string; document: RuleDocument }> = [
  { label: 'spec', document: { rule: { spec: 'is-active' } } },
  { label: 'named spec', document: { rule: { spec: 'is-active', name: 'activity' } } },
  { label: 'expression', document: { rule: { expression: 'n > 0' } } },
  { label: 'not', document: { rule: { not: { spec: 'is-flagged' } } } },
  { label: 'and', document: { rule: { and: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'or', document: { rule: { or: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'xor', document: { rule: { xor: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'andAlso', document: { rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'orElse', document: { rule: { orElse: [{ spec: 'a' }, { spec: 'b' }] } } },
  { label: 'n-ary', document: { rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] } } },
  {
    label: 'mixed precedence',
    document: { rule: { orElse: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } },
  },
  {
    label: 'looser child parenthesised',
    document: { rule: { and: [{ orElse: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } },
  },
  {
    label: 'asAllSatisfied',
    document: { rule: { asAllSatisfied: { spec: 'is-positive' }, path: 'orders' } },
  },
  {
    label: 'asAnySatisfied',
    document: { rule: { asAnySatisfied: { spec: 'is-positive' }, path: 'orders' } },
  },
  {
    label: 'asNSatisfied',
    document: { rule: { asNSatisfied: { spec: 'is-positive' }, n: 2, path: 'orders' } },
  },
  {
    label: 'asAtLeastNSatisfied with param',
    document: {
      rule: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: '@minOrders', path: 'orders' },
    },
  },
  {
    label: 'asAtMostNSatisfied',
    document: { rule: { asAtMostNSatisfied: { spec: 'is-positive' }, n: 1, path: 'orders' } },
  },
  {
    label: 'named quantifier',
    document: {
      rule: { asAllSatisfied: { spec: 'is-positive' }, path: 'orders', name: 'quota' },
    },
  },
  {
    label: 'named compound',
    document: { rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' } },
  },
  {
    label: 'parameters',
    document: {
      parameters: {
        minOrders: { type: 'integer', default: 3 },
        label: { type: 'string', default: 'gold' },
        strict: { type: 'boolean', default: false },
        ratio: { type: 'number' },
      },
      rule: { spec: 'is-active' },
    },
  },
  {
    label: 'reference composition',
    document: {
      parameters: { minOrders: { type: 'integer', default: 3 } },
      rule: {
        andAlso: [
          { spec: 'is-active' },
          { or: [{ spec: 'is-verified' }, { not: { spec: 'is-flagged' } }] },
          {
            asAtLeastNSatisfied: { andAlso: [{ spec: 'is-positive' }, { spec: 'is-recent' }] },
            n: '@minOrders',
            path: 'orders',
            name: 'quota',
          },
        ],
      },
    },
  },
];

describe('DSL round-trip', () => {
  it.each(DOCUMENTS)('parse(print(doc)) preserves $label', ({ document }) => {
    const text = print(document);
    const result = parse(text);
    expect(result.errors).toEqual([]);
    expect(result.document).toEqual(document);
  });

  it.each(DOCUMENTS)('print is idempotent for $label', ({ document }) => {
    const once = print(document);
    const twice = print(parse(once).document!);
    expect(twice).toBe(once);
  });

  it('every parsed node has a span', () => {
    for (const { document } of DOCUMENTS) {
      const result = parse(print(document));
      expect(result.spans.some((span) => span.path === '$.rule')).toBe(true);
    }
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-roundtrip.test.ts`
Expected: Any mismatch between parser and printer surfaces here (most likely the parenthesisation of named compounds or looser children).

- [ ] **Step 3: Reconcile parser and printer**

Fix whichever side is wrong — do not weaken the test. Likely fixes: `needsParens` must return `true` for a named non-leaf node (so `as` binds to the group), and `precedenceOf` must return the array index consistently with the parser's `BINARY_LEVELS` order (both are loosest-first, so the indices line up).

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-roundtrip.test.ts`
Expected: PASS — 43 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/test/dsl-roundtrip.test.ts ui/packages/rules-core/src/dsl/
git commit -m "test(dsl): prove parser/printer round-trip over every node kind"
```

---

## Task 9: Decoration merging

The DSL text carries structure and names only. `whenTrue`/`whenFalse` payloads live on the store's document and are re-attached after each parse.

**Files:**
- Create: `ui/packages/rules-core/src/dsl/decorations.ts`
- Test: `ui/packages/rules-core/test/dsl-decorations.test.ts`

Rule: copy `whenTrue`/`whenFalse` from `prior` onto `parsed` where the same path exists in both **and** the node kind matches. Otherwise drop them. Names come from the text and are never merged.

- [ ] **Step 1: Write the failing test**

Create `ui/packages/rules-core/test/dsl-decorations.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { mergeDecorations } from '../src/dsl/decorations.js';
import type { RuleDocument } from '../src/document.js';

describe('mergeDecorations', () => {
  it('re-attaches payloads when the structure is identical', () => {
    const prior: RuleDocument = {
      rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' },
    };
    const parsed: RuleDocument = { rule: { spec: 'is-active' } };

    expect(mergeDecorations(parsed, prior)).toEqual({
      rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' },
    });
  });

  it('re-attaches object payloads', () => {
    const prior: RuleDocument = {
      rule: { spec: 'is-active', name: 'a', whenTrue: { tier: 'gold' }, whenFalse: { tier: 'bronze' } },
    };
    const parsed: RuleDocument = { rule: { spec: 'is-active', name: 'a' } };

    expect(mergeDecorations(parsed, prior).rule).toMatchObject({
      whenTrue: { tier: 'gold' }, whenFalse: { tier: 'bronze' },
    });
  });

  it('merges payloads onto operands by indexed path', () => {
    const prior: RuleDocument = {
      rule: { andAlso: [{ spec: 'a', whenTrue: 'A' }, { spec: 'b', whenTrue: 'B' }] },
    };
    const parsed: RuleDocument = { rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }] } };

    expect(mergeDecorations(parsed, prior).rule).toEqual({
      andAlso: [{ spec: 'a', whenTrue: 'A' }, { spec: 'b', whenTrue: 'B' }],
    });
  });

  it('drops a payload when the node kind at that path changed', () => {
    const prior: RuleDocument = { rule: { spec: 'is-active', whenTrue: 'yes' } };
    const parsed: RuleDocument = { rule: { not: { spec: 'is-active' } } };

    expect(mergeDecorations(parsed, prior)).toEqual({ rule: { not: { spec: 'is-active' } } });
  });

  it('drops a payload when the spec at that path changed', () => {
    const prior: RuleDocument = { rule: { spec: 'is-active', whenTrue: 'yes' } };
    const parsed: RuleDocument = { rule: { spec: 'is-verified' } };

    expect(mergeDecorations(parsed, prior)).toEqual({ rule: { spec: 'is-verified' } });
  });

  it('drops payloads for paths that no longer exist', () => {
    const prior: RuleDocument = {
      rule: { andAlso: [{ spec: 'a', whenTrue: 'A' }, { spec: 'b', whenTrue: 'B' }] },
    };
    const parsed: RuleDocument = { rule: { spec: 'a' } };

    expect(mergeDecorations(parsed, prior)).toEqual({ rule: { spec: 'a' } });
  });

  it('keeps the name from the parsed document, not the prior one', () => {
    const prior: RuleDocument = { rule: { spec: 'is-active', name: 'old', whenTrue: 'yes' } };
    const parsed: RuleDocument = { rule: { spec: 'is-active', name: 'new' } };

    expect(mergeDecorations(parsed, prior).rule).toMatchObject({ name: 'new', whenTrue: 'yes' });
  });

  it('keeps the parameters from the parsed document', () => {
    const prior: RuleDocument = {
      parameters: { old: { type: 'integer' } }, rule: { spec: 'a' },
    };
    const parsed: RuleDocument = {
      parameters: { fresh: { type: 'string' } }, rule: { spec: 'a' },
    };

    expect(mergeDecorations(parsed, prior).parameters).toEqual({ fresh: { type: 'string' } });
  });

  it('does not mutate either input', () => {
    const prior: RuleDocument = { rule: { spec: 'a', whenTrue: 'A' } };
    const parsed: RuleDocument = { rule: { spec: 'a' } };
    const priorCopy = structuredClone(prior);
    const parsedCopy = structuredClone(parsed);

    mergeDecorations(parsed, prior);

    expect(prior).toEqual(priorCopy);
    expect(parsed).toEqual(parsedCopy);
  });

  it('merges into a quantifier body by its node-key path', () => {
    const prior: RuleDocument = {
      rule: { asAllSatisfied: { spec: 'is-positive', whenTrue: 'ok' }, path: 'orders' },
    };
    const parsed: RuleDocument = {
      rule: { asAllSatisfied: { spec: 'is-positive' }, path: 'orders' },
    };

    expect(mergeDecorations(parsed, prior).rule).toMatchObject({
      asAllSatisfied: { spec: 'is-positive', whenTrue: 'ok' },
    });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-decorations.test.ts`
Expected: FAIL — cannot resolve `../src/dsl/decorations.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/packages/rules-core/src/dsl/decorations.ts`:

```typescript
import { isSpecNode, nodeKind, type Payload, type RuleDocument, type RuleNode } from '../document.js';
import { listPaths } from '../paths.js';

/** The payload fields carried across; `name` comes from the DSL text and is never merged. */
interface Decorations { whenTrue?: Payload; whenFalse?: Payload }

/** True when two nodes are the same kind and, for specs, the same spec — so payloads still apply. */
function isCompatible(a: RuleNode, b: RuleNode): boolean {
  if (nodeKind(a) !== nodeKind(b)) return false;
  if (isSpecNode(a) && isSpecNode(b)) return a.spec === b.spec;
  return true;
}

/**
 * Re-attaches `whenTrue`/`whenFalse` payloads from a prior document onto a freshly parsed
 * one, matching nodes by path. A payload is carried over only when the node at that path
 * is compatible (same kind, and same spec for spec nodes); otherwise it is dropped, so a
 * structural edit never mis-assigns a payload to an unrelated node. Neither input is
 * mutated.
 */
export function mergeDecorations(parsed: RuleDocument, prior: RuleDocument): RuleDocument {
  const priorNodes = new Map(listPaths(prior).map(({ path, node }) => [path, node]));
  const merged = structuredClone(parsed);

  for (const { path, node } of listPaths(merged)) {
    const previous = priorNodes.get(path);
    if (!previous || !isCompatible(node, previous)) continue;

    const { whenTrue, whenFalse } = previous as Decorations;
    if (whenTrue !== undefined) (node as Decorations).whenTrue = structuredClone(whenTrue);
    if (whenFalse !== undefined) (node as Decorations).whenFalse = structuredClone(whenFalse);
  }

  return merged;
}
```

`listPaths` walks the cloned document and yields live node references, so assigning onto `node` mutates `merged` in place.

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-decorations.test.ts`
Expected: PASS — 10 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/decorations.ts ui/packages/rules-core/test/dsl-decorations.test.ts
git commit -m "feat(dsl): merge payload decorations onto parsed documents by path"
```

---

## Task 10: Export the DSL from rules-core

**Files:**
- Create: `ui/packages/rules-core/src/dsl/index.ts`
- Modify: `ui/packages/rules-core/src/index.ts`
- Test: `ui/packages/rules-core/test/dsl-exports.test.ts`

- [ ] **Step 1: Write the failing test**

Create `ui/packages/rules-core/test/dsl-exports.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { parse, print, tokenize, mergeDecorations } from '../src/index.js';

describe('DSL public exports', () => {
  it('exposes the language layer from the package root', () => {
    expect(typeof tokenize).toBe('function');
    expect(typeof parse).toBe('function');
    expect(typeof print).toBe('function');
    expect(typeof mergeDecorations).toBe('function');
  });

  it('round-trips through the public entry point', () => {
    expect(parse(print({ rule: { spec: 'is-active' } })).document)
      .toEqual({ rule: { spec: 'is-active' } });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-exports.test.ts`
Expected: FAIL — `parse` is not exported from `../src/index.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/packages/rules-core/src/dsl/index.ts`:

```typescript
export * from './types.js';
export * from './lexer.js';
export * from './parser.js';
export * from './printer.js';
export * from './decorations.js';
```

Append to `ui/packages/rules-core/src/index.ts`:

```typescript
export * from './dsl/index.js';
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `pnpm -C ui/packages/rules-core test`
Expected: PASS — the whole core suite, including all DSL files.

Run: `pnpm -C ui/packages/rules-core typecheck`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/index.ts ui/packages/rules-core/src/index.ts ui/packages/rules-core/test/dsl-exports.test.ts
git commit -m "feat(dsl): export the DSL language layer from rules-core"
```

---

## Task 11: Add CodeMirror dependencies

**Files:**
- Modify: `ui/apps/demo/package.json`

- [ ] **Step 1: Install the packages**

```bash
pnpm -C ui/apps/demo add @codemirror/state @codemirror/view @codemirror/language @codemirror/autocomplete @codemirror/lint @codemirror/commands @lezer/highlight
```

- [ ] **Step 2: Verify the demo still builds and tests pass**

Run: `pnpm -C ui/apps/demo test`
Expected: PASS — the existing suite is unaffected.

Run: `pnpm -C ui/apps/demo typecheck`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add ui/apps/demo/package.json ui/pnpm-lock.yaml
git commit -m "build(demo): add CodeMirror 6 dependencies"
```

---

## Task 12: Motiv language and theme for CodeMirror

**Files:**
- Create: `ui/apps/demo/src/dsl/motivLanguage.ts`
- Create: `ui/apps/demo/src/dsl/theme.ts`
- Test: `ui/apps/demo/test/dsl/motivLanguage.test.ts`

The `StreamLanguage` tokenizer reuses the same classification the core lexer uses, but works character-by-character over CodeMirror's `StringStream`.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/dsl/motivLanguage.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { StreamLanguage } from '@codemirror/language';
import { motivStreamParser } from '../../src/dsl/motivLanguage.js';

/** Drives the stream parser over a line and collects the emitted token names. */
function tokensOf(line: string): Array<string | null> {
  const language = StreamLanguage.define(motivStreamParser);
  const state = language.streamParser.startState!(2);
  const names: Array<string | null> = [];
  // Minimal StringStream stand-in exercised through the real parser.
  const { StringStream } = require('@codemirror/language') as typeof import('@codemirror/language');
  const stream = new StringStream(line, 2, 2, 0);
  while (!stream.eol()) {
    const name = motivStreamParser.token(stream, state);
    if (stream.current().trim() !== '') names.push(name);
    stream.start = stream.pos;
  }
  return names;
}

describe('motivStreamParser', () => {
  it('tags spec names', () => {
    expect(tokensOf('is-active')).toEqual(['variableName']);
  });

  it('tags operators', () => {
    expect(tokensOf('&& || & | ^ !')).toEqual(Array(6).fill('operator'));
  });

  it('tags keywords and quantifiers distinctly', () => {
    expect(tokensOf('param as in')).toEqual(['keyword', 'keyword', 'keyword']);
    expect(tokensOf('atLeast all any')).toEqual(['keyword', 'keyword', 'keyword']);
  });

  it('tags strings, numbers and param references', () => {
    expect(tokensOf('"quota" 3 @minOrders')).toEqual(['string', 'number', 'variableName.special']);
  });

  it('tags a backtick expression as a string', () => {
    expect(tokensOf('`n > 0`')).toEqual(['string.special']);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/motivLanguage.test.ts`
Expected: FAIL — cannot resolve `../../src/dsl/motivLanguage.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/apps/demo/src/dsl/motivLanguage.ts`:

```typescript
import { HighlightStyle, LanguageSupport, StreamLanguage, syntaxHighlighting } from '@codemirror/language';
import type { StreamParser, StringStream } from '@codemirror/language';
import { tags } from '@lezer/highlight';

const KEYWORDS = new Set(['param', 'in', 'as', 'all', 'any', 'exactly', 'atLeast', 'atMost']);
const TYPES = new Set(['integer', 'number', 'string', 'boolean']);

/** Consumes a delimited run, tolerating an unterminated one at end of line. */
function skipDelimited(stream: StringStream, delimiter: string): void {
  while (!stream.eol()) {
    if (stream.next() === delimiter) return;
  }
}

/**
 * A character-level tokenizer for the Motiv DSL. Mirrors the core lexer's classification,
 * emitting `@lezer/highlight` tag names CodeMirror can style.
 */
export const motivStreamParser: StreamParser<unknown> = {
  token(stream) {
    if (stream.eatSpace()) return null;

    if (stream.match('&&') || stream.match('||')) return 'operator';
    if (stream.match(/^[&|^!]/)) return 'operator';
    if (stream.match(/^[(){}]/)) return 'bracket';
    if (stream.match(/^[:=]/)) return 'punctuation';

    if (stream.peek() === '"') { stream.next(); skipDelimited(stream, '"'); return 'string'; }
    if (stream.peek() === '`') { stream.next(); skipDelimited(stream, '`'); return 'string.special'; }

    if (stream.match(/^@[A-Za-z0-9_-]*/)) return 'variableName.special';
    if (stream.match(/^[0-9]+/)) return 'number';

    const word = stream.match(/^[A-Za-z_][A-Za-z0-9_-]*/);
    if (word) {
      const text = Array.isArray(word) ? word[0]! : stream.current();
      if (KEYWORDS.has(text)) return 'keyword';
      if (TYPES.has(text)) return 'typeName';
      return 'variableName';
    }

    stream.next();
    return null;
  },
};

/** Maps the parser's tag names onto the demo's CSS custom properties. */
export const motivHighlightStyle = HighlightStyle.define([
  { tag: tags.variableName, color: 'var(--dsl-spec)' },
  { tag: tags.special(tags.variableName), color: 'var(--dsl-ref)' },
  { tag: tags.keyword, color: 'var(--dsl-kw)' },
  { tag: tags.typeName, color: 'var(--dsl-quant)' },
  { tag: tags.operator, color: 'var(--dsl-op)' },
  { tag: tags.bracket, color: 'var(--dsl-punct)' },
  { tag: tags.punctuation, color: 'var(--dsl-punct)' },
  { tag: tags.string, color: 'var(--dsl-str)' },
  { tag: tags.special(tags.string), color: 'var(--dsl-str)' },
  { tag: tags.number, color: 'var(--dsl-num)' },
]);

/** The Motiv DSL language, ready to drop into an EditorState's extensions. */
export function motiv(): LanguageSupport {
  return new LanguageSupport(StreamLanguage.define(motivStreamParser), [
    syntaxHighlighting(motivHighlightStyle),
  ]);
}
```

Create `ui/apps/demo/src/dsl/theme.ts`:

```typescript
import { EditorView } from '@codemirror/view';

/** Editor chrome bound to the demo's CSS custom properties, so it tracks light/dark. */
export const motivEditorTheme = EditorView.theme({
  '&': {
    height: '100%',
    fontSize: '13.5px',
    backgroundColor: 'var(--dsl-bg)',
    color: 'var(--dsl-fg)',
  },
  '.cm-content': {
    fontFamily: 'var(--mono)',
    padding: '10px 0',
    caretColor: 'var(--dsl-fg)',
  },
  '.cm-gutters': {
    backgroundColor: 'var(--dsl-gutter-bg)',
    color: 'var(--dsl-gutter)',
    border: 'none',
    borderRight: '1px solid var(--border)',
  },
  '.cm-activeLine': { backgroundColor: 'transparent' },
  '.cm-activeLineGutter': { backgroundColor: 'transparent' },
  '&.cm-focused': { outline: 'none' },
  '.cm-tooltip': {
    backgroundColor: 'var(--dsl-tooltip-bg)',
    color: 'var(--dsl-tooltip-fg)',
    border: '1px solid var(--border)',
    borderRadius: '8px',
  },
});
```

If the `tokensOf` helper in the test proves awkward against the real `StringStream` API, simplify the test to assert on rendered token classes through `DslEditor` in Task 15 instead — but keep at least one direct assertion that `motivStreamParser.token` returns `'keyword'` for `param`.

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/motivLanguage.test.ts`
Expected: PASS — 5 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/dsl/motivLanguage.ts ui/apps/demo/src/dsl/theme.ts ui/apps/demo/test/dsl/motivLanguage.test.ts
git commit -m "feat(demo): add Motiv CodeMirror language and theme"
```

---

## Task 13: Autocomplete source

**Files:**
- Create: `ui/apps/demo/src/dsl/completion.ts`
- Test: `ui/apps/demo/test/dsl/completion.test.ts`

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/dsl/completion.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { EditorState } from '@codemirror/state';
import { CompletionContext } from '@codemirror/autocomplete';
import { createMotivCompletion } from '../../src/dsl/completion.js';
import type { Catalog } from '@motiv/rules-core';

const CATALOG: Catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Currently active.' },
    { name: 'is-positive', modelType: 'order', metadataType: 'String', isAsync: false, description: 'Above zero.' },
    { name: 'is-premium', modelType: 'customer', metadataType: 'String', isAsync: true, description: 'Premium tier.' },
  ],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};

/** Runs the completion source against a document whose caret sits at the end. */
function complete(text: string) {
  const state = EditorState.create({ doc: text, selection: { anchor: text.length } });
  const context = new CompletionContext(state, text.length, true);
  return createMotivCompletion(() => CATALOG)(context);
}

describe('createMotivCompletion', () => {
  it('offers specs matching the typed prefix', () => {
    const result = complete('is-p');
    expect(result?.options.map((o) => o.label)).toContain('is-positive');
    expect(result?.options.map((o) => o.label)).toContain('is-premium');
    expect(result?.options.map((o) => o.label)).not.toContain('is-active');
  });

  it('anchors the completion at the start of the typed word', () => {
    expect(complete('is-p')?.from).toBe(0);
  });

  it('carries the spec description as detail', () => {
    const option = complete('is-a')?.options.find((o) => o.label === 'is-active');
    expect(option?.detail).toContain('Currently active.');
  });

  it('offers collections after the in keyword', () => {
    expect(complete('all in ord')?.options.map((o) => o.label)).toContain('orders');
  });

  it('offers quantifiers', () => {
    expect(complete('atL')?.options.map((o) => o.label)).toContain('atLeast');
  });

  it('offers keywords', () => {
    expect(complete('par')?.options.map((o) => o.label)).toContain('param');
  });

  it('offers parameter references declared in the document', () => {
    const labels = complete('param minOrders: integer = 3\n\natLeast(@min')?.options.map((o) => o.label);
    expect(labels).toContain('@minOrders');
  });

  it('returns null when there is no word to complete', () => {
    expect(complete('is-active ')).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/completion.test.ts`
Expected: FAIL — cannot resolve `../../src/dsl/completion.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/apps/demo/src/dsl/completion.ts`:

```typescript
import type { CompletionContext, CompletionResult, Completion } from '@codemirror/autocomplete';
import type { Catalog } from '@motiv/rules-core';

const QUANTIFIERS: Array<[string, string]> = [
  ['all', 'every element satisfies'],
  ['any', 'at least one element satisfies'],
  ['exactly', 'exactly n elements satisfy'],
  ['atLeast', 'at least n elements satisfy'],
  ['atMost', 'at most n elements satisfy'],
];
const KEYWORDS: Array<[string, string]> = [
  ['param', 'declare a parameter'],
  ['in', 'the collection to quantify over'],
  ['as', 'name this node'],
];
const TYPES = ['integer', 'number', 'string', 'boolean'];

/** Parameter names declared by `param <name>:` lines in the current document. */
function declaredParameters(text: string): string[] {
  return [...text.matchAll(/\bparam\s+([A-Za-z_][A-Za-z0-9_-]*)\s*:/g)].map((match) => match[1]!);
}

/**
 * Builds the DSL completion source. The catalog is read through a getter so the source
 * stays valid as the catalog loads asynchronously.
 */
export function createMotivCompletion(getCatalog: () => Catalog) {
  return (context: CompletionContext): CompletionResult | null => {
    const word = context.matchBefore(/[@A-Za-z_][A-Za-z0-9_-]*/);
    if (!word || (word.from === word.to && !context.explicit)) return null;

    const catalog = getCatalog();
    const text = context.state.doc.toString();
    const options: Completion[] = [];

    if (word.text.startsWith('@')) {
      for (const name of declaredParameters(text)) {
        options.push({ label: `@${name}`, type: 'variable', detail: 'parameter' });
      }
      return { from: word.from, options, validFor: /^@[A-Za-z0-9_-]*$/ };
    }

    for (const spec of catalog.specs) {
      options.push({
        label: spec.name,
        type: 'function',
        detail: [spec.description ?? '', spec.isAsync ? '(async)' : ''].filter(Boolean).join(' '),
        info: `${spec.modelType} · ${spec.metadataType}`,
      });
    }
    for (const collection of catalog.collections) {
      options.push({
        label: collection.path,
        type: 'variable',
        detail: `collection · ${collection.elementModelType}[]`,
      });
    }
    for (const [label, detail] of QUANTIFIERS) options.push({ label, type: 'keyword', detail });
    for (const [label, detail] of KEYWORDS) options.push({ label, type: 'keyword', detail });
    for (const label of TYPES) options.push({ label, type: 'type', detail: 'parameter type' });

    return { from: word.from, options, validFor: /^[A-Za-z0-9_-]*$/ };
  };
}
```

CodeMirror filters and bolds the typed prefix itself, so the source returns the full option set anchored at `word.from` and lets `validFor` handle refinement — this is why the "matching the typed prefix" test passes without manual filtering (CodeMirror's `FuzzyMatcher` applies to the returned options).

If the first test fails because unfiltered options are returned verbatim, add an explicit prefix filter before returning:

```typescript
    const prefix = word.text.toLowerCase();
    const matching = options.filter((option) => option.label.toLowerCase().startsWith(prefix));
    return { from: word.from, options: matching, validFor: /^[A-Za-z0-9_-]*$/ };
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/completion.test.ts`
Expected: PASS — 8 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/dsl/completion.ts ui/apps/demo/test/dsl/completion.test.ts
git commit -m "feat(demo): add catalog-driven DSL autocomplete source"
```

---

## Task 14: Lint and hover

Diagnostics come from two places: parser errors (native offsets) and backend `RuleError`s (backend paths, mapped through `spans`).

**Files:**
- Create: `ui/apps/demo/src/dsl/lint.ts`
- Create: `ui/apps/demo/src/dsl/hover.ts`
- Test: `ui/apps/demo/test/dsl/lint.test.ts`

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/dsl/lint.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { parse } from '@motiv/rules-core';
import type { RuleError } from '@motiv/rules-core';
import { diagnosticsFor } from '../../src/dsl/lint.js';

describe('diagnosticsFor', () => {
  it('maps a parser error onto its source range', () => {
    const text = '(is-active';
    const diagnostics = diagnosticsFor(text, parse(text), []);
    expect(diagnostics).toContainEqual(
      expect.objectContaining({ from: 0, to: 1, severity: 'error' }),
    );
  });

  it('maps a backend error onto the span of its path', () => {
    const text = 'is-nonsense';
    const errors: RuleError[] = [
      { path: '$.rule', code: 'UnknownSpec', message: "'is-nonsense' is not a registered spec" },
    ];
    const diagnostics = diagnosticsFor(text, parse(text), errors);
    expect(diagnostics).toContainEqual(
      expect.objectContaining({ from: 0, to: 11, message: expect.stringContaining('registered') }),
    );
  });

  it('maps a backend error on a nested path onto that operand', () => {
    const text = 'is-active && is-nonsense';
    const errors: RuleError[] = [
      { path: '$.rule.andAlso[1]', code: 'UnknownSpec', message: 'unknown' },
    ];
    const diagnostics = diagnosticsFor(text, parse(text), errors);
    expect(diagnostics).toContainEqual(expect.objectContaining({ from: 13, to: 24 }));
  });

  it('falls back to the whole document when a path has no span', () => {
    const text = 'is-active';
    const errors: RuleError[] = [
      { path: '$.rule.andAlso[7]', code: 'InvalidNode', message: 'nope' },
    ];
    const diagnostics = diagnosticsFor(text, parse(text), errors);
    expect(diagnostics).toContainEqual(expect.objectContaining({ from: 0, to: text.length }));
  });

  it('includes the error code in the message', () => {
    const text = 'is-nonsense';
    const errors: RuleError[] = [{ path: '$.rule', code: 'UnknownSpec', message: 'unknown' }];
    expect(diagnosticsFor(text, parse(text), errors)[0]!.message).toContain('UnknownSpec');
  });

  it('returns no diagnostics for a clean document', () => {
    const text = 'is-active';
    expect(diagnosticsFor(text, parse(text), [])).toEqual([]);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/lint.test.ts`
Expected: FAIL — cannot resolve `../../src/dsl/lint.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/apps/demo/src/dsl/lint.ts`:

```typescript
import type { Diagnostic } from '@codemirror/lint';
import type { ParseResult, RuleError } from '@motiv/rules-core';

/** Resolves the text range for a backend node path, falling back to the whole document. */
function rangeFor(path: string, result: ParseResult, length: number): { from: number; to: number } {
  // Backend errors may address a sub-field (e.g. `$.rule.whenTrue`); walk up to the node.
  let candidate = path;
  while (candidate.length > 0) {
    const span = result.spans.find((entry) => entry.path === candidate);
    if (span) return { from: span.from, to: span.to };
    const cut = candidate.lastIndexOf('.');
    if (cut <= 0) break;
    candidate = candidate.slice(0, cut);
  }
  return { from: 0, to: length };
}

/**
 * Folds parser errors and backend validation errors into CodeMirror diagnostics.
 * Parser errors already carry source offsets; backend errors are keyed by node path and
 * are mapped through the parse result's spans.
 */
export function diagnosticsFor(
  text: string,
  result: ParseResult,
  errors: RuleError[],
): Diagnostic[] {
  const diagnostics: Diagnostic[] = result.errors.map((error) => ({
    from: error.from,
    to: Math.max(error.to, error.from + 1),
    severity: 'error' as const,
    message: `${error.code}: ${error.message}`,
  }));

  for (const error of errors) {
    const { from, to } = rangeFor(error.path, result, text.length);
    diagnostics.push({
      from,
      to: Math.max(to, from + 1),
      severity: 'error',
      message: `${error.code}: ${error.message}`,
      source: error.path,
    });
  }

  return diagnostics;
}
```

Create `ui/apps/demo/src/dsl/hover.ts`:

```typescript
import { hoverTooltip, type Tooltip } from '@codemirror/view';
import type { Diagnostic } from '@codemirror/lint';

/** Renders the tooltip body: code, message, and the node path the error is anchored to. */
function renderTooltip(diagnostic: Diagnostic): HTMLElement {
  const dom = document.createElement('div');
  dom.className = 'dsl-hover';

  const [code, ...rest] = diagnostic.message.split(': ');
  const heading = document.createElement('div');
  heading.className = 'dsl-hover-code';
  heading.textContent = code ?? '';
  dom.append(heading);

  const message = document.createElement('div');
  message.className = 'dsl-hover-message';
  message.textContent = rest.join(': ');
  dom.append(message);

  if (diagnostic.source) {
    const path = document.createElement('code');
    path.className = 'dsl-hover-path';
    path.textContent = diagnostic.source;
    dom.append(path);
  }
  return dom;
}

/**
 * A hover tooltip over the current diagnostics. Diagnostics are supplied by a getter so
 * the tooltip always reads the latest lint pass without rebuilding the extension.
 */
export function motivHover(getDiagnostics: () => Diagnostic[]) {
  return hoverTooltip((_view, pos): Tooltip | null => {
    const hit = getDiagnostics().find((d) => pos >= d.from && pos <= d.to);
    if (!hit) return null;
    return { pos: hit.from, end: hit.to, above: true, create: () => ({ dom: renderTooltip(hit) }) };
  });
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/lint.test.ts`
Expected: PASS — 6 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/dsl/lint.ts ui/apps/demo/src/dsl/hover.ts ui/apps/demo/test/dsl/lint.test.ts
git commit -m "feat(demo): map parser and backend errors to editor diagnostics"
```

---

## Task 15: The sync hook

`useDslSync` is the state machine from the spec: text is the source of truth, edits debounce-commit into the store, and an external store change under a dirty buffer raises a conflict.

**Files:**
- Create: `ui/apps/demo/src/dsl/useDslSync.ts`
- Test: `ui/apps/demo/test/dsl/useDslSync.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/dsl/useDslSync.test.tsx`:

```typescript
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { RuleEditorStore } from '@motiv/rules-core';
import { useDslSync } from '../../src/dsl/useDslSync.js';

describe('useDslSync', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('starts synced, printing the store document into the buffer', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    expect(result.current.text).toBe('is-active');
    expect(result.current.status).toBe('synced');
  });

  it('marks the buffer dirty as soon as the text changes', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-verified'));

    expect(result.current.status).toBe('dirty');
  });

  it('commits a clean parse to the store after the debounce', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-verified'));
    act(() => { vi.advanceTimersByTime(300); });

    expect(store.getState().document).toEqual({ rule: { spec: 'is-verified' } });
    expect(result.current.status).toBe('synced');
  });

  it('does not commit unparseable text and reports an error status', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('(is-active'));
    act(() => { vi.advanceTimersByTime(300); });

    expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } });
    expect(result.current.status).toBe('error');
  });

  it('preserves payloads from the store across a text edit', () => {
    const store = new RuleEditorStore({
      rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' },
    });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-active as "activity"'));
    act(() => { vi.advanceTimersByTime(300); });

    expect(store.getState().document.rule).toMatchObject({
      spec: 'is-active', name: 'activity', whenTrue: 'yes', whenFalse: 'no',
    });
  });

  it('reprints silently when the store changes and the buffer is clean', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));

    expect(result.current.text).toBe('is-verified');
    expect(result.current.conflict).toBe(false);
    expect(result.current.status).toBe('synced');
  });

  it('raises a conflict when the store changes while the buffer is dirty', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-recent'));
    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));

    expect(result.current.conflict).toBe(true);
    expect(result.current.text).toBe('is-recent');
  });

  it('reformat from tree discards local text and clears the conflict', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-recent'));
    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));
    act(() => result.current.reformatFromTree());

    expect(result.current.text).toBe('is-verified');
    expect(result.current.conflict).toBe(false);
    expect(result.current.status).toBe('synced');
  });

  it('keep editing dismisses the conflict but keeps the local text', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-recent'));
    act(() => store.replaceNode('$.rule', { spec: 'is-verified' }));
    act(() => result.current.keepEditing());

    expect(result.current.conflict).toBe(false);
    expect(result.current.text).toBe('is-recent');
  });

  it('format reprints the current buffer canonically', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-active   &&   is-recent'));
    act(() => { vi.advanceTimersByTime(300); });
    act(() => result.current.format());

    expect(result.current.text).toBe('is-active && is-recent');
  });

  it('does not treat its own commit as an external change', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { result } = renderHook(() => useDslSync(store));

    act(() => result.current.setText('is-active && is-recent'));
    act(() => { vi.advanceTimersByTime(300); });

    expect(result.current.conflict).toBe(false);
    expect(result.current.text).toBe('is-active && is-recent');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/useDslSync.test.tsx`
Expected: FAIL — cannot resolve `../../src/dsl/useDslSync.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/apps/demo/src/dsl/useDslSync.ts`:

```typescript
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  mergeDecorations, parse, print,
  type ParseResult, type RuleDocument, type RuleEditorStore,
} from '@motiv/rules-core';

/** How long the buffer must be idle before a clean parse is committed to the store. */
const COMMIT_DEBOUNCE_MS = 300;

/** Whether the buffer agrees with the store, has uncommitted edits, or cannot be parsed. */
export type SyncStatus = 'synced' | 'dirty' | 'error';

export interface DslSync {
  text: string;
  status: SyncStatus;
  /** True when the store changed underneath a dirty buffer and the user must choose. */
  conflict: boolean;
  parseResult: ParseResult;
  setText: (text: string) => void;
  /** Canonically reprints the buffer (the Format action). */
  format: () => void;
  /** Discards local text and reprints from the store's document. */
  reformatFromTree: () => void;
  /** Dismisses the conflict, keeping the local text as the pending source. */
  keepEditing: () => void;
}

/**
 * Binds a DSL text buffer to a {@link RuleEditorStore} with the text as the source of
 * truth: edits debounce-parse and commit into the store, while changes made elsewhere
 * (the Builder) reprint into a clean buffer or raise a conflict against a dirty one.
 */
export function useDslSync(store: RuleEditorStore): DslSync {
  const [text, setTextState] = useState(() => print(store.getState().document));
  const [status, setStatus] = useState<SyncStatus>('synced');
  const [conflict, setConflict] = useState(false);
  const [parseResult, setParseResult] = useState<ParseResult>(() => parse(print(store.getState().document)));

  /** The store document this buffer is known to agree with. */
  const baseDocument = useRef<RuleDocument>(store.getState().document);
  /** Set while we are the ones writing to the store, so the subscription ignores the echo. */
  const selfCommitting = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const dirty = useRef(false);

  const commit = useCallback((next: string): void => {
    const result = parse(next);
    setParseResult(result);
    if (!result.document) {
      setStatus('error');
      return;
    }
    const merged = mergeDecorations(result.document, store.getState().document);
    selfCommitting.current = true;
    store.loadDocument(merged);
    baseDocument.current = store.getState().document;
    selfCommitting.current = false;
    dirty.current = false;
    setStatus('synced');
  }, [store]);

  const setText = useCallback((next: string): void => {
    setTextState(next);
    dirty.current = true;
    setStatus('dirty');
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => commit(next), COMMIT_DEBOUNCE_MS);
  }, [commit]);

  // React to changes the Builder (or any other surface) made to the same store.
  useEffect(() => store.subscribe(() => {
    if (selfCommitting.current) return;
    const { document } = store.getState();
    if (document === baseDocument.current) return; // not a document change (e.g. setErrors)
    baseDocument.current = document;
    if (dirty.current) {
      setConflict(true);
      return;
    }
    const printed = print(document);
    setTextState(printed);
    setParseResult(parse(printed));
    setStatus('synced');
  }), [store]);

  useEffect(() => () => { if (timer.current) clearTimeout(timer.current); }, []);

  const format = useCallback((): void => {
    const result = parse(text);
    if (!result.document) return;
    const printed = print(result.document);
    setTextState(printed);
    setParseResult(parse(printed));
  }, [text]);

  const reformatFromTree = useCallback((): void => {
    const printed = print(store.getState().document);
    if (timer.current) clearTimeout(timer.current);
    baseDocument.current = store.getState().document;
    dirty.current = false;
    setTextState(printed);
    setParseResult(parse(printed));
    setConflict(false);
    setStatus('synced');
  }, [store]);

  const keepEditing = useCallback((): void => setConflict(false), []);

  return { text, status, conflict, parseResult, setText, format, reformatFromTree, keepEditing };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/useDslSync.test.tsx`
Expected: PASS — 11 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/dsl/useDslSync.ts ui/apps/demo/test/dsl/useDslSync.test.tsx
git commit -m "feat(demo): add DSL text/store sync with conflict handling"
```

---

## Task 16: Payload popover

Clicking a spec token opens an editor for that node's `name`, `whenTrue` and `whenFalse`. String mode when the spec's `metadataType` is `String`; JSON object mode otherwise.

**Files:**
- Create: `ui/apps/demo/src/dsl/PayloadPopover.tsx`
- Test: `ui/apps/demo/test/dsl/PayloadPopover.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/dsl/PayloadPopover.test.tsx`:

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore } from '@motiv/rules-core';
import type { Catalog } from '@motiv/rules-core';
import { PayloadPopover } from '../../src/dsl/PayloadPopover.js';

const CATALOG: Catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Currently active.' },
    { name: 'is-tiered', modelType: 'customer', metadataType: 'Tier', isAsync: false, description: 'Tiered.' },
  ],
  collections: [],
  metadataTypes: {
    Tier: { type: 'object', properties: { tier: { type: 'string' } } },
  },
};

function renderPopover(overrides: Partial<Parameters<typeof PayloadPopover>[0]> = {}) {
  const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
  const onClose = vi.fn();
  render(
    <PayloadPopover
      store={store}
      catalog={CATALOG}
      path="$.rule"
      spec="is-active"
      onClose={onClose}
      {...overrides}
    />,
  );
  return { store, onClose };
}

describe('PayloadPopover', () => {
  it('shows the spec name and its catalog description', () => {
    renderPopover();
    expect(screen.getByText('is-active')).toBeTruthy();
    expect(screen.getByText(/Currently active/)).toBeTruthy();
  });

  it('saves the node name to the store', async () => {
    const user = userEvent.setup();
    const { store } = renderPopover();

    await user.type(screen.getByLabelText('Name'), 'activity');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(store.getState().document.rule).toMatchObject({ name: 'activity' });
  });

  it('saves string payloads for an Explanation spec', async () => {
    const user = userEvent.setup();
    const { store } = renderPopover();

    await user.type(screen.getByLabelText('When true'), 'is active');
    await user.type(screen.getByLabelText('When false'), 'not active');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(store.getState().document.rule).toMatchObject({
      whenTrue: 'is active', whenFalse: 'not active',
    });
  });

  it('saves object payloads for an object metadata spec', async () => {
    const user = userEvent.setup();
    const store = new RuleEditorStore({ rule: { spec: 'is-tiered', name: 'tier' } });
    render(
      <PayloadPopover
        store={store} catalog={CATALOG} path="$.rule" spec="is-tiered" onClose={vi.fn()}
      />,
    );

    const whenTrue = screen.getByLabelText('When true');
    await user.clear(whenTrue);
    await user.type(whenTrue, '{{"tier": "gold"}');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(store.getState().document.rule).toMatchObject({ whenTrue: { tier: 'gold' } });
  });

  it('reports invalid JSON instead of saving', async () => {
    const user = userEvent.setup();
    const store = new RuleEditorStore({ rule: { spec: 'is-tiered', name: 'tier' } });
    render(
      <PayloadPopover
        store={store} catalog={CATALOG} path="$.rule" spec="is-tiered" onClose={vi.fn()}
      />,
    );

    await user.type(screen.getByLabelText('When true'), '{{not json');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(screen.getByRole('alert')).toBeTruthy();
    expect(store.getState().document.rule).not.toHaveProperty('whenTrue');
  });

  it('closes without saving on cancel', async () => {
    const user = userEvent.setup();
    const { store, onClose } = renderPopover();

    await user.type(screen.getByLabelText('Name'), 'ignored');
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalled();
    expect(store.getState().document.rule).not.toHaveProperty('name');
  });

  it('pre-fills existing decorations', () => {
    const store = new RuleEditorStore({
      rule: { spec: 'is-active', name: 'activity', whenTrue: 'yes', whenFalse: 'no' },
    });
    render(
      <PayloadPopover
        store={store} catalog={CATALOG} path="$.rule" spec="is-active" onClose={vi.fn()}
      />,
    );

    expect(screen.getByLabelText<HTMLInputElement>('Name').value).toBe('activity');
    expect(screen.getByLabelText<HTMLTextAreaElement>('When true').value).toBe('yes');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/PayloadPopover.test.tsx`
Expected: FAIL — cannot resolve `../../src/dsl/PayloadPopover.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/apps/demo/src/dsl/PayloadPopover.tsx`:

```typescript
import { useMemo, useState } from 'react';
import { getNode, type Catalog, type Payload, type RuleEditorStore } from '@motiv/rules-core';

export interface PayloadPopoverProps {
  store: RuleEditorStore;
  catalog: Catalog;
  /** Backend path of the node being decorated, e.g. `$.rule.andAlso[0]`. */
  path: string;
  /** The spec name at that path, used to look the entry up in the catalog. */
  spec: string;
  onClose: () => void;
}

/** Renders a payload for editing: strings verbatim, objects as pretty JSON. */
function toFieldValue(payload: Payload | undefined): string {
  if (payload === undefined) return '';
  return typeof payload === 'string' ? payload : JSON.stringify(payload, null, 2);
}

/**
 * Edits one node's name and whenTrue/whenFalse payloads. Specs whose metadata is a plain
 * explanation get string fields; object-metadata specs get JSON fields validated on save.
 */
export function PayloadPopover(props: PayloadPopoverProps) {
  const { store, catalog, path, spec, onClose } = props;
  const entry = catalog.specs.find((candidate) => candidate.name === spec);
  const isObjectMode = !!entry && entry.metadataType !== 'String' && entry.metadataType !== 'Explanation';

  const node = getNode(store.getState().document, path);
  const decoration = (node ?? {}) as { name?: string; whenTrue?: Payload; whenFalse?: Payload };

  const [name, setName] = useState(decoration.name ?? '');
  const [whenTrue, setWhenTrue] = useState(() => toFieldValue(decoration.whenTrue));
  const [whenFalse, setWhenFalse] = useState(() => toFieldValue(decoration.whenFalse));
  const [error, setError] = useState<string | null>(null);

  const elementSchema = useMemo(
    () => (entry ? catalog.metadataTypes?.[entry.metadataType] : undefined),
    [catalog.metadataTypes, entry],
  );

  const parseField = (value: string, label: string): Payload | undefined | Error => {
    if (value.trim() === '') return undefined;
    if (!isObjectMode) return value;
    try {
      return JSON.parse(value) as Payload;
    } catch {
      return new Error(`${label} is not valid JSON.`);
    }
  };

  const save = (): void => {
    const parsedTrue = parseField(whenTrue, 'When true');
    const parsedFalse = parseField(whenFalse, 'When false');
    if (parsedTrue instanceof Error) return setError(parsedTrue.message);
    if (parsedFalse instanceof Error) return setError(parsedFalse.message);

    store.setName(path, name.trim() === '' ? undefined : name.trim());
    store.setDecoration(path, { whenTrue: parsedTrue, whenFalse: parsedFalse });
    setError(null);
    onClose();
  };

  return (
    <div className="dsl-popover" role="dialog" aria-label={`Payload for ${spec}`}>
      <header className="dsl-popover-head">
        <span className="dsl-popover-spec">{spec}</span>
        {entry?.isAsync && <span className="dsl-badge">async</span>}
        <button type="button" className="dsl-popover-close" onClick={onClose} aria-label="Close">×</button>
      </header>

      {entry?.description && <p className="dsl-popover-desc">{entry.description}</p>}
      <dl className="dsl-popover-meta">
        <div><dt>model type</dt><dd>{entry?.modelType ?? '—'}</dd></div>
        <div><dt>returns</dt><dd>{entry?.metadataType ?? '—'}</dd></div>
      </dl>

      <label className="dsl-field">
        <span>Name</span>
        <input value={name} onChange={(event) => setName(event.target.value)} placeholder="Optional node name" />
      </label>

      <label className="dsl-field">
        <span>When true</span>
        <textarea
          value={whenTrue}
          onChange={(event) => setWhenTrue(event.target.value)}
          spellCheck={false}
          placeholder={isObjectMode ? '{ }' : 'Why the result is true…'}
        />
      </label>

      <label className="dsl-field">
        <span>When false</span>
        <textarea
          value={whenFalse}
          onChange={(event) => setWhenFalse(event.target.value)}
          spellCheck={false}
          placeholder={isObjectMode ? '{ }' : 'Why the result is false…'}
        />
      </label>

      {isObjectMode && elementSchema?.properties && (
        <p className="dsl-popover-hint">
          Keys: {Object.keys(elementSchema.properties).join(', ')}
        </p>
      )}
      {error && <p role="alert" className="dsl-popover-error">{error}</p>}

      <footer className="dsl-popover-actions">
        <button type="button" onClick={onClose}>Cancel</button>
        <button type="button" className="primary" onClick={save}>Save</button>
      </footer>
    </div>
  );
}
```

`store.setDecoration` accepts `undefined` values, which clears a payload — that is the desired behaviour when a field is emptied.

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/PayloadPopover.test.tsx`
Expected: PASS — 7 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/dsl/PayloadPopover.tsx ui/apps/demo/test/dsl/PayloadPopover.test.tsx
git commit -m "feat(demo): add spec payload popover"
```

---

## Task 17: The DSL editor component

Assembles CodeMirror with the language, completion, lint and hover extensions, plus the toolbar (Format, filename, sync pill), the conflict banner, and the popover.

**Files:**
- Create: `ui/apps/demo/src/dsl/DslEditor.tsx`
- Test: `ui/apps/demo/test/dsl/DslEditor.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/dsl/DslEditor.test.tsx`:

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore } from '@motiv/rules-core';
import type { Catalog } from '@motiv/rules-core';
import { DslEditor } from '../../src/dsl/DslEditor.js';

const CATALOG: Catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Active.' },
    { name: 'is-verified', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Verified.' },
  ],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};

describe('DslEditor', () => {
  it('renders the document text in the editor', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    render(<DslEditor store={store} catalog={CATALOG} />);

    await waitFor(() => expect(screen.getByText('is-active')).toBeTruthy());
  });

  it('shows the synced status by default', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    render(<DslEditor store={store} catalog={CATALOG} />);

    expect(screen.getByLabelText('sync status').textContent).toMatch(/synced/i);
  });

  it('exposes a Format action', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    render(<DslEditor store={store} catalog={CATALOG} />);

    expect(screen.getByRole('button', { name: /format/i })).toBeTruthy();
  });

  it('shows the conflict banner when the store changes under a dirty buffer', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { rerender } = render(<DslEditor store={store} catalog={CATALOG} />);

    // Simulate a dirty buffer, then an external change.
    await waitFor(() => expect(screen.getByLabelText('sync status')).toBeTruthy());
    const view = document.querySelector('.cm-content');
    expect(view).toBeTruthy();

    store.replaceNode('$.rule', { spec: 'is-verified' });
    rerender(<DslEditor store={store} catalog={CATALOG} />);

    // A clean buffer reprints silently rather than raising a conflict.
    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
  });

  it('renders the conflict banner actions when a conflict is active', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    render(<DslEditor store={store} catalog={CATALOG} conflictForTesting />);

    expect(screen.getByRole('button', { name: /reformat from tree/i })).toBeTruthy();
    expect(screen.getByRole('button', { name: /keep editing/i })).toBeTruthy();
  });
});
```

If the `conflictForTesting` escape hatch feels wrong, drive the conflict through the real path instead: type into the CodeMirror content element with `userEvent`, then call `store.replaceNode`. Prefer the real path if it works in jsdom; keep the prop only as a fallback and delete it if unused.

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/DslEditor.test.tsx`
Expected: FAIL — cannot resolve `../../src/dsl/DslEditor.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/apps/demo/src/dsl/DslEditor.tsx`:

```typescript
import { useEffect, useMemo, useRef, useState } from 'react';
import { EditorState } from '@codemirror/state';
import { EditorView, keymap, lineNumbers, highlightActiveLine } from '@codemirror/view';
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
import { autocompletion, completionKeymap } from '@codemirror/autocomplete';
import { linter, lintKeymap, type Diagnostic } from '@codemirror/lint';
import { getNode, isSpecNode, type Catalog, type RuleEditorStore } from '@motiv/rules-core';
import { motiv } from './motivLanguage.js';
import { motivEditorTheme } from './theme.js';
import { createMotivCompletion } from './completion.js';
import { diagnosticsFor } from './lint.js';
import { motivHover } from './hover.js';
import { useDslSync } from './useDslSync.js';
import { PayloadPopover } from './PayloadPopover.js';
import { useRuleEditor } from '@motiv/rules-react';

export interface DslEditorProps {
  store: RuleEditorStore;
  catalog: Catalog;
  /** Test-only: forces the conflict banner open. */
  conflictForTesting?: boolean;
}

const STATUS_TEXT: Record<string, string> = {
  synced: 'synced', dirty: 'unsynced', error: 'parse error',
};

/** The DSL surface: a CodeMirror editor over the rule, plus its toolbar and popover. */
export function DslEditor(props: DslEditorProps) {
  const { store, catalog } = props;
  const sync = useDslSync(store);
  const editorState = useRuleEditor(store);
  const host = useRef<HTMLDivElement | null>(null);
  const view = useRef<EditorView | null>(null);
  const [popover, setPopover] = useState<{ path: string; spec: string } | null>(null);

  // Latest values read by CodeMirror extensions, which are built once.
  const latest = useRef({ sync, catalog, errors: editorState.errors });
  latest.current = { sync, catalog, errors: editorState.errors };

  const diagnostics = useMemo(
    () => diagnosticsFor(sync.text, sync.parseResult, editorState.errors),
    [sync.text, sync.parseResult, editorState.errors],
  );
  const diagnosticsRef = useRef<Diagnostic[]>(diagnostics);
  diagnosticsRef.current = diagnostics;

  /** Opens the payload popover for the spec node under a document offset. */
  const openPopoverAt = (offset: number): void => {
    const span = [...latest.current.sync.parseResult.spans]
      .filter((entry) => offset >= entry.from && offset <= entry.to)
      .sort((a, b) => (b.to - b.from) - (a.to - a.from))
      .pop();
    if (!span) return setPopover(null);
    const node = getNode(store.getState().document, span.path);
    if (node && isSpecNode(node)) setPopover({ path: span.path, spec: node.spec });
    else setPopover(null);
  };

  useEffect(() => {
    if (!host.current) return;
    const state = EditorState.create({
      doc: latest.current.sync.text,
      extensions: [
        lineNumbers(),
        highlightActiveLine(),
        history(),
        keymap.of([...defaultKeymap, ...historyKeymap, ...completionKeymap, ...lintKeymap]),
        motiv(),
        motivEditorTheme,
        autocompletion({ override: [createMotivCompletion(() => latest.current.catalog)] }),
        linter(() => diagnosticsRef.current),
        motivHover(() => diagnosticsRef.current),
        EditorView.updateListener.of((update) => {
          if (update.docChanged) latest.current.sync.setText(update.state.doc.toString());
          else if (update.selectionSet) openPopoverAt(update.state.selection.main.head);
        }),
      ],
    });
    const instance = new EditorView({ state, parent: host.current });
    view.current = instance;
    return () => { instance.destroy(); view.current = null; };
    // Built once: live values are read through `latest`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Push text the hook produced (format, reprint, conflict resolution) into the editor.
  useEffect(() => {
    const instance = view.current;
    if (!instance) return;
    const current = instance.state.doc.toString();
    if (current === sync.text) return;
    instance.dispatch({ changes: { from: 0, to: current.length, insert: sync.text } });
  }, [sync.text]);

  const conflict = props.conflictForTesting || sync.conflict;

  return (
    <div className="dsl">
      <div className="dsl-toolbar">
        <button type="button" className="btn" onClick={sync.format} title="Reprint the document canonically">
          Format
        </button>
        <span className="dsl-filename">quota-rule.motiv</span>
        <span className={`dsl-pill dsl-pill-${sync.status}`} aria-label="sync status">
          {STATUS_TEXT[sync.status]}
        </span>
      </div>

      {conflict && (
        <div className="dsl-banner" role="alert">
          <span>
            The rule changed in the <b>Builder</b> while your DSL was unsaved. Your local edits are kept below.
          </span>
          <button type="button" onClick={sync.reformatFromTree}>Reformat from tree</button>
          <button type="button" onClick={sync.keepEditing}>Keep editing</button>
        </div>
      )}

      <div className="dsl-surface" ref={host} />

      {popover && (
        <PayloadPopover
          store={store}
          catalog={catalog}
          path={popover.path}
          spec={popover.spec}
          onClose={() => setPopover(null)}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm -C ui/apps/demo exec vitest run test/dsl/DslEditor.test.tsx`
Expected: PASS — 5 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/dsl/DslEditor.tsx ui/apps/demo/test/dsl/DslEditor.test.tsx
git commit -m "feat(demo): assemble the DSL editor component"
```

---

## Task 18: Builder ⇄ DSL toggle in the shell

**Files:**
- Create: `ui/apps/demo/src/panes/EditorPane.tsx`
- Modify: `ui/apps/demo/src/panes/BuilderPane.tsx`
- Modify: `ui/apps/demo/src/App.tsx`
- Test: `ui/apps/demo/test/panes/EditorPane.test.tsx`

`BuilderPane` currently renders its own `<section className="pane">` wrapper with a header. The toggle needs to sit in that header slot for both surfaces, so extract the Builder's body into `BuilderBody` and let `EditorPane` own the wrapper.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/panes/EditorPane.test.tsx`:

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore, RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { EditorPane } from '../../src/panes/EditorPane.js';

const CATALOG = {
  specs: [{ name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Active.' }],
  collections: [],
};

function renderPane() {
  const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(CATALOG), { status: 200, headers: { 'content-type': 'application/json' } }),
  );
  const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });
  render(
    <RuleEditorProvider store={store}>
      <EditorPane client={client} />
    </RuleEditorProvider>,
  );
  return { store };
}

describe('EditorPane', () => {
  it('shows the Builder surface by default', () => {
    renderPane();
    expect(screen.getByRole('tab', { name: 'Builder' }).getAttribute('aria-selected')).toBe('true');
  });

  it('switches to the DSL surface when the DSL tab is chosen', async () => {
    const user = userEvent.setup();
    renderPane();

    await user.click(screen.getByRole('tab', { name: 'DSL' }));

    expect(screen.getByRole('tab', { name: 'DSL' }).getAttribute('aria-selected')).toBe('true');
    await waitFor(() => expect(screen.getByLabelText('sync status')).toBeTruthy());
  });

  it('switches back to the Builder', async () => {
    const user = userEvent.setup();
    renderPane();

    await user.click(screen.getByRole('tab', { name: 'DSL' }));
    await user.click(screen.getByRole('tab', { name: 'Builder' }));

    expect(screen.queryByLabelText('sync status')).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm -C ui/apps/demo exec vitest run test/panes/EditorPane.test.tsx`
Expected: FAIL — cannot resolve `../../src/panes/EditorPane.js`.

- [ ] **Step 3: Extract the Builder body**

In `ui/apps/demo/src/panes/BuilderPane.tsx`, replace the exported `BuilderPane` component (lines 36-81) with a body component plus a thin wrapper that preserves the existing standalone usage:

```typescript
/** The recursive single-open-accordion rule builder over the boolean grammar. */
export function BuilderBody(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;

  const [expanded, setExpanded] = useState<Set<string>>(() => initialExpanded(store.getState().document));

  const toggle = (path: string): void => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(path)) {
        next.delete(path);
        return next;
      }
      const prefix = parentPrefixOf(path);
      if (prefix !== null) {
        for (const candidate of next) {
          if (candidate !== path && parentPrefixOf(candidate) === prefix) next.delete(candidate);
        }
      }
      next.add(path);
      return next;
    });
  };

  return (
    <>
      {catalogState.status === 'loading' && <p>Loading catalog…</p>}
      {catalogState.status === 'error' && <p role="alert">Failed to load catalog.</p>}
      <AccordionContext.Provider value={{ isExpanded: (path) => expanded.has(path), toggle, catalog }}>
        <RuleNodeEditor path={ROOT} depth={0} modelType={MODEL_TYPE} />
      </AccordionContext.Provider>
    </>
  );
}

/** The Builder as a standalone pane, retained for direct use and existing tests. */
export function BuilderPane(props: { client: RulesApiClient }) {
  return (
    <section className="pane" aria-label="Builder">
      <div className="pane-header">
        <h2>Builder</h2>
        <button type="button" className="btn ext-point" disabled title="requires backend (coming)">
          parameters — coming
        </button>
      </div>
      <BuilderBody client={props.client} />
    </section>
  );
}
```

- [ ] **Step 4: Write the EditorPane**

Create `ui/apps/demo/src/panes/EditorPane.tsx`:

```typescript
import { useState } from 'react';
import type { Catalog, RulesApiClient } from '@motiv/rules-core';
import { useCatalog, useRuleEditorStore } from '@motiv/rules-react';
import { BuilderBody } from './BuilderPane.js';
import { DslEditor } from '../dsl/DslEditor.js';

const EMPTY_CATALOG: Catalog = { specs: [], collections: [] };

type Surface = 'builder' | 'dsl';

/**
 * The left-hand editing pane. The Builder tree and the DSL text are two views of the same
 * rule: both write to the shared store, so JSON and Evaluate stay live under either.
 */
export function EditorPane(props: { client: RulesApiClient }) {
  const [surface, setSurface] = useState<Surface>('builder');
  const store = useRuleEditorStore();
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;

  return (
    <section className="pane" aria-label="Editor">
      <div className="pane-header">
        <div className="surface-tabs" role="tablist" aria-label="Editing surface">
          {(['builder', 'dsl'] as const).map((value) => (
            <button
              key={value}
              type="button"
              role="tab"
              aria-selected={surface === value}
              className={surface === value ? 'tab active' : 'tab'}
              onClick={() => setSurface(value)}
            >
              {value === 'builder' ? 'Builder' : 'DSL'}
            </button>
          ))}
        </div>
        {surface === 'dsl' && <span className="pane-hint">text is the source of truth</span>}
      </div>

      {surface === 'builder'
        ? <BuilderBody client={props.client} />
        : <DslEditor store={store} catalog={catalog} />}
    </section>
  );
}
```

Then in `ui/apps/demo/src/App.tsx`, swap the import and the rendered pane:

```typescript
import { EditorPane } from './panes/EditorPane.js';
```

and replace `<BuilderPane client={client} />` with:

```typescript
        <EditorPane client={client} />
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `pnpm -C ui/apps/demo test`
Expected: PASS — including the existing `App.test.tsx` and builder tests.

If `App.test.tsx` asserted on the `Builder` pane's `aria-label`, update that assertion to `Editor` — the pane's label changed deliberately.

- [ ] **Step 6: Commit**

```bash
git add ui/apps/demo/src/panes/EditorPane.tsx ui/apps/demo/src/panes/BuilderPane.tsx ui/apps/demo/src/App.tsx ui/apps/demo/test/panes/EditorPane.test.tsx ui/apps/demo/test/App.test.tsx
git commit -m "feat(demo): toggle between Builder and DSL surfaces"
```

---

## Task 19: Styles

**Files:**
- Modify: `ui/apps/demo/src/styles/tokens.css`
- Modify: `ui/apps/demo/src/styles/app.css`

- [ ] **Step 1: Add the DSL colour tokens**

Append to the `:root` block in `ui/apps/demo/src/styles/tokens.css`:

```css
:root {
  --dsl-bg: #ffffff; --dsl-fg: #24292f; --dsl-gutter-bg: #fbfbfa; --dsl-gutter: #9ca3af;
  --dsl-spec: #4f46e5; --dsl-op: #a15c2f; --dsl-kw: #7c3aed; --dsl-quant: #0369a1;
  --dsl-str: #0f7b3f; --dsl-num: #b45309; --dsl-ref: #0e7490; --dsl-punct: #57606a;
  --dsl-ok: #16a34a; --dsl-warn: #d97706; --dsl-err: #dc2626;
  --dsl-banner-bg: #fffbeb; --dsl-banner-bd: #fcd996; --dsl-banner-fg: #7c4a03;
  --dsl-tooltip-bg: #26262b; --dsl-tooltip-fg: #f4f4f5;
}
```

And to the dark-scheme block:

```css
@media (prefers-color-scheme: dark) {
  :root {
    --dsl-bg: #1e1e22; --dsl-fg: #d4d4d8; --dsl-gutter-bg: #191a1d; --dsl-gutter: #5b5b63;
    --dsl-spec: #a5b4fc; --dsl-op: #dcae82; --dsl-kw: #c4b5fd; --dsl-quant: #7dd3fc;
    --dsl-str: #86efac; --dsl-num: #fcd34d; --dsl-ref: #67e8f9; --dsl-punct: #9ca3af;
    --dsl-ok: #4ade80; --dsl-warn: #fbbf24; --dsl-err: #f87171;
    --dsl-banner-bg: #332a12; --dsl-banner-bd: #5c4a1a; --dsl-banner-fg: #f5d58a;
    --dsl-tooltip-bg: #26262b; --dsl-tooltip-fg: #f4f4f5;
  }
}
```

Keep the existing declarations in both blocks — these are additions, not replacements.

- [ ] **Step 2: Add the component styles**

Append to `ui/apps/demo/src/styles/app.css`:

```css
/* --- Builder/DSL surface toggle --- */
.surface-tabs { display: inline-flex; gap: 2px; padding: 2px; background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius); }
.surface-tabs .tab { height: 24px; padding: 0 11px; font: 600 11.5px var(--sans); color: var(--muted); background: transparent; border: none; border-radius: 6px; cursor: pointer; }
.surface-tabs .tab.active { color: var(--text); background: var(--bg); box-shadow: 0 1px 2px rgb(0 0 0 / 14%); }
.pane-hint { font: 500 11px var(--sans); color: var(--muted); }

/* --- DSL editor --- */
.dsl { display: flex; flex-direction: column; min-height: 0; flex: 1; }
.dsl-toolbar { display: flex; align-items: center; gap: 10px; padding: 6px 8px; border-bottom: 1px solid var(--border); }
.dsl-filename { font: 500 12px var(--mono); color: var(--muted); }
.dsl-toolbar .dsl-pill { margin-left: auto; }
.dsl-pill { display: inline-flex; align-items: center; gap: 6px; height: 22px; padding: 0 9px; border-radius: 11px; font: 500 11.5px var(--sans); }
.dsl-pill::before { content: ''; width: 7px; height: 7px; border-radius: 50%; background: currentcolor; }
.dsl-pill-synced { color: var(--dsl-ok); background: color-mix(in srgb, var(--dsl-ok) 12%, transparent); }
.dsl-pill-dirty { color: var(--dsl-warn); background: color-mix(in srgb, var(--dsl-warn) 14%, transparent); }
.dsl-pill-error { color: var(--dsl-err); background: color-mix(in srgb, var(--dsl-err) 12%, transparent); }
.dsl-surface { flex: 1; min-height: 240px; overflow: auto; }
.dsl-surface .cm-editor { height: 100%; }

/* --- Conflict banner --- */
.dsl-banner { display: flex; align-items: center; gap: 10px; padding: 8px 12px; font: 12px var(--sans); color: var(--dsl-banner-fg); background: var(--dsl-banner-bg); border-bottom: 1px solid var(--dsl-banner-bd); }
.dsl-banner span { flex: 1; }
.dsl-banner button { height: 24px; padding: 0 10px; font: 500 12px var(--sans); border-radius: 6px; cursor: pointer; border: 1px solid var(--dsl-banner-bd); background: transparent; color: inherit; }
.dsl-banner button:first-of-type { color: #fff; background: var(--accent); border-color: transparent; }

/* --- Hover tooltip --- */
.dsl-hover { padding: 8px 10px; max-width: 320px; }
.dsl-hover-code { font: 600 10.5px var(--sans); text-transform: uppercase; letter-spacing: .04em; color: var(--dsl-err); }
.dsl-hover-message { margin-top: 3px; font: 12.5px/1.4 var(--sans); }
.dsl-hover-path { display: inline-block; margin-top: 4px; font: 11px var(--mono); opacity: .75; }

/* --- Payload popover --- */
.dsl-popover { position: absolute; z-index: 20; width: 360px; padding: 12px; background: var(--bg); border: 1px solid var(--border); border-radius: var(--radius); box-shadow: 0 12px 34px rgb(0 0 0 / 18%); }
.dsl-popover-head { display: flex; align-items: center; gap: 8px; }
.dsl-popover-spec { font: 600 14px var(--mono); color: var(--dsl-spec); }
.dsl-popover-close { margin-left: auto; border: none; background: transparent; font-size: 16px; cursor: pointer; color: var(--muted); }
.dsl-popover-desc { margin: 6px 0 0; font: 12.5px/1.45 var(--sans); color: var(--text); }
.dsl-popover-meta { display: flex; gap: 16px; margin: 9px 0 0; }
.dsl-popover-meta dt { font: 600 9.5px var(--sans); text-transform: uppercase; letter-spacing: .05em; color: var(--muted); }
.dsl-popover-meta dd { margin: 2px 0 0; font: 500 12px var(--mono); }
.dsl-field { display: block; margin-top: 10px; }
.dsl-field > span { display: block; margin-bottom: 4px; font: 600 10.5px var(--sans); text-transform: uppercase; letter-spacing: .04em; color: var(--muted); }
.dsl-field input, .dsl-field textarea { width: 100%; box-sizing: border-box; padding: 6px 9px; font: 13px var(--mono); color: var(--text); background: var(--bg); border: 1px solid var(--border); border-radius: 6px; }
.dsl-field textarea { min-height: 56px; resize: vertical; }
.dsl-popover-hint { margin: 8px 0 0; font: 11.5px/1.4 var(--sans); color: var(--muted); }
.dsl-popover-error { margin: 8px 0 0; font: 12px var(--sans); color: var(--dsl-err); }
.dsl-popover-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 12px; }
.dsl-popover-actions button { height: 28px; padding: 0 12px; font: 500 12.5px var(--sans); border: 1px solid var(--border); background: var(--surface); color: var(--text); border-radius: 6px; cursor: pointer; }
.dsl-popover-actions .primary { color: #fff; background: var(--accent); border-color: transparent; font-weight: 600; }
```

- [ ] **Step 3: Verify the demo still renders**

Run: `pnpm -C ui/apps/demo test`
Expected: PASS.

Run: `pnpm -C ui/apps/demo build`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add ui/apps/demo/src/styles/tokens.css ui/apps/demo/src/styles/app.css
git commit -m "style(demo): add DSL editor, banner and popover styles"
```

---

## Task 20: End-to-end coverage

**Files:**
- Create: `ui/apps/demo/e2e/dsl.spec.ts`

- [ ] **Step 1: Read the existing e2e setup**

Run: `ls ui/apps/demo/e2e && cat ui/apps/demo/playwright.config.ts 2>/dev/null || cat ui/apps/demo/e2e/*.config.* 2>/dev/null`

Match the existing spec's conventions (base URL, fixtures, how the API is stubbed or served). Read one existing spec in full before writing the new one.

- [ ] **Step 2: Write the e2e spec**

Create `ui/apps/demo/e2e/dsl.spec.ts`, following the conventions you just read:

```typescript
import { test, expect } from '@playwright/test';

test.describe('DSL editor', () => {
  test('typing DSL updates the JSON document', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('tab', { name: 'DSL' }).click();

    const editor = page.locator('.cm-content');
    await editor.click();
    await page.keyboard.press('ControlOrMeta+a');
    await page.keyboard.type('is-active && is-verified');

    await expect(page.getByLabel('rule document')).toContainText('andAlso', { timeout: 5000 });
    await expect(page.getByLabel('sync status')).toContainText('synced');
  });

  test('an unknown spec surfaces a diagnostic', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('tab', { name: 'DSL' }).click();

    const editor = page.locator('.cm-content');
    await editor.click();
    await page.keyboard.press('ControlOrMeta+a');
    await page.keyboard.type('is-nonsense');

    await expect(page.locator('.cm-lintRange-error').first()).toBeVisible({ timeout: 5000 });
  });

  test('Format reprints the buffer canonically', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('tab', { name: 'DSL' }).click();

    const editor = page.locator('.cm-content');
    await editor.click();
    await page.keyboard.press('ControlOrMeta+a');
    await page.keyboard.type('is-active     &&     is-verified');
    await page.getByRole('button', { name: /format/i }).click();

    await expect(editor).toContainText('is-active && is-verified');
  });
});
```

- [ ] **Step 3: Run the e2e suite**

Run: `pnpm -C ui/apps/demo e2e`
Expected: PASS. If the API is not stubbed in the existing setup, the catalog request may fail and specs may need the same stubbing the existing spec uses — mirror it.

- [ ] **Step 4: Commit**

```bash
git add ui/apps/demo/e2e/dsl.spec.ts
git commit -m "test(demo): add DSL editor end-to-end coverage"
```

---

## Task 21: Full verification and simplification pass

**Files:** all changed files.

- [ ] **Step 1: Run every check**

```bash
pnpm -C ui/packages/rules-core test
pnpm -C ui/packages/rules-core typecheck
pnpm -C ui/packages/rules-react test
pnpm -C ui/apps/demo test
pnpm -C ui/apps/demo typecheck
pnpm -C ui/apps/demo build
```

Expected: every command exits 0. Fix any failure before continuing — do not proceed with a red suite.

- [ ] **Step 2: Verify the demo runs against the backend**

```bash
./run-demo.sh
```

Then open `http://localhost:5100`, switch to the DSL tab, and confirm: text appears highlighted, typing updates the JSON pane, an unknown spec squiggles, and clicking a spec opens the popover. Stop the server when done.

- [ ] **Step 3: Run the mandatory code-simplifier pass**

The project requires this after any change (see `CLAUDE.md` § Post-Implementation Code Review). Dispatch a `code-simplifier` agent over the changed files:

- `ui/packages/rules-core/src/dsl/*.ts`
- `ui/apps/demo/src/dsl/*.ts(x)`
- `ui/apps/demo/src/panes/EditorPane.tsx`

Focus on duplication between the core lexer and the CodeMirror stream parser, the length of `parser.ts`, and any procedural code in `DslEditor.tsx`. Apply the agent's recommendations, then re-run the suites from Step 1.

- [ ] **Step 4: Commit any simplifications**

```bash
git add -A
git commit -m "refactor(dsl): apply code-simplifier review"
```

---

## Self-Review Notes

Spec coverage checked section by section:

| Spec section | Tasks |
| --- | --- |
| `rules-core/src/dsl/` layer | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 |
| Grammar (all node kinds, precedence, `as`, backticks, params) | 3, 4, 5, 7, 8 |
| Canonical formatting | 7 (reference-document test) |
| `demo/src/dsl/` CodeMirror integration | 12, 13, 14 |
| Sync state machine + self-commit guard | 15 |
| Payload popover (string ⇄ object, schema hints) | 16 |
| Autocomplete | 13 |
| Lint + hover | 14 |
| Builder ⇄ DSL toggle | 18 |
| Theming | 12, 19 |
| Testing strategy | every task, plus 20, 21 |

Known deliberate deviations from the spec, all noted in-task:
- The **"Rule returns" header strip** is not implemented as a separate element; the return
  shape is surfaced in the payload popover's `returns` field instead. The spec declared the
  strip display-only, so no behaviour is lost. Add it as a follow-up if the visual is wanted.
- `DslEditor.test.tsx` carries a `conflictForTesting` prop as a fallback if jsdom cannot
  drive CodeMirror input; Task 17 instructs deleting it if the real path works.
