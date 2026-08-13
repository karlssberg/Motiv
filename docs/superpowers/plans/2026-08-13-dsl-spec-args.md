# DSL Spec Args Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Teach `@motiv-rules/core` about spec-node `args`, so a parameterised rule document survives a DSL round-trip instead of silently losing its arguments.

**Architecture:** Step 1 (Tasks 1–6) adds an `args` field to `SpecNode` and a parenthesised `s(n = 1)` call syntax across the lexer, parser and printer, with no catalog dependency — this alone closes the silent-loss hole. Step 2 (Tasks 7–10) projects the already-internal `SpecRegistryEntry.Parameters` onto the HTTP catalog so args may be *typed* positionally, while names remain what gets stored. The printer always emits the named form, which keeps the round-trip guarantee catalog-free.

**Tech Stack:** TypeScript (ESM, `.js` import specifiers), Vitest, pnpm workspaces; C# minimal APIs in `Motiv.Serialization.AspNetCore`, xUnit.

**Design doc:** `docs/superpowers/specs/2026-08-13-dsl-spec-args-design.md`

## Global Constraints

- **No change to `schemas/rule.v1.json`**, to the C# binder, or to any stored document. The C# side already accepts `args`; only the TS side is behind.
- **Arg values are literals only**: `string | number | boolean | null`. `@parameter` references are *not* substituted into args by `RuleParameterSubstituter`, so accepting them would author documents the backend silently ignores.
- **Names are the stored source of truth.** Positional order (Step 2) is a catalog hint consumed at author time and never enters a rule document.
- **The printer always emits the named form.** Positional is input-only.
- TypeScript imports inside the package use `.js` specifiers (`from '../document.js'`), matching every existing file.
- Every task is TDD: write the failing test, run it and *see* it fail for the right reason, write minimal code, re-run, commit.
- Run `pnpm e2e` (never bare `playwright test`) if an e2e run is needed — the sample serves a prebuilt `wwwroot` that goes stale silently.

## Commands

| Purpose | Command |
|---|---|
| rules-core unit tests | `pnpm -C ui/packages/rules-core test` |
| a single rules-core test file | `pnpm -C ui/packages/rules-core test <file>` |
| rules-core typecheck | `pnpm -C ui/packages/rules-core typecheck` |
| demo unit tests | `pnpm -C ui/apps/demo test` |
| .NET tests | `dotnet test src/Motiv.Serialization.AspNetCore.Tests` |

> **Note on `dotnet`:** this machine keeps its runtimes user-local. If `dotnet` is not found or a target framework is missing, prefix the command with `DOTNET_ROOT=~/.dotnet PATH=~/.dotnet:$PATH`. `net472` never runs here.

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `ui/packages/rules-core/src/document.ts` | `ArgValue` type; `args` on `SpecNode` | 1 |
| `ui/packages/rules-core/src/dsl/types.ts` | `'comma'` added to `TokenKind` | 2 |
| `ui/packages/rules-core/src/dsl/lexer.ts` | emit a `comma` token for `,` | 2 |
| `ui/packages/rules-core/src/dsl/parser.ts` | `parseArgs`, `parseArgValue`, shared `parseIdentifier` | 3, 4 |
| `ui/packages/rules-core/src/dsl/printer.ts` | `printArgs`, `printArgValue` | 5 |
| `ui/apps/demo/src/dsl/motivLanguage.ts` | comma → `punctuation` in the CM stream parser | 6 |
| `ui/apps/demo/src/styles/app.css` | `.tok-comma` colour | 6 |
| `src/Motiv.Serialization.AspNetCore/RulesContracts.cs` | `CatalogParameter`; `Parameters` on `CatalogEntry` | 7 |
| `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` | project `SpecRegistryEntry.Parameters` | 7 |
| `ui/packages/rules-core/src/contracts.ts` | `CatalogParameter`; `parameters` on `CatalogEntry` | 8 |
| `ui/packages/rules-core/src/dsl/parser.ts` | positional arg resolution | 9 |
| `ui/packages/rules-core/src/dsl/printer.ts` | declared-order arg printing | 10 |

---

# Step 1 — named args, catalog-free

## Task 1: `args` on `SpecNode`

**Files:**
- Modify: `ui/packages/rules-core/src/document.ts:14`
- Test: `ui/packages/rules-core/test/schema.test.ts:19-27`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `export type ArgValue = string | number | boolean | null`, and `SpecNode.args?: Record<string, ArgValue>`. Every later task depends on both names.

> **Why the "test" here is a typecheck.** TypeScript types are erased at runtime, so a Vitest assertion cannot fail on a missing type — Vitest transpiles without typechecking, and the ajv document below would pass either way. The honest red for a *type* is `tsc --noEmit`. The ajv case is added in the same task because it proves the TS shape actually conforms to the C# schema, which is the thing that matters.

- [ ] **Step 1: Add an args-bearing document to the schema-drift test**

In `ui/packages/rules-core/test/schema.test.ts`, add two entries to the end of the existing `documents` array (around line 26, after the `{ name: 'doc', rule: { xor: … } }` entry):

```ts
  { rule: { spec: 'approver-count-at-least', args: { n: 1 } } },
  {
    rule: {
      spec: 'threshold', args: { limit: 2.5, label: 'high', strict: true, note: null },
    },
  },
```

- [ ] **Step 2: Run the typecheck to verify it fails**

Run: `pnpm -C ui/packages/rules-core typecheck`

Expected: FAIL with `error TS2353: Object literal may only specify known properties, and 'args' does not exist in type 'SpecNode'.`

- [ ] **Step 3: Add the type**

In `ui/packages/rules-core/src/document.ts`, replace line 14:

```ts
export interface SpecNode extends Decoration { spec: string }
```

with:

