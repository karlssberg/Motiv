# Studio — expression leaves: a small owned language, solved on both sides

**Date:** 2026-09-18 · **Decided by prototype:** branch `prototype/expression-leaf-authoring`
(variant A of three) · **Brainstormed:** 2026-09-15 to 2026-09-18

## The problem

The rule document format has had an `{ "expression": "…" }` leaf since 2026-07-15, the DSL lexes
a backtick literal into it, and Studio's node toolbar shows a disabled "expression — coming"
button. Nothing evaluates it: all four binders return `ExpressionsNotEnabled`, naming a
`Motiv.Serialization.Expressions` package that was designed around System.Linq.Dynamic.Core and
never built. Every runtime proposition therefore bottoms out in a registered spec, which the
propositions docs state as "a predicate is C#".

The ask was to design the expression builder. The opening thought — a C# editor constrained to
lambda syntax — was set aside on the first question: the editor is not the cost, the language
server behind it is. Real C# needs a Roslyn workspace per document and accepts far more than
`Spec.From` can decompose; Dynamic LINQ has no language service at all and a grammar Studio would
model none of. Both are remote code execution by any Studio author. An owned language is the only
target the editor can be honest about.

## The decision

An expression leaf holds **one condition over the model in scope at its position**, in a small
owned language: comparisons and arithmetic over model fields, `@parameters` and literals, plus a
closed set of collection methods with explicit lambdas. Composition and quantification stay in the
DSL around it. The leaf is **authored in the DSL pane**, where backticks are a live sub-language
with highlighting, completion, lint and an inspector that says what the leaf under the caret is
scoped to and reads as. Numeric types are **solved on both client and server** by one set of
rules, pinned by a shared conformance corpus, with the server as the authority.

### The language

```
leaf     := or
or       := and ( "||" and )*
and      := eq ( "&&" eq )*
eq       := cmp ( ( "==" | "!=" ) cmp )?
cmp      := add ( ( "<" | "<=" | ">" | ">=" ) add )?
add      := mul ( ( "+" | "-" ) mul )*
mul      := unary ( ( "*" | "/" ) unary )*
unary    := ( "!" | "-" ) unary | postfix
postfix  := primary ( "." ident ( "(" args? ")" )? )*
args     := ( lambda | or ) ( "," ( lambda | or ) )*
lambda   := ident "=>" or
primary  := number | string | "null" | "true" | "false" | "@" ident | ident | "(" or ")"
```

- **Names** are the catalog's JSON-schema names — what completion can offer and what the scenario
  table shows — and the binder maps them to CLR members through the serializer's naming policy.
- **Methods** are closed sets, grown by name: on collections `where any all count sum min max`;
  on strings `equalsIgnoreCase`. No casts, no user functions, no date and time in v1.
- **Scope** is positional. At the root a leaf sees the rule's model; inside `in orders { … }` it
  sees the element; inside a lambda its parameter names the element and the enclosing scope stays
  reachable. That last point is the reason lambdas are explicit: the outer quantifier's body
  cannot see its parent, so `orders.all(o => o.total <= creditLimit)` has no DSL spelling.
- **`&&`, `||`, `!` are permitted at any level.** The editor warns when a top-level connective
  could be DSL instead, since DSL gives each side its own row and its own `whenTrue`/`whenFalse`.

In a document:

```
param minOrders: integer = 3
param vipThreshold: number = 1000

let is-adult = `age >= 18`
let is-big-spender = `orders.where(o => o.status == "paid").sum(o => o.total) > @vipThreshold`
let has-no-risky-order = `orders.all(o => o.total <= creditLimit)`

customer.is-active
  & is-adult
  & atLeast(@minOrders) in orders { order.is-large | `daysSinceShipped < 30` }
  & (is-big-spender || has-no-risky-order)
```

### Semantics

