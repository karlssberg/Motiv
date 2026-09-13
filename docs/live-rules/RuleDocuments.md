---
title: RuleDocuments
---

`RuleDocuments` supplies rule-document JSON for use as a rule's default implementation. Passing a
`RuleDocumentSource` (rather than a compiled spec) to a [rule constructor](Rules.md) makes the
document the version-1 behavior &mdash; bound against the registry when the rule is added to a
[`RuleSet`](RuleSet.md), and restored by every revert.

```csharp
public static RuleDocumentSource FromJson(string json);
public static RuleDocumentSource Embedded(string resourceName);
public static RuleDocumentSource Embedded(string resourceName, Assembly assembly);
```

## FromJson()

Wraps raw rule-document JSON:

```csharp
public sealed class WeekendPromoRule() : Rule<Customer, string>(
    "weekend-promo",
    RuleDocuments.FromJson("""
        { "rule": { "and": [ { "spec": "is-active" }, { "spec": "has-orders" } ] } }
        """),
    "Whether the weekend promotion applies");
```

## Embedded()

Reads a rule document embedded in the calling assembly (or an explicitly given assembly). The name
matches by trailing resource-name segment, so a project-relative file name resolves without the
assembly-name prefix:

```csharp
public sealed class LoyaltyDiscountRule() : Rule<Customer, string>(
    "loyalty-discount", RuleDocuments.Embedded("loyalty-discount.json"),
    "Whether the customer qualifies for the loyalty discount");
```

with the document embedded in the project file:

```xml
<ItemGroup>
  <EmbeddedResource Include="Rules/loyalty-discount.json" />
</ItemGroup>
```

Both `"loyalty-discount.json"` and `"Rules/loyalty-discount.json"` resolve the resource; matching is
by whole trailing segment, so `"loyalty.json"` never binds `"e-loyalty.json"`. An
`InvalidOperationException` is thrown when no resource matches or when the name is ambiguous.

## Definitions

A document may carry a `definitions` object: document-local propositions that the `rule` tree (or
other definitions) can reference without a catalog round-trip. Each entry maps a local name to a
**definition** &mdash; a `rule` subtree plus optional `whenTrue` / `whenFalse` decoration &mdash;
and is referenced with a `{ "local": "<name>" }` node instead of `{ "spec": "<name>" }`:

```json
{
  "name": "qualifies for loyalty discount",
  "definitions": {
    "is-active-and-adult": {
      "rule": { "andAlso": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] },
      "whenTrue": "customer is active and an adult",
      "whenFalse": "customer is inactive or a minor"
    }
  },
  "rule": {
    "and": [
      { "local": "is-active-and-adult" },
      { "spec": "customer.has-orders" }
    ]
  }
}
```

A local binds exactly as a nested name does today &mdash; `Spec.Build(body).Create(name)`, or with
`whenTrue`/`whenFalse` supplied, `Spec.Build(body).WhenTrue(...).WhenFalse(...).Create(name)` &mdash;
so its `Reason` takes the `name == true` / `name == false` form. What it *asserts* depends on
whether it is decorated: a definition carrying `whenTrue`/`whenFalse` gives
`Assertions == ["<name> == true"]`, with those strings surfacing as `Values`, while an undecorated
definition adds no assertion of its own and its `Assertions` remain the body's &mdash; a definition
wrapping `customer.is-active` asserts `["customer is active"]`. A definition declares no model type of its
own: it is bound at each reference, against that reference's model, so the same local can be used at
the document root and inside a quantifier body. A definition may reference other locals and any
catalog proposition, but locals may not be recursive, directly or through other locals.

