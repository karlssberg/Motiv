# Spec Args in the TS DSL — Design

**Date:** 2026-08-13
**Status:** Approved (design)
**Source:** Disclosure 2 on [#123](https://github.com/karlssberg/Motiv/pull/123) (Spec 1 — Trust &
Control), which shipped parameterised specs via spec-node `args` on the C# side and recorded that
`@motiv-rules/core` does not know them. Ticket [06](https://github.com/karlssberg/Motiv/issues/106)
governs the schema/versioning constraints.

## Summary

`Motiv.Serialization` accepts `args` on a spec node — the mechanism Spec 1's ten built-in `change.*`
gate specs use to be parameterised (`approver-count-at-least` with `n = 1`). `schemas/rule.v1.json`
declares it. The TypeScript stack does not: `args` appears **nowhere** in `ui/packages/rules-core/src`,
so `SpecNode` has no such field and the DSL printer drops it on round-trip.

This design closes that hole in two steps. **Step 1** teaches the TS types and the DSL grammar about
named args, with no catalog dependency. **Step 2** adds ordered parameter metadata to the catalog so
args may be *typed* positionally, while names remain the stored source of truth.

## Why now

Today the gap is unreachable: no UI authors args, and gate configuration is API-only. It stops being
unreachable the moment `args` reaches the editor, and the failure mode is silent — a document
round-tripped through the DSL loses its args and publishes as a different rule. Under an approval
gate with an append-only version log, a silent semantic change is the worst available failure.

## Decisions (locked)

1. **Named args are the contract.** The rule document stores `args` as a name→value map. This is
   unchanged from `rule.v1.json` and the C# `Dictionary<string, object?>` binder; **no schema change,
   no C# binder change.**
2. **Parenthesised call syntax with `=`** — `change.approver-count-at-least(n = 1)`.
3. **Positional order is a catalog *hint*, never document data.** Order lives in `CatalogEntry`, is
   consumed at author time, and never enters a rule document.
4. **The printer always emits the named form.** Positional is input-only.
5. **Two steps, one design.** Step 1 stands alone and closes the hole; step 2 is ergonomics on top.

### Why `=` and not `:`

The grammar has already assigned both tokens a meaning, in its one existing use of each: in
`param n: integer = 5`, `:` introduces a **type** and `=` introduces a **value**. Spec args are
values, drawn from very nearly the same literal union as a parameter default. Choosing `:` would
teach a reader "`:` is followed by a type" and then break it. `=` keeps one token, one meaning — and
`parseDefault` already reads exactly that literal union.

### Why named args, and not positional binding

Positional binding would mean that reordering a spec's parameters silently changes the meaning of
every stored document using the positional form. Named args are immune. For a product whose decision
log exists to answer "why was *this* customer declined?", a stored document that quietly re-binds
after an unrelated code change is a correctness hazard, not a style preference.

Resolving positional input to names **at author time** keeps the ergonomics without the hazard: a
stale hint can only affect the line being typed, which is the same exposure any autocomplete has.

## Scope

### In scope

- `ArgValue` and `SpecNode.args` in `rules-core`.
- DSL lexer/parser/printer support for named args, including the round-trip guarantee.
- The two known hand-copy duplication sites in the demo (highlighting).
- Step 2: ordered `parameters` on the catalog (C# + TS), positional *input*, declared-order printing.

### Non-goals

- **`@param` references inside args.** `RuleParameterSubstituter` interpolates `whenTrue`/`whenFalse`
  text and resolves `n`, but never touches `args`. Accepting `@param` in TS would author documents
  the backend silently ignores.
- **Catalog-driven completion or lint of arg names/values.** Deferred editor-affordance scope; step
  2's catalog work is its prerequisite.
- **Any change to `rule.v1.json`, the C# binder, or stored documents.**
- Escaping inside DSL strings. The DSL has no escapes, so a `"` cannot appear in a quoted name — a
  pre-existing limitation of `as "name"`, inherited unchanged and not addressed here.

---

## Step 1 — named args, catalog-free

### Types (`ui/packages/rules-core/src/document.ts`)

```ts
/** A value supplied to a parameterised spec. Literals only — the binder does not
 *  substitute `@parameter` references into args. */
export type ArgValue = string | number | boolean | null;

export interface SpecNode extends Decoration { spec: string; args?: Record<string, ArgValue> }
```

`args` lands on `SpecNode` only, mirroring `rule.v1.json` where `args` appears solely in `specNode`.
`src/index.ts` is `export *`, so `ArgValue` publishes without a barrel edit — noting that ticket 06
requires curating this barrel before first publish, which is separate work.

### Grammar

```
primary := SPEC args? | `expr` | '(' expr ')' | quantifier
args    := '(' arg (',' arg)* ')'
arg     := (SPEC | STRING) '=' literal
literal := NUMBER | STRING | 'true' | 'false' | 'null'
```

Two collision checks make this additive rather than breaking:

- A `(` following a **spec** token is currently a hard `UnexpectedToken` error, so no existing
  document can mean anything by it.
- The counted quantifier's `(` follows a **quantifier** token — a different `TokenKind`. The two
  uses of `(` never meet, so `exactly(2) in orders { … }` is unaffected.

### Lexer (`src/dsl/lexer.ts`, `src/dsl/types.ts`)

One new token kind: `,` → `comma`. There is no comma in the language today; it currently lexes as an
`error` character. Nothing else needs lexer work — `(`/`)` are `paren`, `=` is `equals`, and
`true`/`false`/`null` arrive as plain word tokens, exactly as `parseDefault` already handles for
parameter defaults.

### Parser (`src/dsl/parser.ts`)

`parsePrimary`'s spec branch consumes an optional arg list:

```ts
if (token.kind === 'spec') {
  state.next();
  const args = parseArgs(state);           // undefined when no '(' follows
  return args ? { spec: token.value, args } : { spec: token.value };
}
```

- **Quoted arg names** are accepted (`s("all" = 1)`). Without this, keyword-shaped keys are
  unrepresentable — the same gap `param all: integer` already has. Since this design exists to stop
  silent loss, the gap is closed rather than documented.
- **Duplicate arg names** are a parse error, not last-wins. Last-wins is silent loss.
- Keys are inserted with `Object.defineProperty`, following the `__proto__` precedent established in
  `parseParameters`.
- New error codes follow the existing idiom: `ExpectedArgName`, `ExpectedArgValue`, `UnclosedArgs`,
  `DuplicateArg`.

### Printer (`src/dsl/printer.ts`)

`printBody`'s spec branch becomes `${node.spec}${printArgs(node.args)}`. `printArgs` returns `''`
for absent or empty args, so `args: {}` prints as bare `s` and round-trips to `{spec:'s'}` —
semantically identical, documented and tested. `s()` is a parse error. Names are quoted only when a
bare word would not survive relexing; values reuse the existing literal rendering, extended for
`null`.

### Deliberately unchanged

- **`decorations.ts`** — `isCompatible` compares `a.spec === b.spec`, and args must *not* join that
  check: retyping `s(n = 1)` as `s(n = 2)` is the same spec and should keep its `whenTrue`/`whenFalse`
  payloads.
- **Spans** — `parsePostfix` records `start..lastEnd` *after* `parsePrimary` returns, so args fall
  inside the node's span for free.
- **`validation.ts`** — client-side validation delegates to the server, so nothing in TS rejects args
  today and nothing needs to start.

### Duplication sites that must move in lockstep

Both are the hand-copy drift `lexer.ts` warns about in its own comments:

| Site | Change | Symptom if missed |
|---|---|---|
| `ui/apps/demo/src/dsl/motivLanguage.ts:66` | add `,` to the `':' \|\| '='` punctuation check | commas lose highlighting in CodeMirror |
| `ui/apps/demo/src/styles/app.css:614` | add `.tok-comma` beside `.tok-colon`/`.tok-equals` | commas render in body colour in builder rows |

---

## Step 2 — positional hints from the catalog

### C# surface

`SpecRegistryEntry.Parameters` is currently `internal IReadOnlyList<RuleParameterDeclaration>?`.
`Motiv.Serialization.csproj` already grants `InternalsVisibleTo` to `Motiv.Serialization.AspNetCore`,
so **the catalog endpoint can read it as-is**: the property stays internal and the endpoint projects
an ordered `parameters` array onto the catalog response. Widening `Parameters` to public is therefore
unnecessary and is not done — the HTTP contract is the right place for this to become public, not the
registry type. The catalog addition is additive on a package that has never shipped, so it costs
nothing under ticket 06.

### TS surface

```ts
export interface CatalogParameter {
  name: string;
  type: ParameterDeclaration['type'];   // 'integer' | 'number' | 'string' | 'boolean'
  default?: ArgValue;
}
export interface CatalogEntry { /* … existing … */ parameters?: CatalogParameter[] }
```

`parse(text, options?)`, `print(document, options?)` and `printInline(node, options?)` take an
optional catalog. All three keep working without one.

- **Parse** accepts positional `s(1)` only when the catalog is present *and* names the spec.
  Otherwise positional is a parse error — never a guess. Positional args must precede named ones,
  mirroring C#. A spec whose catalog entry has no `parameters` rejects args outright, pre-empting the
  server's `UnexpectedArguments`.
- **Print** always emits the **named** form, ordered by declaration when a catalog is available and
  by insertion order otherwise. Arg order is cosmetic; JSON object key order was never semantic.

### The invariant this preserves

`printer.ts` documents that `parse(printInline(node)).document.rule` deep-equals `node` — the
property that makes a rendered builder row safe to hand back to the parser. Because printer output is
always named, that round-trip **never needs a catalog**. The catalog may be absent, stale, or wrong
and the guarantee is unaffected. Positional is strictly an input affordance.

---

## Testing

Test-first throughout, per the repo's TDD requirement. The headline is the round-trip property in
`dsl-roundtrip.test.ts` — the test that fails today and encodes the actual bug.

**Step 1**
- Round-trip: a document with args survives document → DSL → document unchanged.
- Parser: each literal type (number, negative number, string, `true`, `false`, `null`); multiple args;
  args combined with an `as` clause; args inside quantifier bodies and under negation.
- Parser errors: missing `=`, missing value, unclosed paren, duplicate name, `s()`, arg name that is a
  bare keyword.
- Printer: named output, quoting only when required, omission for absent and empty args.
- `printInline` parity with `print` for an arg-bearing node.
- `__proto__` as an arg name does not reach the prototype setter.

**Step 2**
- Positional input resolves to named storage against a catalog.
- Positional input without a catalog is an error, not a guess.
- Positional input for a spec absent from the catalog is an error.
- Args on a spec with no declared `parameters` are rejected.
- Positional-after-named is an error.
- Print orders args by declaration when a catalog is supplied, and does not reorder without one.

**Regression surface:** the full `rules-core` suite plus the demo's UI tests, since the lexer's
`TokenKind` union widens. `pnpm e2e` per the repo's e2e note (never bare `playwright test`).

## Verification obligations

- A gate document using `change.approver-count-at-least(n = 1)` round-trips through the DSL with its
  args intact — the concrete case from Spec 1.
- No change to any file under `schemas/`, and no change to C# binder behaviour, provable by diff.
- Commas highlight identically in both the CodeMirror editor and the builder's inline rows.

## Follow-ups this does not close

- Curating the `rules-core` barrel before first publish (ticket 06).
- Catalog-driven completion and lint of arg names and values (editor affordances).
- The DSL's lack of string escapes, which bounds representable names in `as` clauses and arg keys
  alike.
