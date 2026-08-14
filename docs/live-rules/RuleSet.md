---
title: RuleSet
---

`RuleSet` is the set of live rules an application executes, and the write path for replacing them.
Adding a rule binds its default immediately (fail-fast at startup); `UpdateAsync()` and
`RevertAsync()` bind, persist, and publish in that order &mdash; writers get optimistic version
conflicts, evaluators always see a coherent snapshot, and nothing is live unless it is also durable.

```csharp
public RuleSet(SpecRegistry registry, IRuleStore? store = null, RuleSerializerOptions? options = null);

RuleSet Add(RuleBase rule);
RuleBase? Find(string name);
RuleSetEntry? FindEntry(string name);
IReadOnlyCollection<RuleSetEntry> Rules { get; }
int Count { get; }

Task<RuleUpdateResult> UpdateAsync(
    string name, string documentJson, int expectedVersion, RuleChangeProvenance provenance,
    CancellationToken cancellationToken = default);
Task<RuleUpdateResult> RevertAsync(
    string name, int expectedVersion, RuleChangeProvenance provenance,
    CancellationToken cancellationToken = default);
Task<RuleUpdateResult> RestoreAsync(
    string name, int targetVersion, int expectedVersion, RuleChangeProvenance provenance,
    CancellationToken cancellationToken = default);
Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken cancellationToken = default);
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
finish at startup; `UpdateAsync()`/`RevertAsync()`/lookups are safe concurrently thereafter.

In ASP.NET Core, [`AddMotivRules()`](AspNetCore.md) builds this `RuleSet` for you from the rules
enrolled in DI. Pass a store to [`AddRuleStore()`](durability.md) so published rules survive a
restart; without one, rules live for the process lifetime, as they always have.

## Updating and Reverting

```csharp
var outcome = await rules.UpdateAsync(
    "can-checkout", documentJson, expectedVersion: 3, new RuleChangeProvenance("alice", "tighten the check"));

var summary = outcome.Outcome switch
{
    RuleUpdateOutcome.Updated => $"replaced; now at version {outcome.Version}",
    RuleUpdateOutcome.VersionConflict => $"stale; the rule is at version {outcome.Version}",
    RuleUpdateOutcome.Invalid => $"rejected with {outcome.Errors.Count} error(s)",
    _ => "no rule registered under that name"
};
```

`RevertAsync()` restores the rule's default with the same outcome contract. Expected outcomes are
values, not exceptions &mdash; `RuleUpdateResult` carries the `Outcome`, the `Version`
(new version on `Updated`, current version on `VersionConflict`), and the `Errors`
(on `Invalid`).

Every write carries a `RuleChangeProvenance` &mdash; who is publishing, and why &mdash; recorded in
the [version log](durability.md#the-version-log) when a store is registered. See
[Rule Durability](durability.md) for what a version-log row records, how to read history with
`HistoryAsync()`, and how to roll back with `RestoreAsync()`.

## Remarks

- **Optimistic concurrency.** Every write carries the version the caller last observed. If another
  writer has published in the meantime, the write is refused and the caller receives
  `VersionConflict` with the current version &mdash; nobody's change is silently clobbered. When a
  store is registered, the version conflict is decided by the store's own `(Name, Version)` primary
  key, so it holds across replicas, not just within one process.
- **Bind → persist → publish.** `UpdateAsync()` fully validates and binds the document, then
  persists it to the store, *before* publishing; on `Invalid` or `VersionConflict`, the live rule is
  untouched. A store that refuses the write leaves nothing live either &mdash; there is no rollback
  step because none is needed.
- **Versions only move forward.** Reverting bumps the version rather than restoring an old number,
  so a version observed once is never observed again with different content. `RestoreAsync()`
  rolls back by *appending* a copy of an old document, not by rewriting history.
- **Coherent listings.** `Rules` and `FindEntry()` return `RuleSetEntry` records whose version and
  document come from a single snapshot &mdash; they are always mutually consistent, even while the
  rule is being replaced.
- **Shared binding semantics.** The `RuleSet` binds documents with the same registry and options as
  the rest of the serialization surface; construct it with the same `SpecRegistry` (and
  `RuleSerializerOptions`) used elsewhere so documents bind identically everywhere.

## Next Steps

- Declare the rules being registered with the [Rule Classes](Rules.md).
- See [Rule Durability](durability.md) for registering a store, the version log, quarantine, and
  rolling back.
- Serve `UpdateAsync()`/`RevertAsync()` over HTTP with [ASP.NET Core Integration](AspNetCore.md).
- See [DeserializeAsyncSpec()](DeserializeAsyncSpec.md) for how async rules bind their documents.