```ts
/**
 * A value supplied to a parameterised spec. Literals only: `RuleParameterSubstituter`
 * interpolates `whenTrue`/`whenFalse` text and resolves `n`, but never rewrites `args`, so a
 * `@parameter` reference here would be authored and then silently ignored by the binder.
 */
export type ArgValue = string | number | boolean | null;

export interface SpecNode extends Decoration { spec: string; args?: Record<string, ArgValue> }
```

- [ ] **Step 4: Run the typecheck and the tests to verify they pass**

Run: `pnpm -C ui/packages/rules-core typecheck`
Expected: PASS (no output)

Run: `pnpm -C ui/packages/rules-core test schema`
Expected: PASS — including the two new `rule.v1.json drift` cases. If ajv rejects them, the TS shape disagrees with the real schema and the *type* is wrong, not the test.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/document.ts ui/packages/rules-core/test/schema.test.ts
git commit -m "feat(rules-core): add args to SpecNode

Mirrors schemas/rule.v1.json, where args appears solely in specNode.
Covered by the ajv drift test, which compiles the real schema."
```

---

## Task 2: `comma` token

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/types.ts:12-18`, `ui/packages/rules-core/src/dsl/lexer.ts:79-80`
- Test: `ui/packages/rules-core/test/dsl-lexer.test.ts`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `TokenKind` gains the member `'comma'`. Task 3 relies on `,` lexing as `{ kind: 'comma' }`; Task 6 relies on the widened union.

- [ ] **Step 1: Write the failing test**

Append to `ui/packages/rules-core/test/dsl-lexer.test.ts`, inside the existing top-level `describe`:

```ts
  it('lexes a comma as its own token', () => {
    expect(tokenize(',')).toEqual([{ kind: 'comma', value: ',', from: 0, to: 1 }]);
  });

  it('lexes a comma between words without swallowing them', () => {
    expect(tokenize('a, b').map((token) => token.kind)).toEqual(['spec', 'comma', 'spec']);
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm -C ui/packages/rules-core test dsl-lexer`

Expected: FAIL — the comma currently falls through to the final `push('error', …)` branch, so the received kind is `'error'`, not `'comma'`.

- [ ] **Step 3: Add the token kind and the lexer branch**

In `ui/packages/rules-core/src/dsl/types.ts`, add a member to `TokenKind` immediately after `'equals'` (line 13):

```ts
  | 'comma'       // ,
```

In `ui/packages/rules-core/src/dsl/lexer.ts`, add a branch immediately after the `equals` branch (line 80):

```ts
    if (char === ',') { push('comma', i, i + 1); i++; continue; }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm -C ui/packages/rules-core test dsl-lexer`
Expected: PASS

Run: `pnpm -C ui/packages/rules-core test`
Expected: PASS — the whole suite, since `TokenKind` is a public union and widening it can break exhaustive switches.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/types.ts ui/packages/rules-core/src/dsl/lexer.ts ui/packages/rules-core/test/dsl-lexer.test.ts
git commit -m "feat(rules-core): lex a comma as its own token

Separates argument-list entries. Previously fell through to the error
branch, since the language had no comma anywhere."
```

---

## Task 3: parse named args

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/parser.ts:162-165` (the `parsePrimary` spec branch)
- Test: `ui/packages/rules-core/test/dsl-parser.test.ts`, `ui/packages/rules-core/test/dsl-parser-errors.test.ts`

**Interfaces:**
- Consumes: `ArgValue` (Task 1); the `'comma'` token kind (Task 2).
- Produces: `parseArgs(state: ParserState): Record<string, ArgValue> | undefined` and `parseArgValue(state: ParserState): ArgValue | undefined`, both module-private. New `DslError` codes, all strings: `ExpectedArgName`, `ExpectedArgValue`, `UnclosedArgs`, `DuplicateArg`.

- [ ] **Step 1: Write the failing tests**

Append to `ui/packages/rules-core/test/dsl-parser.test.ts`, inside the existing top-level `describe`:

```ts
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

  it('parses a spec with args and a name', () => {
    const result = parse('s(n = 1) as "gate"');
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual({ spec: 's', args: { n: 1 }, name: 'gate' });
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
```

Append to `ui/packages/rules-core/test/dsl-parser-errors.test.ts`, inside its top-level `describe`:

```ts
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm -C ui/packages/rules-core test dsl-parser`

Expected: FAIL. The positive cases fail with `UnexpectedToken` on `(` — `parsePrimary` returns `{ spec }` and leaves `(` unconsumed, so `parse` reports it as trailing input. The error cases fail because the codes do not exist yet.

- [ ] **Step 3: Implement `parseArgValue` and `parseArgs`**

In `ui/packages/rules-core/src/dsl/parser.ts`, add the import of `ArgValue` to the existing type import on line 1:

```ts
import type { ArgValue, ParameterDeclaration, RuleDocument, RuleNode } from '../document.js';
```

Add both functions immediately above `parsePrimary` (before line 154):

```ts
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
```

Replace the `parsePrimary` spec branch (lines 162-165):

```ts
  if (token.kind === 'spec') {
    state.next();
    return { spec: token.value };
  }
```

with:

```ts
  if (token.kind === 'spec') {
    state.next();
    const args = parseArgs(state);
    return args ? { spec: token.value, args } : { spec: token.value };
  }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm -C ui/packages/rules-core test dsl-parser`
Expected: PASS

Run: `pnpm -C ui/packages/rules-core test`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/parser.ts ui/packages/rules-core/test/dsl-parser.test.ts ui/packages/rules-core/test/dsl-parser-errors.test.ts
git commit -m "feat(rules-core): parse named spec arguments

