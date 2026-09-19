---
title: Expression Leaves
---

## What an expression leaf is

A rule document leaf is either a `{ "spec": "…" }` reference to a registered proposition or an
`{ "expression": "…" }` node: one condition, written in a small owned language, over the model in
scope at that position. In the DSL a leaf is a backtick literal &mdash; `` `age >= 18` `` &mdash;
and everything around it (composition, quantification, naming) stays ordinary DSL. A leaf binds to
the equivalent of `Spec.From(model => expression)`, so its assertions are the decomposed clauses a
compiled expression would produce, not a name-and-suffix pair.

## The language

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

An identifier is ASCII: `[A-Za-z_][A-Za-z0-9_]*`. A non-ASCII character in a name is a syntax
error &mdash; `InvalidExpression` &mdash; not a silently-accepted field name.

Names are the catalog's JSON-schema names: whatever completion offers and the scenario table
shows, mapped to the underlying CLR member through the serializer's naming policy. There are no
CLR names, no PascalCase, in a leaf.

Two method sets are closed and grow only by name:

| On | Methods |
|---|---|
| Collections | `where`, `any`, `all`, `count`, `sum`, `min`, `max` |
| Strings | `equalsIgnoreCase` |

There are no casts, no user-defined functions, and no date/time support in v1.

| Expression | Binds to |
|---|---|
| `` `age >= 18` `` | a comparison against `int` |
| `` `orders.where(o => o.status == "paid").sum(o => o.total) > @vipThreshold` `` | a filtered aggregate compared against a `number` parameter |
| `` `orders.all(o => o.total <= creditLimit)` `` | a quantifier with a comparison against a sibling field, via an explicit lambda parameter |
| `` `name.equalsIgnoreCase("acme")` `` | a case-insensitive string comparison |
| `` `orders == null || orders.count() == 0` `` | a null check ahead of a method call on the same nullable collection |

## Scope

Scope is positional, not lexical in the general sense:

- **At the document root**, a leaf sees the rule's model.
- **Inside a quantifier body** (`in orders { … }`), a leaf sees the collection's element type.
- **Inside a lambda** (`o => …`), the lambda's parameter names the element, and the *enclosing*
  scope stays reachable &mdash; `orders.all(o => o.total <= creditLimit)` compares each order's
  total against the customer-level `creditLimit`.

The one shape with no spelling is the reverse: a DSL quantifier's body cannot see its parent, so
`atLeast(2) in orders { `total > creditLimit` }` has no way to reach `creditLimit` without an
explicit lambda inside the leaf itself. That asymmetry is why lambdas in the leaf language are
explicit rather than implicit.

`&&`, `||` and `!` are permitted at any level inside a leaf, but the editor warns when a top-level
connective could be DSL instead &mdash; DSL gives each side its own row, its own `whenTrue`/
`whenFalse`, and its own assertion.

## Semantics

- **Strings** compare ordinal and case-sensitive. `equalsIgnoreCase` is
  `StringComparison.OrdinalIgnoreCase` &mdash; there is deliberately no `.lower()`, since case
  mapping (not comparison) is where the Turkish-i class of bugs lives.
- **`null`** is an operand of `==`/`!=` only. Navigating through a null value yields null, calling
  a method on a null collection yields null, and any other operator with a null operand yields
  `false`. A leaf never throws on a shape the schema already said was possible, which a
  hand-written `Spec.From` lambda would.
- **Numerics** align with C# where C# is exact, and refuse where C# would be lossy. Literals and
  parameters are untyped until solved; model fields and fixed-type methods (`count()` is always
  `int`; `sum(o => o.total)` is whatever `total` is) are anchors. An untyped subtree takes the join
  of its anchors over the allowed widenings:

  | Allowed | Refused |
  |---|---|
  | `int → long` | `long → double` |
  | `int → decimal` | anything `→ float` |
  | `long → decimal` | `decimal ↔ double` |
  | `int → double` | |
  | `float → double` | |

  A refused pair is `ExpressionTypeMismatch`, naming both types. A fractional literal prefers
  `decimal`. **Mixed `integer`/`number` parameters in one untyped subtree solve to `decimal`** —
  a `number` parameter's constraint always wins the join, since only `decimal` honors both an
  integral and a fractional declared kind at once. An unanchored subtree defaults to `int` (or
  `decimal`, if anything in it is fractional) with a warning, not an error. Integer `/` truncates,
  with a warning.