**Name grammar.** A local name is an ASCII letter or `_`, followed by ASCII letters, digits, `-` or
`_` &mdash; no dot (locals don't namespace) and no space. The DSL's reserved words (`param`, `let`,
`in`, `integer`, `number`, `string`, `boolean`, `all`, `any`, `exactly`, `atLeast`, `atMost`)
are not valid local names, since a local is also written and referenced as a bare word in the DSL.

**Paths.** A definition is addressed at `$.definitions.<name>`, and its body at
`$.definitions.<name>.rule…`, exactly as `$.rule…` addresses the root.

**Errors.** `InvalidLocalName` &mdash; a definition key, or the name a `local` node references, is
not a legal local name. `UnknownLocal` &mdash; a `local` node names a definition the document does
not declare. `CycleDetected` &mdash; two or more definitions reference each other in a loop; the
message joins the chain with ` → `. A malformed `definitions` entry or node shape reports the usual
`InvalidNode`.

**Compatibility.** A reader built before this feature has never heard of `definitions` or `local`
and rejects the document with `unknown property 'definitions'` &mdash; the same rule that already
governs every other document-level key. There is no `$schema` version gate: the parser never
inspects `$schema`, so an old reader's refusal is what marks a document as needing the newer one.

### Authoring in the DSL

The rule DSL declares a local with a `let <name> = <expression>` statement, placed after any
`param` statements and before the rule expression, each on its own line and separated by a blank
line:

```
param minOrders: integer = 3

let is-active-and-adult = customer.is-active && customer.is-adult

is-active-and-adult & customer.has-orders(min = @minOrders)
```

**Declaration order doesn't matter.** Every `let` name is collected before any statement's body is
parsed, so a `let` may reference another `let` declared further down the preamble. Cycles are still
refused &mdash; a local may not reference itself, directly or through another local &mdash;
regardless of where the declarations sit.

**A bare word resolves against the document's own `let` declarations.** A word that names a
declared local becomes a local reference; any other word becomes a catalog reference. This is why a
local name can't contain a dot (a dotted word is always a catalog reference) and can't be one of the
DSL's reserved words: `param`, `let`, `in`, `integer`, `number`, `string`, `boolean`, `all`,
`any`, `exactly`, `atLeast`, `atMost`.

**Shadowing a catalog proposition is a warning, not an error.** A root-level catalog proposition can
share a name with a `let` (a local never has a dot, so it can never collide with a namespaced
catalog name). Inside its own document, the local wins; the editor reports a *shadows catalog
proposition* warning at the declaration so the author notices.

**The DSL has no inline naming clause.** A rule's or nested node's `name`, `whenTrue` and
`whenFalse` live in the JSON document and are set from the builder, not written into the DSL text
&mdash; and they are preserved across a DSL edit, exactly like any other decoration. A nested named
node in an existing document is migrated to a `let` declaration with the *Extract to definition*
builder action.

**`whenTrue`/`whenFalse` on a definition are set in the builder, not in the DSL text** &mdash; as
for every node, decoration lives outside the expression and is edited alongside it, not written
inline.

## Remarks

- **Binding is deferred; failure is not.** A document default is parsed and bound when the rule is
  added to a `RuleSet` &mdash; an invalid document fails there, at startup, with the rule's name in
  the exception, never at first evaluation.
- **Reverts re-bind the document.** `DELETE`/`Revert()` restores the document default as a new,
  incremented version.
- **`Embedded(name)` infers its caller from the stack.** Call it directly from a method in the
  assembly holding the resource. Called from inside a short lambda that another assembly invokes
  &mdash; a DI factory, a test assertion wrapper &mdash; the JIT may fold that lambda into its
  invoker and the lookup will target the wrong assembly. Pass the assembly explicitly with
  `Embedded(name, assembly)` wherever it can be named.
- **Documents are the interchange format.** The same JSON shape flows through the
  [HTTP endpoints](AspNetCore.md), so a document authored or exported from a rule-builder UI can be
  embedded verbatim as a rule's default.

## Next Steps

- Pass a document source to one of the [Rule Classes](Rules.md) constructors.
- See [`RuleSet`](RuleSet.md) for when the document binds and how reverts behave.