A '(' after a spec token was previously a hard UnexpectedToken, so this
is additive: no existing document could mean anything by it. Duplicate
names are an error rather than last-wins, since last-wins is silent loss."
```

---

## Task 4: contextual identifier positions

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/parser.ts` — the arg-name check from Task 3, `parseQuantifier`'s path check (line 125), `parseParameters`' name check (line 304)
- Test: `ui/packages/rules-core/test/dsl-parser.test.ts`

**Interfaces:**
- Consumes: `parseArgs` (Task 3).
- Produces: `parseIdentifier(state: ParserState, code: string, message: string): Token | undefined`, module-private, used by all three positions.

> **The rule.** The lexer classifies words context-free, so `all` is a `quantifier` token and `string` a `type` token wherever they appear. That is right in exactly one position and overreaching in three. An identifier position is *unambiguous* when the grammar admits nothing else there — arg names, `param` names, and collection paths after `in`. The **spec-name position is excluded**: at expression position a bare `all` genuinely could open `all in orders { … }`, so the lexer's classification is load-bearing and stays. The **arg-value position is excluded too**, for an unrelated reason: `all` there is not an over-classified name, it is simply not a literal.

- [ ] **Step 1: Write the failing tests**

Append to `ui/packages/rules-core/test/dsl-parser.test.ts`, inside the existing top-level `describe`:

```ts
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm -C ui/packages/rules-core test dsl-parser`

Expected: FAIL on the first three. `s(all = 1)` reports `ExpectedArgName` (`all` lexes as `quantifier`); `param all:` reports `ExpectedParameterName`; `any in string` reports `ExpectedCollection` (`string` lexes as `type`). The fourth test should already PASS — it guards the carve-out, so if it ever fails, the change went too far.

- [ ] **Step 3: Add the shared helper and use it in all three positions**

In `ui/packages/rules-core/src/dsl/parser.ts`, extend the type import on line 3 to include `TokenKind`:

```ts
import type { DslError, NodeSpan, ParseResult, Token, TokenKind } from './types.js';
```

Add the helper immediately after the `ParserState` class (after line 54):

```ts
/**
 * The token kinds a word can lex as. The lexer classifies words context-free, so `all` is a
 * `quantifier` and `string` a `type` wherever they appear.
 */
const WORD_KINDS: ReadonlySet<TokenKind> = new Set<TokenKind>(['spec', 'keyword', 'type', 'quantifier']);

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
```

In `parseArgs` (Task 3), replace the five-line name check:

```ts
    const name = state.peek();
    if (!name || name.kind !== 'spec') {
      state.error('ExpectedArgName', 'expected an argument name', name);
      return undefined;
    }
    state.next();
```

with:

```ts
    const name = parseIdentifier(state, 'ExpectedArgName', 'expected an argument name');
    if (!name) return undefined;
```

In `parseQuantifier`, replace the collection-path check (lines 124-129):

```ts
  const pathToken = state.peek();
  if (!pathToken || pathToken.kind !== 'spec') {
    state.error('ExpectedCollection', 'expected a collection path after `in`', pathToken);
    return undefined;
  }
  state.next();
```

with:

```ts
  const pathToken = parseIdentifier(
    state, 'ExpectedCollection', 'expected a collection path after `in`',
  );
  if (!pathToken) return undefined;
```

In `parseParameters`, replace the name check (lines 303-308):

```ts
    const nameToken = state.peek();
    if (!nameToken || nameToken.kind !== 'spec') {
      state.error('ExpectedParameterName', 'expected a parameter name', nameToken);
      break;
    }
    state.next();
```

with:

```ts
    const nameToken = parseIdentifier(state, 'ExpectedParameterName', 'expected a parameter name');
    if (!nameToken) break;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm -C ui/packages/rules-core test dsl-parser`
Expected: PASS, all four.

Run: `pnpm -C ui/packages/rules-core test`
Expected: PASS. In particular `dsl-parser-errors.test.ts:32` (`parse('all in { is-positive }')` expecting `ExpectedCollection`) must still pass — the token after `in` there is a `brace`, not a word, and `WORD_KINDS` admits only word kinds. That test is the check on this change's blast radius: it loosens which *words* are accepted, not whether a non-word is.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/parser.ts ui/packages/rules-core/test/dsl-parser.test.ts
git commit -m "feat(rules-core): accept any word in unambiguous identifier positions

Arg names, param declaration names and collection paths each admit
nothing but an identifier, so requiring kind === 'spec' was the lexer
deciding what the parser already knows -- at the cost of legal names the
DSL could not express. The spec-name position keeps its classification,
since a bare 'all' there could open a quantifier."
```

---

## Task 5: print args, and close the round-trip

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/printer.ts:133-139` (`printBody`)
- Test: `ui/packages/rules-core/test/dsl-printer.test.ts`, `ui/packages/rules-core/test/dsl-roundtrip.test.ts`

**Interfaces:**
- Consumes: `SpecNode.args` (Task 1); `parseArgs` (Task 3).
- Produces: no exported surface change — `print`, `printInline` keep their signatures.

> **This is the task that fixes the reported bug.** `dsl-roundtrip.test.ts` is table-driven: a `DOCUMENTS` array feeds four property tests (`parse(print(doc))` preserves the document, `print` is idempotent, `parse(printInline(rule))` preserves the rule, and every node has a span). Adding one entry exercises all four.

- [ ] **Step 1: Write the failing tests**

In `ui/packages/rules-core/test/dsl-roundtrip.test.ts`, add two entries to the `DOCUMENTS` array (after the `named spec` entry, around line 10):

```ts
  { label: 'spec with args', document: { rule: { spec: 'gate', args: { n: 1 } } } },
  {
    label: 'spec with every arg literal kind',
    document: {
      rule: {
        spec: 'gate',
        args: { count: -2, ratio: 2.5, label: 'high', strict: true, note: null },
      },
    },
  },
```

Append to `ui/packages/rules-core/test/dsl-printer.test.ts`, inside the existing `describe('print', …)`:

