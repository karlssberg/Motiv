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

## Remarks

- **Binding is deferred; failure is not.** A document default is parsed and bound when the rule is
  added to a `RuleSet` &mdash; an invalid document fails there, at startup, with the rule's name in
  the exception, never at first evaluation.
- **Reverts re-bind the document.** `DELETE`/`Revert()` restores the document default as a new,
  incremented version.
- **Documents are the interchange format.** The same JSON shape flows through the
  [HTTP endpoints](AspNetCore.md), so a document authored or exported from a rule-builder UI can be
  embedded verbatim as a rule's default.

## Next Steps

- Pass a document source to one of the [Rule Classes](Rules.md) constructors.
- See [`RuleSet`](RuleSet.md) for when the document binds and how reverts behave.
