---
title: RuleSet
---

`RuleSet` is the set of live rules an application executes, and the write path for replacing them.
Adding a rule binds its default immediately (fail-fast at startup); `Update()` and `Revert()`
validate and bind first, then publish atomically &mdash; writers get optimistic version conflicts,
evaluators always see a coherent snapshot.

```csharp
public RuleSet(SpecRegistry registry, RuleSerializerOptions? options = null);

RuleSet Add(RuleBase rule);
RuleBase? Find(string name);
RuleSetEntry? FindEntry(string name);
IReadOnlyCollection<RuleSetEntry> Rules { get; }
int Count { get; }

RuleUpdateResult Update(string name, string documentJson, int expectedVersion);
RuleUpdateResult Revert(string name, int expectedVersion);
```

## Registering Rules

```csharp
var rules = new RuleSet(registry)
    .Add(new CanCheckoutRule())
    .Add(new FraudScreeningRule())
    .Add(new LoyaltyDiscountRule());
```

`Add()` binds the rule's default and publishes version 1 immediately. A rule with an invalid
default document throws a `RuleSerializationException` here &mdash; at startup, with the failing
rule's name in the message &mdash; rather than at first evaluation. Registration is intended to
finish at startup; `Update()`/`Revert()`/lookups are safe concurrently thereafter.

In ASP.NET Core, [`AddMotivRules()`](AspNetCore.md) builds this `RuleSet` for you from the rules
enrolled in DI.

## Updating and Reverting

```csharp
var outcome = rules.Update("can-checkout", documentJson, expectedVersion: 3);

var summary = outcome.Outcome switch
{
    RuleUpdateOutcome.Updated => $"replaced; now at version {outcome.Version}",
    RuleUpdateOutcome.VersionConflict => $"stale; the rule is at version {outcome.Version}",
    RuleUpdateOutcome.Invalid => $"rejected with {outcome.Errors.Count} error(s)",
    _ => "no rule registered under that name"
};
```

`Revert()` restores the rule's default with the same outcome contract. Expected outcomes are
values, not exceptions &mdash; `RuleUpdateResult` carries the `Outcome`, the `Version`
(new version on `Updated`, current version on `VersionConflict`), and the `Errors`
(on `Invalid`).

## Remarks

- **Optimistic concurrency.** Every write carries the version the caller last observed. If another
  writer has published in the meantime, the compare-and-swap fails and the caller receives
  `VersionConflict` with the current version &mdash; nobody's change is silently clobbered.
- **Validate → bind → publish.** `Update()` fully validates and binds the document *before*
  publishing; on `Invalid`, the live rule is untouched.
- **Versions only move forward.** Reverting bumps the version rather than restoring an old number,
  so a version observed once is never observed again with different content.
- **Coherent listings.** `Rules` and `FindEntry()` return `RuleSetEntry` records whose version and
  document come from a single snapshot &mdash; they are always mutually consistent, even while the
  rule is being replaced.
- **Shared binding semantics.** The `RuleSet` binds documents with the same registry and options as
  the rest of the serialization surface; construct it with the same `SpecRegistry` (and
  `RuleSerializerOptions`) used elsewhere so documents bind identically everywhere.

## Next Steps

- Declare the rules being registered with the [Rule Classes](Rules.md).
- Serve `Update()`/`Revert()` over HTTP with [ASP.NET Core Integration](AspNetCore.md).
- See [DeserializeAsyncSpec()](DeserializeAsyncSpec.md) for how async rules bind their documents.