```ts
  it('prints a single named argument', () => {
    expect(print({ rule: { spec: 'gate', args: { n: 1 } } })).toBe('gate(n = 1)');
  });

  it('prints every argument literal kind', () => {
    expect(print({
      rule: { spec: 'gate', args: { count: -2, ratio: 2.5, label: 'high', strict: true, note: null } },
    })).toBe('gate(count = -2, ratio = 2.5, label = "high", strict = true, note = null)');
  });

  it('prints nothing for an empty argument map', () => {
    expect(print({ rule: { spec: 'gate', args: {} } })).toBe('gate');
  });

  it('prints args before an `as` clause', () => {
    expect(print({ rule: { spec: 'gate', args: { n: 1 }, name: 'check' } })).toBe('gate(n = 1) as "check"');
  });

  // The last resort. A key that is not word-shaped cannot be represented, and the DSL has no
  // escapes to fall back on. Printing it bare yields text that fails to parse — a visible lint
  // error — rather than text that parses to a *different* document. Throwing was rejected:
  // printInline renders every builder row, so a throw would take the row down with it.
  it('prints a non-word-shaped arg name bare, yielding text that fails to parse', () => {
    const text = print({ rule: { spec: 'gate', args: { 'not a name': 1 } } });
    expect(text).toBe('gate(not a name = 1)');
    expect(parse(text).errors.length).toBeGreaterThan(0);
  });
```

> The last test needs `parse` in this file. Add `import { parse } from '../src/dsl/parser.js';` to the imports if it is not already there.

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm -C ui/packages/rules-core test dsl-printer dsl-roundtrip`

Expected: FAIL. The printer tests receive `'gate'` where `'gate(n = 1)'` was expected — this *is* the reported bug, reproduced. The round-trip cases fail on `parse(print(doc))` returning `{ spec: 'gate' }` against an expected `{ spec: 'gate', args: { n: 1 } }`. The `empty argument map` case should already PASS.

- [ ] **Step 3: Implement the printer**

In `ui/packages/rules-core/src/dsl/printer.ts`, add `type ArgValue` and `type SpecNode` to the existing type import from `'../document.js'` (lines 4-5):

```ts
  type ArgValue, type BinaryNode, type BinaryOperator, type HigherOrderKey, type HigherOrderNode,
  type NotNode, type ParameterDeclaration, type RuleDocument, type RuleNode, type SpecNode,
```

Add both functions immediately above `printBody` (before line 133):

```ts
/** Renders one argument value. `String` covers number, boolean and null exactly. */
function printArgValue(value: ArgValue): string {
  return typeof value === 'string' ? quote(value) : String(value);
}

/**
 * Renders an argument list, or `''` when there are none — so an empty `args` map prints as a bare
 * spec and round-trips to a node without `args`. The two are semantically identical.
 *
 * Names print bare, never quoted: quoting a name would make it read as a value, and the parser's
 * contextual identifier rule already accepts any word-shaped name here.
 */
function printArgs(args: SpecNode['args']): string {
  const entries = Object.entries(args ?? {});
  if (entries.length === 0) return '';
  const rendered = entries.map(([name, value]) => `${name} = ${printArgValue(value)}`);
  return `(${rendered.join(', ')})`;
}
```

Replace the spec line inside `printBody` (line 134):

```ts
  if (isSpecNode(node)) return node.spec;
```

with:

```ts
  if (isSpecNode(node)) return `${node.spec}${printArgs(node.args)}`;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm -C ui/packages/rules-core test dsl-printer dsl-roundtrip`
Expected: PASS — including all four round-trip properties for both new documents.

Run: `pnpm -C ui/packages/rules-core test && pnpm -C ui/packages/rules-core typecheck`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/printer.ts ui/packages/rules-core/test/dsl-printer.test.ts ui/packages/rules-core/test/dsl-roundtrip.test.ts
git commit -m "fix(rules-core): print spec arguments instead of dropping them

Closes the silent loss disclosed on #123: a parameterised document round
-tripped through the DSL published as a different rule. Covered by the
table-driven round-trip properties, which now include args documents."
```

---

## Task 6: demo highlighting

**Files:**
- Modify: `ui/apps/demo/src/dsl/motivLanguage.ts:66`, `ui/apps/demo/src/styles/app.css:614-615`
- Test: `ui/apps/demo/test/dsl/motivLanguage.test.ts`

**Interfaces:**
- Consumes: the `'comma'` token kind (Task 2).
- Produces: nothing consumed by later tasks.

> **Why this is its own task.** `motivLanguage.ts` hand-copies the lexer's character classes for CodeMirror's stream parser, and `lexer.ts` carries a comment saying that this duplication is exactly how the two drifted apart before. `app.css` styles by `tok-{kind}`, so a new kind with no rule renders in body colour. Both are silent-looking regressions, not crashes.

- [ ] **Step 1: Write the failing test**

Append to `ui/apps/demo/test/dsl/motivLanguage.test.ts`, inside `describe('motivStreamParser', …)`:

```ts
  it('tags a comma as punctuation, like the other separators', () => {
    expect(tagOf(',')).toBe('punctuation');
    expect(tagOf(':')).toBe('punctuation');
    expect(tagOf('=')).toBe('punctuation');
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm -C ui/apps/demo test motivLanguage`

Expected: FAIL on the comma. The `:` and `=` assertions pass already; they are there so the test states the rule the comma is joining rather than asserting a bare fact.

- [ ] **Step 3: Add the comma to the stream parser and the stylesheet**

In `ui/apps/demo/src/dsl/motivLanguage.ts`, replace line 66:

```ts
    if (char === ':' || char === '=') return 'punctuation';
```

with:

```ts
    if (char === ':' || char === '=' || char === ',') return 'punctuation';
```

In `ui/apps/demo/src/styles/app.css`, replace lines 614-615:

