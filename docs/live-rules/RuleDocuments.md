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
so its `Reason` and `Assertions` take the `name == true` / `name == false` form, with any
`whenTrue`/`whenFalse` strings surfacing as `Values`. A definition declares no model type of its
own: it is bound at each reference, against that reference's model, so the same local can be used at
the document root and inside a quantifier body. A definition may reference other locals and any
catalog proposition, but locals may not be recursive, directly or through other locals.

**Name grammar.** A local name is an ASCII letter or `_`, followed by ASCII letters, digits, `-` or
`_` &mdash; no dot (locals don't namespace) and no space. The DSL's reserved words (`param`, `let`,
`in`, `as`, `integer`, `number`, `string`, `boolean`, `all`, `any`, `exactly`, `atLeast`, `atMost`)
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