- **A parameter's declared kind is a constraint**: `integer` may solve to any integral or exact
  type; `number` to any fractional one.
- **The result is boolean.** A `where`/`any`/`all` whose lambda body is not boolean reports one
  `ExpressionTypeMismatch` at that body and does not cascade further errors from the same cause.
- **Type strings**, as shown in hover and facts, are `int`, `long`, `float`, `double`, `decimal`,
  `bool`, `string`, `object`, `collection`. A `?` suffix appears only on a value kind (numeric or
  `bool`) reached through a nullable path &mdash; `int?`, not `Customer?`: every reference-typed
  member is treated as nullable already, since reflection cannot see nullable-reference
  annotations, so a `?` there would say nothing.
- **The catalog stamps `format`** on numeric schema fields &mdash; `int32`, `int64`, `single`,
  `double`, `decimal` &mdash; so the editor can tell a `decimal` property from a `double` one
  without guessing from JSON Schema's bare `"number"` type.

## What Studio shows

- **Completion** in the DSL pane's backtick sub-language, scoped to the position: model fields,
  parameters, the two closed method sets, and (inside a lambda) the lambda parameter alongside
  everything from the enclosing scope.
- **Lint**, from both sides: the client parses and checks on the keystroke; the server checks again
  after the debounce and its message wins where both report, at the same range.
- **Hover facts**, e.g. `` `1000` as `decimal`, from `orders.sum(o => o.total)` `` &mdash; what a
  literal or parameter solved to, and which anchor decided it.
- **The inspector**: a `region` named "expression inspector" under the DSL pane, showing the scope
  of the leaf under the caret and how it reads against the scenario whose details are open in the
  scenario table. A leaf inside a quantifier body is read wrapped in its nearest enclosing
  quantifier, so the reading reflects what the quantifier actually evaluates, not the leaf in
  isolation.

## Errors

| Code | Example |
|---|---|
| `InvalidExpression` | `` `age >=` `` &mdash; the expression is not syntactically complete |
| `UnknownField` | `` `ager >= 18` `` &mdash; `ager` is not a field of the model in scope |
| `UnknownMethod` | `` `name.trim()` `` &mdash; `trim` is outside the closed method sets |
| `ExpressionTypeMismatch` | `` `total > "1000"` `` &mdash; a number compared with a string |
| `ExpressionRequiresMetadata` | a bare leaf in a metadata document, with no `whenTrue`/`whenFalse` of its own and no enclosing node supplying them |

A syntax or type problem reports as a diagnostic at its range, exactly like any other lint
finding. A document that lints clean but still fails to bind shows the server's error the same way
an unknown spec reference does today. Arithmetic overflow at evaluation time throws a checked
exception carrying the rule name and leaf path; a null operand never throws.

## Limits

- **`netstandard2.0` does not bind leaves.** A bare leaf on that target framework reports
  `ExpressionsNotEnabled` &mdash; "expression nodes are supported on .NET 8 or later." The target
  exists for Framework consumers of documents authored elsewhere; a second numeric implementation
  without generic math is exactly the drift the conformance corpus exists to prevent.
- **No casts.** A value's type is whatever its anchor gives it; there is no `(decimal)` or
  `(int)` syntax to force one.
- **The method sets are closed**, by name, on purpose &mdash; growing them is a language change,
  not a per-call opt-in.
- **In a metadata document**, a bare leaf carries no `TMetadata` of its own: it, or an enclosing
  node, must supply `whenTrue`/`whenFalse`, or binding reports `ExpressionRequiresMetadata`.

## Remarks

- **`decimal` is the default fractional type**, not `double`, because it is the join that honors
  both an `integer` and a `number` parameter declaration at once, and because C#'s own implicit
  conversions never let `double` and `decimal` mix without a cast &mdash; there was never a lossless
  default to prefer instead.
- **Null never throws.** Every model already carries nullable members the schema exporter marks as
  such; a leaf that panicked on a null the schema said was possible would be less honest than the
  registered specs it composes with.
- **The conformance corpus is the contract.** `ui/packages/rules-core/test/expression/corpus.json`
  pins the same cases &mdash; the same leaf text, scope, and expected problems or facts &mdash; run
  by both the TypeScript checker and the C# binder. A case that passes on one side and fails on the
  other fails CI, which is what keeps completion, lint and hover in the editor honest about what
  the server will actually bind.