```css
.tok-colon,
.tok-equals { color: var(--dsl-punctuation); }
```

with:

```css
.tok-colon,
.tok-comma,
.tok-equals { color: var(--dsl-punctuation); }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm -C ui/apps/demo test motivLanguage`
Expected: PASS

Run: `pnpm -C ui/apps/demo test && pnpm -C ui/apps/demo typecheck`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/dsl/motivLanguage.ts ui/apps/demo/src/styles/app.css ui/apps/demo/test/dsl/motivLanguage.test.ts
git commit -m "fix(demo): highlight the comma in both DSL surfaces

motivLanguage.ts hand-copies the lexer for CodeMirror's stream parser and
app.css styles by tok-{kind}, so a new token kind needs both or commas
render unstyled. This duplication is the drift lexer.ts warns about."
```

---

## Checkpoint: Step 1 is complete and shippable

At this point the reported bug is fixed and the branch is releasable on its own. Run the full gate before continuing:

```bash
pnpm -C ui/packages/rules-core test && pnpm -C ui/packages/rules-core typecheck && pnpm -C ui/apps/demo test && pnpm -C ui/apps/demo typecheck
```

Step 2 is ergonomics. If it is deferred, nothing is left half-built.

---

# Step 2 — positional hints from the catalog

## Task 7: project spec parameters onto the catalog

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/RulesContracts.cs:12-13`, `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs:251-258`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/CatalogEndpointTests.cs`

**Interfaces:**
- Consumes: nothing from Step 1.
- Produces: the wire shape `{"name": "n", "type": "integer", "default": 2}` under a `parameters` array on each catalog spec entry. Task 8 mirrors it in TypeScript.

> **Two facts that shape this task.** First, `SpecRegistryEntry.Parameters` is `internal`, but `Motiv.Serialization.csproj:22` already grants `InternalsVisibleTo` to `Motiv.Serialization.AspNetCore` — so the endpoint reads it as-is and the property stays internal. The HTTP contract is where this becomes public, not the registry type.
>
> Second, the catalog has **two** projections. `CompiledSpecs` maps `SpecRegistryEntry`, which has `Parameters`. `EffectiveSpecs` maps `PropositionEntry`, which does **not** — an authored proposition is a document, not a parameterised factory registration. So `EffectiveSpecs` looks the entry up in the registry by name and emits parameters only when the effective definition is still the compiled one (`Origin == PropositionOrigin.Compiled`); an authored or overridden definition emits `null`, because its behaviour comes from a document rather than from an arg contract.

- [ ] **Step 1: Write the failing test**

Append to `src/Motiv.Serialization.AspNetCore.Tests/CatalogEndpointTests.cs`, inside the existing `CatalogEndpointTests` class. This follows the file's established style: Shouldly assertions over the raw `JsonElement` body, and `TestApp.StartAsync(registry, options)`.

```csharp
    [Fact]
    public async Task Should_list_declared_parameters_in_order_for_a_parameterised_spec()
    {
        // Arrange
        var registry = new SpecRegistry()
            .RegisterParameterised(
                "at-least",
                [
                    new RuleParameterDeclaration("floor", RuleParameterType.Integer, hasDefault: true, 2),
                    new RuleParameterDeclaration("label", RuleParameterType.String, hasDefault: false, null),
                ],
                values => Spec.Build((int n) => n >= (int)values["floor"]!).Create("at-least"));
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await TestApp.StartAsync(registry, options);

        // Act
        var response = await app.GetTestClient().GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var parameters = body.GetProperty("specs")[0].GetProperty("parameters");

        parameters.GetArrayLength().ShouldBe(2);
        parameters[0].GetProperty("name").GetString().ShouldBe("floor");
        parameters[0].GetProperty("type").GetString().ShouldBe("integer");
        parameters[0].GetProperty("default").GetInt32().ShouldBe(2);
        parameters[1].GetProperty("name").GetString().ShouldBe("label");
        parameters[1].GetProperty("type").GetString().ShouldBe("string");
        parameters[1].GetProperty("default").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_report_no_parameters_for_a_plain_spec()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-positive", IsPositive);
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await TestApp.StartAsync(registry, options);

        // Act
        var response = await app.GetTestClient().GetAsync("/api/rules/catalog");

        // Assert
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("specs")[0].GetProperty("parameters").ValueKind.ShouldBe(JsonValueKind.Null);
    }
```

> **On the `type` assertion.** `"integer"` lowercase is the point of the test, not incidental: it matches the `parameterDeclaration.type` enum in `rule.v1.json`, so the schema, the DSL and the catalog all use one vocabulary. If the serializer emits `"Integer"`, the projection helper's `ToLowerInvariant()` is missing or being bypassed.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.AspNetCore.Tests --filter CatalogEndpointTests`

Expected: FAIL to compile — `CatalogEntry` has no `Parameters` member.

- [ ] **Step 3: Add the contract and both projections**

In `src/Motiv.Serialization.AspNetCore/RulesContracts.cs`, add above `CatalogEntry` (line 12):

```csharp
/// <summary>One declared parameter of a parameterised spec, in declaration order.</summary>
/// <param name="Name">The name a <c>spec</c> node's <c>args</c> supplies the value under.</param>
/// <param name="Type">The scalar type, lowercased to match the names in <c>rule.v1.json</c>.</param>
/// <param name="Default">The default value, or <c>null</c> when the parameter is required.</param>
public sealed record CatalogParameter(string Name, string Type, object? Default);
```

Add a `Parameters` member to `CatalogEntry`:

```csharp
public sealed record CatalogEntry(
    string Name, string ModelType, string MetadataType, bool IsAsync, string? Description,
    PropositionOrigin Origin, IReadOnlyList<CatalogParameter>? Parameters = null);
```