- **Strings** compare ordinal and case-sensitive. `equalsIgnoreCase` is
  `StringComparison.OrdinalIgnoreCase`. Not `.lower()`: case mapping is where the Turkish-i class
  of bugs lives.
- **`null`** is an operand of `==` and `!=` only. It exists because the models already have
  nullable members (`Orders?`, `CustomerId?`) and the schema exporter marks them so. Everything
  else is semantics: navigation through null yields null, a method on a null collection yields
  null, and any other operator with a null operand yields `false`. A live rule never throws on a
  shape the schema said was possible — which a hand-written `Spec.From` lambda would.
- **Numerics** align with C# where C# is exact and refuse where it is lossy. Literals and
  parameters are *untyped* until solved. Model fields and fixed-type methods (`count()` is `int`;
  `sum(o => o.total)` is whatever `total` is) are **anchors**; operators generate constraints; an
  untyped leaf takes the join of its anchors over the allowed widenings. `int→long`, `int→decimal`,
  `long→decimal`, `int→double`, `float→double` are allowed; `long→double`, anything to `float`, and
  `decimal↔double` are refused with an error naming both types. A fractional literal prefers
  `decimal`. An unanchored subtree defaults to `int` or `decimal` with a warning. A parameter's
  declared kind is a constraint: `integer` may solve to any integral or exact type, `number` to any
  fractional one. Arithmetic is checked. Integer `/` truncates, with a warning.
- **The result is boolean.** A leaf binds to the equivalent of `Spec.From(model => expression)`,
  so its assertions are the decomposed clauses a compiled expression produces.
- **`netstandard2.0`** keeps `ExpressionsNotEnabled`, reworded: leaves need .NET 8 or later. The
  target exists for Framework consumers of documents authored elsewhere, and a second numeric
  implementation without generic math is exactly the drift the corpus exists to prevent.

### The TypeScript side

