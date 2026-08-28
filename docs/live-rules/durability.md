---
title: Rule Durability
---

Rules live in memory by default, evaluated only by the process that registered them. Registering a
store with `AddRuleStore()` makes every publish durable: `RuleSet` writes to an append-only version
log before anything goes live, so a restart &mdash; or a second replica reading the same store &mdash;
sees exactly what was published, not just what happened to still be running.

```csharp
MotivRulesBuilder AddRuleStore(IRuleStore? store = null, bool failFastOnQuarantine = true);

public interface IRuleStore
{
    IReadOnlyList<StoredRule> Load();
    Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken);
    Task<long> GetGenerationAsync(CancellationToken cancellationToken);
    Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken cancellationToken);
}
```

## Registering a Store

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddRuleStore(new JsonFileRuleStore(rulesPath))
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>();
```

Without `AddRuleStore()`, rules live for the process lifetime only, exactly as before this feature
existed &mdash; `InMemoryRuleStore` backs every `RuleSet` that isn't given a store explicitly, and
is a real store, not a stub: it enforces the same `(Name, Version)` primary key a database store
would. `JsonFileRuleStore` above is Studio's own `IRuleStore`, not a library type; see
`src/Motiv.Studio/JsonFileRuleStore.cs`.

`AddMotivRules()` loads the store when the `RuleSet` is first resolved, applying every stored head
over each rule's compiled default &mdash; after every `AddRule<TRule>()` has enrolled its rule, and
after `AddPropositions()` has loaded, since a stored rule document may reference an authored
proposition.

## The Version Log

Every publish appends one immutable `StoredRuleVersion` row:

```csharp
public sealed record StoredRuleVersion(
    string Name, int Version, string? DocumentJson, string Author, DateTimeOffset TimestampUtc,
    string? ChangeNote, string? ApprovalRef, string? BuildId);
```

- **`(Name, Version)` is the primary key** &mdash; the whole compare-and-set. Two replicas that both
  compute "next = 6" race on the insert, and the key lets exactly one win; see
  [Concurrent Writers](#concurrent-writers).
- **`DocumentJson` is nullable, and null is meaningful.** Null means "reverted to the compiled
  default at this version" &mdash; never an absent row. Losing that distinction would make a revert
  indistinguishable from a rule that was never authored.
- **`Author`, `ChangeNote`, `ApprovalRef` and `BuildId` are the provenance** a caller supplies via
  `RuleChangeProvenance` &mdash; who published, why, which change request it discharged (when
  governed), and which build was running. `BuildId` falls back to `BuildIdentity.Current` (the entry
  assembly's informational version) when left null, since a compiled default has no document to
  fingerprint.
- **The log is append-only and kept forever.** There is no pruning path and no rewrite: a rollback
  does not edit history, it appends a new row &mdash; see [Rolling Back](#rolling-back).

`StoredRule(Name, Version, DocumentJson)` is the **head projection** &mdash; the highest-versioned
row for a name, reduced to what a load needs. `RuleSet.Load()` reads every head once at startup;
`HistoryAsync()` reads a name's whole log, oldest first &mdash; see [Reading History](#reading-history).

`GetGenerationAsync()` is a scalar read that moves whenever any write lands: a fencing token a
future multi-instance refresh path can poll without re-reading the whole store. Nothing in this
release polls it yet &mdash; see [Deliberately Out of Scope](#deliberately-out-of-scope).

## Concurrent Writers

Two replicas of the same application, both holding the same `RuleSet` state, can both attempt to
publish against the same `expectedVersion` at once. Exactly one wins:

```csharp
var results = await Task.WhenAll(
    replicaA.UpdateAsync("can-checkout", documentA, expectedVersion: 1, new RuleChangeProvenance("alice")),
    replicaB.UpdateAsync("can-checkout", documentB, expectedVersion: 1, new RuleChangeProvenance("bob")));

// One Updated, one VersionConflict — decided by the store's primary key, not a lock
```

The store's `(Name, Version)` primary key, not an in-process lock, is what decides: the losing
`AppendAsync` call reports `RuleAppendResult.Conflict(name, currentVersion)`, which `RuleSet` turns
into `RuleUpdateOutcome.VersionConflict` carrying the version to re-base onto &mdash; and, because the
refusal is checked before anything mutates, the loser's own in-memory rule is left untouched too, not
just the store. The version log ends up with exactly one new row: one publish, one rejected attempt,
never two published rows for the same version.

## Quarantine and the Fail-Fast Switch

`RuleSet.Load()` applies each stored head over the rule's compiled default. A document that no
longer binds &mdash; a redeploy renamed a spec a stored rule referenced, say &mdash; is
**quarantined**, not fatal:

- the rule stays on its compiled default, so it can still evaluate;
- its stored version is preserved, so a repair publishes against the version the store actually holds;
- the reason is recorded on `RuleSetEntry.Quarantine` and on the `RuleLoadReport` that `Load()` returns.

```csharp
var report = rules.Load();