> `Parameters` is optional-with-default so the two construction sites can adopt it independently and no other caller breaks.

In `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`, add a projection helper beside `CompiledSpecs`:

```csharp
    /// <summary>
    /// A parameterised registration's declarations, in order, or <c>null</c> for a plain one.
    /// The type name is lowercased to match the `parameterDeclaration.type` enum in rule.v1.json,
    /// so one vocabulary spans the schema, the DSL and the catalog.
    /// </summary>
    private static IReadOnlyList<CatalogParameter>? Parameters(SpecRegistryEntry entry) =>
        entry.Parameters is null
            ? null
            : [.. entry.Parameters.Select(parameter => new CatalogParameter(
                parameter.Name,
                parameter.Type.ToString().ToLowerInvariant(),
                parameter.HasDefault ? parameter.DefaultValue : null))];
```

Extend `CompiledSpecs` to pass `Parameters(entry)` as the final argument.

Extend `EffectiveSpecs` to take the registry and resolve parameters only for a still-compiled definition:

```csharp
    private static IReadOnlyList<CatalogEntry> EffectiveSpecs(
        PropositionSet propositions, SpecRegistry registry) =>
        [.. propositions.Propositions
            .Where(Resolves)
            .Select(entry => new CatalogEntry(
                entry.Name, entry.ModelType, entry.MetadataType, entry.IsAsync,
                entry.Description, entry.Origin,
                entry.Origin == PropositionOrigin.Compiled
                    ? registry.Entries.FirstOrDefault(spec => spec.Name == entry.Name) is { } spec
                        ? Parameters(spec)
                        : null
                    : null))];
```

Update the `MapGet("/catalog", …)` call site to pass `registry` to `EffectiveSpecs`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.AspNetCore.Tests --filter CatalogEndpointTests`
Expected: PASS

Run: `dotnet test src/Motiv.Serialization.AspNetCore.Tests`
Expected: PASS — the whole project, since `CatalogEntry`'s shape is pinned by other tests.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore/RulesContracts.cs src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs src/Motiv.Serialization.AspNetCore.Tests/
git commit -m "feat(aspnetcore): expose declared spec parameters on the catalog

SpecRegistryEntry.Parameters stays internal -- InternalsVisibleTo already
covers this assembly, so the HTTP contract is where it becomes public.
Only a still-compiled definition carries parameters: an authored or
overridden proposition is a document, not a parameterised registration."
```

---

## Task 8: mirror `parameters` in the TS catalog type

**Files:**
- Modify: `ui/packages/rules-core/src/contracts.ts:4-12`
- Test: `ui/packages/rules-core/test/schema.test.ts` (the `Catalog schema maps typing` describe)

**Interfaces:**
- Consumes: the wire shape from Task 7; `ArgValue` (Task 1); `ParameterDeclaration` (existing, in `document.ts`).
- Produces: `CatalogParameter` and `CatalogEntry.parameters?: CatalogParameter[]`. Tasks 9 and 10 both read them.

- [ ] **Step 1: Write the failing test**

Append to `ui/packages/rules-core/test/schema.test.ts`, inside `describe('Catalog schema maps typing', …)`:

```ts
  it('a catalog entry carries ordered parameter declarations', () => {
    const catalog: Catalog = {
      specs: [{
        name: 'at-least',
        modelType: 'customer',
        metadataType: 'String',
        isAsync: false,
        origin: 'Compiled',
        parameters: [
          { name: 'floor', type: 'integer', default: 2 },
          { name: 'label', type: 'string' },
        ],
      }],
      collections: [],
    };

    expect(catalog.specs[0]!.parameters?.map((parameter) => parameter.name)).toEqual(['floor', 'label']);
  });
```

> `'Compiled'` is a member of the existing `PropositionOrigin` union (`contracts.ts:133`), so it typechecks as written.

- [ ] **Step 2: Run the typecheck to verify it fails**

Run: `pnpm -C ui/packages/rules-core typecheck`

Expected: FAIL with `error TS2353: Object literal may only specify known properties, and 'parameters' does not exist in type 'CatalogEntry'.`

- [ ] **Step 3: Add the types**

In `ui/packages/rules-core/src/contracts.ts`, widen the existing import on line 1 — the file already imports from `'./document.js'`, so add to it rather than adding a second line:

```ts
import type { ArgValue, ParameterDeclaration, RuleDocument } from './document.js';
```

Then add above `CatalogEntry` (line 4):

```ts
/** One declared parameter of a parameterised spec, in declaration order. */
export interface CatalogParameter {
  name: string;
  /** The scalar type, matching the `parameterDeclaration.type` enum in rule.v1.json. */
  type: ParameterDeclaration['type'];
  /** The default value, absent when the parameter is required. */
  default?: ArgValue;
}
```

Add the field to `CatalogEntry`:

```ts
  /**
   * The spec's declared parameters, in order — present only for a parameterised compiled
   * registration. Order is a hint for authoring positionally; the stored document is always named.
   */
  parameters?: CatalogParameter[];
```

- [ ] **Step 4: Run the typecheck and tests to verify they pass**

Run: `pnpm -C ui/packages/rules-core typecheck`
Expected: PASS

Run: `pnpm -C ui/packages/rules-core test`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/contracts.ts ui/packages/rules-core/test/schema.test.ts
git commit -m "feat(rules-core): mirror catalog parameter declarations

Ordered, and present only for parameterised compiled registrations.
Order is an authoring hint; stored documents stay named."
```

---

## Task 9: parse positional args against the catalog

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/parser.ts` (`parseArgs`, `parse`), `ui/packages/rules-core/src/dsl/index.ts` if it re-exports `parse`'s signature
- Test: `ui/packages/rules-core/test/dsl-parser.test.ts`, `ui/packages/rules-core/test/dsl-parser-errors.test.ts`