`@motiv-rules/core` gains an `expression/` module beside `dsl/`, editor-neutral and pure over a
leaf text and a scope: `tokenize`, `parseLeaf` (ranges on every node), `scopeAt(document, path,
catalog)` (root model or quantifier element, from `modelTypes` and the collection's `items`),
`solve`/`check` (the lattice and the checker, over schemas carrying `format`), and
`completeLeaf` (the shape `completeDsl` returns). The DSL layer learns about leaves at three
points: `diagnostics.ts` runs `check` on each `ExpressionNode` and offsets ranges into the
document; `completeDsl` delegates into a backtick; `printer` is untouched.

Studio: the stream parser in `motivLanguage.ts` gains the prototype's nested leaf mode; `hover.ts`
shows a leaf's facts ("`1000` as `decimal`, from `orders.sum(o => o.total)`"); `lint.ts` maps a
`range` on a backend error; `DslEditor` gains the inspector strip — the scope of the leaf under the
caret and its reading against the selected scenario, from the evaluate endpoint the scenario table
already calls. The builder's inline row editor takes the same language. `NodeToolbar`'s
placeholder button goes.

Contracts: `JsonSchema.format`, `RuleError.range?`, `ValidationResponse.facts?`, and the new codes.

### The C# side

`Motiv.Serialization/Expressions/`, unit for unit the twin of the TypeScript module:
`LeafTokenizer`, `LeafParser`, `LeafScope` (reflection: JSON names via the naming policy and
`[JsonPropertyName]`, element types of `IEnumerable<T>` members), `NumericLattice` (the widening
table, conversions by `T.Parse` and `T.CreateChecked` under generic math), `LeafChecker` (problems
with ranges, facts per node), `LeafCompiler` (AST → `Expression<Func<TModel, bool>>` of native
nodes with typed constants and checked arithmetic → `Spec.From`).

Generic math is used for the *conversions* only. The operator nodes stay native
`Expression.GreaterThan` and friends, because the decomposer prints what it sees: a helper call
would turn `total > 1000 == true` into `NumericOps.GreaterThan(c.Total, 1000) == true`.

The four binders replace their `BindExpressionLeaf` stub with the compiler; the async binders lift
with `ToAsyncSpec`. In the metadata binders a leaf has no `TMetadata`, so it or an enclosing node
must carry `whenTrue`/`whenFalse` metadata; a bare leaf there is `ExpressionRequiresMetadata`.

**One change to `src/Motiv`:** a nullable member access compiles to a conditional the serializer
would print as `c.Orders == null ? null : c.Orders.Count()`. The compiler tags those conditionals
and `CSharpExpressionSerializer` prints a tagged one as `c.Orders?.Count()`.

Endpoints: the catalog's schema exporter gains a `TransformSchemaNode` that stamps `format`
(`int32`, `int64`, `decimal`, `double`, `single`) on numeric members; validate returns `range`
and `facts`, both optional. `RuleErrorCode` gains `InvalidExpression`, `UnknownField`,
`UnknownMethod`, `ExpressionTypeMismatch`, `ExpressionRequiresMetadata`. The governance comparer
already compares expression text.

### The conformance corpus

`ui/packages/rules-core/test/expression/corpus.json`, linked into `Motiv.Serialization.Tests` as
content. Each case: a leaf text, its scope (a model id in a shared fixture schema, or a
collection's element), and either expected problems (codes and ranges) or expected facts (solved
types per literal and parameter, and the result type). The fixture schema is the prototype's
richer customer — the dev host's model is too thin to exercise widening or nullables — carried as
data on the TypeScript side and as a record with matching JSON names on the C# side. A case that
passes on one side and fails on the other fails CI.

### Errors, end to end

- A syntax or type problem is a diagnostic at its range: from the client on the keystroke, from
  the server after the debounce. Where both report, the server's message wins.
- A document that lints clean but fails to bind shows the server error as unknown spec references
  do today.
- Arithmetic overflow at evaluation throws a checked exception carrying the rule name and leaf
  path, surfaced by the existing evaluation error handling. Null never throws.

## The variants weighed

Three shapes of leaf language, then three authoring surfaces.

**Language.** (A) C#-flavoured with connectives and lambdas — duplicates the DSL's `&&` and
`any in`, so one leaf shows three clauses; needs PascalCase names the catalog does not have.
(B) Value expressions with a comparison at the top and nothing else — one clause per leaf, but no
spelling for a filtered aggregate or a parent-in-scope comparison. (B + explicit lambdas) closes
both gaps with a closed method set and is what was chosen; implicit-scope (`any(orders, total >
@limit)`) and indexer (`orders[status == "paid"]`) forms were rejected for having no way to name
the parent.

**Surface.** Prototyped on the rule route behind `?variant=`: (A) text-first in the DSL pane,
(B) a field with schema chips in the builder row's detail, (C) structured pickers. A won: the DSL
pane already had a place to type and a parse that knows the position's scope; B and C each
re-derived scope from a document path, and C invented a second model of the leaf. Building them
surfaced three things the design carries: the editor needs its own parser because completion and
lint cannot wait for a round trip; "reads as" against a sample model is the feature; pickers would
have to constrain operators by type.

**Where types are solved.** Server-only was considered — one implementation, no corpus, the
existing validate round trip as the channel. Both sides was chosen for instant type feedback and
offline correctness in the unit tests; the corpus is the price.

## Out of scope

- Casts, string methods beyond `equalsIgnoreCase`, date and time, user-defined functions.
- #241's copy-as-C#, though leaves make it nearer.
- Structured editing (variant C).
- `netstandard2.0` support for leaves.

## Documentation

`README.md` gets a brief example under Core Features; `docs/live-rules/expressions.md` carries the
grammar, semantics and numeric rules, with entries in `docs/toc.yml` and `docs/Overview.md`; the
"a predicate is C#" paragraph in `docs/propositions/index.md` is rewritten. The plan and this
design land in the same commit as the implementation.