report.Quarantined;      // IReadOnlyList<QuarantinedRule> — name, version, and why
report.Orphaned;         // stored names no rule is registered under — not a fault; history outlives code
report.HasQuarantine;
report.ThrowIfQuarantined(); // throws RuleSerializationException when anything was quarantined
```

`AddRuleStore(store, failFastOnQuarantine: true)` &mdash; the default &mdash; calls
`ThrowIfQuarantined()` for you, so startup refuses to boot into a rule silently running unapproved
behaviour: a quarantined rule is running its compiled default, which is *not what was published*,
and under an approval gate booting quietly into that is the worse failure. Set
`failFastOnQuarantine: false` to boot anyway and read `Quarantine` off the catalog instead &mdash;
the stored document is retained for repair either way, so this is a boot-vs-serve trade, not a
data-loss risk.

## Reading History

```csharp
var history = await rules.HistoryAsync("can-checkout");

foreach (var version in history) // oldest first
    Console.WriteLine($"v{version.Version} by {version.Author} at {version.TimestampUtc}: {version.ChangeNote}");
```

Empty when the name has never been published. Every row is returned, including ones a later publish
superseded &mdash; this is the audit trail, not the current state; read `FindEntry("can-checkout")`
for that.

## Rolling Back

```csharp
var restored = await rules.RestoreAsync(
    "can-checkout", targetVersion: 5, expectedVersion: 9, new RuleChangeProvenance("bob", "rollback"));
```

`RestoreAsync()` looks up `targetVersion` in the log and republishes its document through the same
`UpdateAsync()`/`RevertAsync()` path &mdash; restoring v5 while the rule is at v9 *appends* v10
carrying v5's document, rather than rewriting history back to v5. The new row is itself the record
that a rollback happened. `targetVersion` still has to exist in the log &mdash; restoring an unknown
version returns `RuleUpdateOutcome.NotFound` &mdash; and `expectedVersion` still has to match the
rule's current version, with the same `VersionConflict` outcome as any other write.

## Deliberately Out of Scope

This release does not ship a background poller that calls `GetGenerationAsync()`/`LoadAsync()` on a
timer to refresh one replica from another's write, or the client-facing fencing token that would
ride on it &mdash; both are planned separately, as is an EF Core store (with migrations and an
importer that round-trips a file-backed store into it). Until then, `IRuleStore` implementations are
supplied by the host (see `JsonFileRuleStore` in Studio), and a replica only reads the store at
its own startup.

## Remarks

- **Never written in the same transaction as [`IPropositionStore`](../propositions/IPropositionStore.md).**
  The two stores are symmetrical and coordinate independently; no write spans both, even inside a
  governed envelope that publishes a rule and a proposition together.
- **A store is a dumb sink.** It validates no document and enforces no rule-level invariant &mdash;
  `RuleSet` decides all of that before anything reaches the store. It is not, however, dumb about
  *structure*: the `(Name, Version)` primary key is load-bearing.
- **Bind → persist → publish, in that order.** `RuleSet` binds and validates under its inner write
  monitor, releases the monitor around the awaited store call, then re-takes it to publish &mdash; see
  [`RuleSet`'s remarks](RuleSet.md#remarks). The outer write gate (a separate, coarser lock —
  `BindingScope`'s semaphore) stays held for the whole operation regardless; it is the monitor, not the
  gate, that is released around the store call. A slow store slows writers, not evaluators; a store
  that throws or refuses leaves nothing live.

## Next Steps

- See [`RuleSet`](RuleSet.md) for `UpdateAsync()`/`RevertAsync()` and the outcome contract this
  durability layer sits underneath.
- See [`IPropositionStore`](../propositions/IPropositionStore.md) for the proposition-side twin of
  `IRuleStore`.
- See [ASP.NET Core Integration](AspNetCore.md) for `AddRuleStore()` in context alongside
  `AddMotivRules()` and `AddPropositions()`.