**Interfaces:**
- Consumes: `CatalogParameter`, `CatalogEntry.parameters` (Task 8); `parseArgs` (Tasks 3–4).
- Produces: `parse(text: string, options?: ParseOptions): ParseResult` where `export interface ParseOptions { catalog?: Catalog }`. Task 10 mirrors the optional-options shape on the printer.

> **The rule.** Positional args resolve to names *at author time*, so what lands in the document is always `{"n": 1}`. Without a catalog, or for a spec the catalog does not name, positional is an **error** — never a guess. Positional args must precede named ones, mirroring C#. A spec whose catalog entry has no `parameters` rejects args outright, pre-empting the server's `UnexpectedArguments`.

- [ ] **Step 1: Write the failing tests**

Add a shared fixture near the top of `ui/packages/rules-core/test/dsl-parser.test.ts`, below the imports:

```ts
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
```

Add `import type { Catalog } from '../src/contracts.js';` to the file's imports, then append these tests inside the top-level `describe`:

```ts
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
```

Append to `ui/packages/rules-core/test/dsl-parser-errors.test.ts` (adding the same `CATALOG` fixture and `Catalog` import there):

```ts
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm -C ui/packages/rules-core test dsl-parser`

Expected: FAIL — `parse` takes one argument, so the `options` argument is a typecheck error and every positional case reports `ExpectedArgName` at runtime. The `no catalog` case should already PASS; it pins the behaviour that must survive.

- [ ] **Step 3: Implement positional resolution**

In `ui/packages/rules-core/src/dsl/parser.ts`, add the import and the options type:

```ts
import type { Catalog, CatalogParameter } from '../contracts.js';

/** Options for {@link parse}. */
export interface ParseOptions {
  /**
   * The spec catalog, used only to resolve *positional* arguments to their declared names.
   * Absent, positional arguments are an error rather than a guess — so parsing stays a pure
   * function of the text for every document the printer can produce.
   */
  catalog?: Catalog;
}
```

Thread the catalog through `ParserState` by adding a constructor parameter:

```ts
  constructor(readonly text: string, readonly catalog?: Catalog) {
    this.tokens = tokenize(text);
  }
```

Add a lookup helper beside `parseIdentifier`:

```ts
/** The declared parameters of `spec`, or `undefined` when the catalog cannot say. */
function declaredParameters(
  state: ParserState, spec: string,
): readonly CatalogParameter[] | undefined {
  return state.catalog?.specs.find((entry) => entry.name === spec)?.parameters;
}
```

Rework `parseArgs` to take the spec name and accept both forms. Replace the whole function body's loop head so that each entry first tries a value (positional) and falls back to `NAME '=' value`:

```ts
/** args := '(' arg (',' arg)* ')' where arg := literal | NAME '=' literal */
function parseArgs(state: ParserState, spec: string): Record<string, ArgValue> | undefined {
  if (state.peek()?.value !== '(') return undefined;
  const open = state.next()!;
  const args: Record<string, ArgValue> = {};
  let positional = 0;
  let sawNamed = false;

  for (;;) {
    // A named argument is NAME '='; anything else in this position is positional.
    const isNamed = state.peek(1)?.kind === 'equals';
    let name: string;
    let value: ArgValue | undefined;

    if (isNamed) {
      const nameToken = parseIdentifier(state, 'ExpectedArgName', 'expected an argument name');
      if (!nameToken) return undefined;
      state.next(); // the '='
      sawNamed = true;
      name = nameToken.value;
      value = parseArgValue(state);
    } else {
      if (sawNamed) {
        state.error('PositionalAfterNamed',
          'a positional argument cannot follow a named one', state.peek());
        return undefined;
      }
      const declared = declaredParameters(state, spec);
      if (!declared) {
        state.error(
          state.catalog === undefined ? 'ExpectedArgName' : 'UnknownParameterisedSpec',
          state.catalog === undefined
            ? 'expected an argument name'
            : `\`${spec}\` declares no parameters to supply positionally`,
          state.peek());
        return undefined;
      }
      if (positional >= declared.length) {
        state.error('TooManyArguments',
          `\`${spec}\` declares ${declared.length} parameter(s)`, state.peek());
        return undefined;
      }
      name = declared[positional]!.name;
      positional++;
      value = parseArgValue(state);
    }

    if (value === undefined) return undefined;

    if (Object.prototype.hasOwnProperty.call(args, name)) {
      state.error('DuplicateArg', `duplicate argument \`${name}\``, state.peek());
      return undefined;
    }
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
```

In `parsePrimary`'s spec branch, pass the spec name and reject args for a spec the catalog says takes none:

```ts
  if (token.kind === 'spec') {
    state.next();
    const hasArgs = state.peek()?.value === '(';
    if (hasArgs && state.catalog !== undefined && declaredParameters(state, token.value) === undefined
        && state.catalog.specs.some((entry) => entry.name === token.value)) {
      state.error('UnexpectedArguments',
        `\`${token.value}\` takes no arguments`, state.peek());
      state.next();
      return undefined;
    }
    const args = parseArgs(state, token.value);
    return args ? { spec: token.value, args } : { spec: token.value };
  }
```

Finally widen `parse`:

```ts
export function parse(text: string, options?: ParseOptions): ParseResult {
  const state = new ParserState(text, options?.catalog);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm -C ui/packages/rules-core test dsl-parser`
Expected: PASS

Run: `pnpm -C ui/packages/rules-core test && pnpm -C ui/packages/rules-core typecheck`
Expected: PASS. The round-trip suite must stay green *without* a catalog — that is the guarantee this task must not weaken.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/parser.ts ui/packages/rules-core/test/dsl-parser.test.ts ui/packages/rules-core/test/dsl-parser-errors.test.ts
git commit -m "feat(rules-core): accept positional args, resolved to names

Positional resolves at author time, so what is stored is always named --
reordering a spec's parameters can never re-bind a stored document.
Without a catalog, positional is an error rather than a guess."
```

---

## Task 10: print args in declared order

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/printer.ts` (`print`, `printInline`, `printArgs`)
- Test: `ui/packages/rules-core/test/dsl-printer.test.ts`

**Interfaces:**
- Consumes: `CatalogParameter`, `CatalogEntry.parameters` (Task 8).
- Produces: `print(document: RuleDocument, options?: PrintOptions): string` and `printInline(node: RuleNode, options?: PrintOptions): string` where `export interface PrintOptions { catalog?: Catalog }`.

> **What must not change.** Output is *always* the named form. The catalog affects arg **order** only — which is cosmetic, because JSON object key order was never semantic. This is what keeps the documented `parse(printInline(node))` round-trip catalog-free.

- [ ] **Step 1: Write the failing tests**

Append to `ui/packages/rules-core/test/dsl-printer.test.ts`, inside `describe('print', …)`, reusing a catalog fixture declared below the imports:

```ts
const CATALOG: Catalog = {
  specs: [{
    name: 'at-least', modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Compiled',
    parameters: [{ name: 'floor', type: 'integer' }, { name: 'label', type: 'string' }],
  }],
  collections: [],
};
```

```ts
  it('prints args in declared order when a catalog is supplied', () => {
    const document: RuleDocument = {
      rule: { spec: 'at-least', args: { label: 'high', floor: 2 } },
    };
    expect(print(document, { catalog: CATALOG })).toBe('at-least(floor = 2, label = "high")');
  });

  it('prints args in insertion order without a catalog', () => {
    const document: RuleDocument = {
      rule: { spec: 'at-least', args: { label: 'high', floor: 2 } },
    };
    expect(print(document)).toBe('at-least(label = "high", floor = 2)');
  });

  it('prints an undeclared arg after the declared ones', () => {
    const document: RuleDocument = {
      rule: { spec: 'at-least', args: { extra: 1, floor: 2 } },
    };
    expect(print(document, { catalog: CATALOG })).toBe('at-least(floor = 2, extra = 1)');
  });
```

Add `import type { Catalog } from '../src/contracts.js';` to the file's imports.

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm -C ui/packages/rules-core test dsl-printer`

Expected: FAIL — `print` takes one argument, so the options argument is a typecheck error, and the declared-order cases emit insertion order. The `without a catalog` case should already PASS; it pins the behaviour that must not change.

- [ ] **Step 3: Implement declared-order printing**

In `ui/packages/rules-core/src/dsl/printer.ts`, add:

```ts
import type { Catalog } from '../contracts.js';

/** Options for {@link print} and {@link printInline}. */
export interface PrintOptions {
  /**
   * The spec catalog, used only to order arguments by declaration. Output is always the named
   * form, so a document printed without a catalog still reparses identically — only the order of
   * the arguments differs, and object key order was never semantic.
   */
  catalog?: Catalog;
}
```

Thread `options` through `printNode` / `printBody` / the child helpers as an extra parameter, and order the entries in `printArgs`:

```ts
function printArgs(node: SpecNode, options?: PrintOptions): string {
  const entries = Object.entries(node.args ?? {});
  if (entries.length === 0) return '';

  const declared = options?.catalog?.specs.find((entry) => entry.name === node.spec)?.parameters;
  const ordered = declared === undefined ? entries : [...entries].sort(([a], [b]) => {
    // An argument the catalog does not declare sorts after every declared one, keeping its
    // relative position among the other undeclared ones.
    const rank = (name: string): number => {
      const index = declared.findIndex((parameter) => parameter.name === name);
      return index === -1 ? declared.length : index;
    };
    return rank(a) - rank(b);
  });

  const rendered = ordered.map(([name, value]) => `${name} = ${printArgValue(value)}`);
  return `(${rendered.join(', ')})`;
}
```

Update `printBody`'s spec line to `return `${node.spec}${printArgs(node, options)}`;` and widen the two exports:

```ts
export function print(document: RuleDocument, options?: PrintOptions): string {
  return `${printParameters(document.parameters)}${printNode(document.rule, '', 'block', options)}`;
}

export function printInline(node: RuleNode, options?: PrintOptions): string {
  return printNode(node, '', 'inline', options);
}
```

> `Array.prototype.sort` is stable in every runtime this targets, which is what keeps undeclared args in their original relative order.

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm -C ui/packages/rules-core test dsl-printer`
Expected: PASS

Run: `pnpm -C ui/packages/rules-core test && pnpm -C ui/packages/rules-core typecheck && pnpm -C ui/apps/demo test`
Expected: PASS. The round-trip suite calls `print`/`printInline` with no options and must stay green.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/printer.ts ui/packages/rules-core/test/dsl-printer.test.ts
git commit -m "feat(rules-core): order printed args by declaration

Cosmetic only -- output is always the named form, so the documented
printInline round-trip stays catalog-free. Undeclared args sort last."
```

---

## Final verification

```bash
pnpm -C ui/packages/rules-core test
pnpm -C ui/packages/rules-core typecheck
pnpm -C ui/apps/demo test
pnpm -C ui/apps/demo typecheck
dotnet test src/Motiv.Serialization.AspNetCore.Tests
pnpm -C ui/apps/demo e2e
```

Then, per CLAUDE.md's mandatory post-implementation step, spawn a `code-simplifier` agent over the changed files and apply what it finds, re-running the affected tests.

## Verification obligations from the design

- [ ] A gate document using `change.approver-count-at-least(n = 1)` round-trips through the DSL with its args intact — the concrete Spec 1 case.
- [ ] `git diff --stat` shows no change under `schemas/`, and no change to C# binder behaviour.
- [ ] Commas highlight identically in the CodeMirror editor and the builder's inline rows.
