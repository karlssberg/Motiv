---
title: IPropositionStore
---

`IPropositionStore` is where authored propositions live between restarts. It keeps durable storage
outside the library, exactly as transport and serialization already are: Motiv decides what is legal
and the store decides where the bytes go.

```csharp
public interface IPropositionStore
{
    IReadOnlyList<StoredProposition> Load();
    Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken);
}

public sealed record PropositionBatch(
    IReadOnlyList<StoredProposition> Saves, IReadOnlyList<string> Deletes);

public sealed record StoredProposition(
    string Name, string ModelType, string DocumentJson, int Version, string? Description);
```

`ModelType` is carried explicitly because it is not in the document &mdash; a rule takes its model
from its C# class, and an authored proposition has no class.

A `PropositionBatch` is one store round trip: everything a single publish changes, applied all at
once or not at all. A name never appears in both `Saves` and `Deletes` &mdash; a publish either
writes a row or removes it. `PropositionBatch.Save(proposition)` and `PropositionBatch.Delete(name)`
build the single-row shape most writes need.

## The Default

`InMemoryPropositionStore` is used when [`AddPropositions()`](AspNetCore.md) is called without one.
Propositions then live for the lifetime of the process, as rules do.

## Writing One

```csharp
public sealed class JsonFilePropositionStore(string path) : IPropositionStore
{
    public IReadOnlyList<StoredProposition> Load() => ReadAll();

    public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
    {
        // Every name the batch speaks for, whether to replace it or drop it.
        var superseded = new HashSet<string>(batch.Deletes, StringComparer.Ordinal);
        foreach (var proposition in batch.Saves)
            superseded.Add(proposition.Name);

        Write([.. ReadAll().Where(existing => !superseded.Contains(existing.Name)), .. batch.Saves]);
        return Task.CompletedTask;
    }

    // Note the asymmetry: ReadAll swallows everything a filesystem can do (a missing,
    // hand-edited or half-written file all read as "no propositions"), while Write lets
    // failures out. See the contract below.
}
```

The sample host ships exactly this, with the `try`/`catch` and locking spelled out &mdash; see
`src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs`. Note that it *reports* what it
swallows: because `WriteAsync` rewrites the file from whatever `ReadAll` returned, an unreadable file
that went unmentioned would be overwritten at the next write rather than kept for repair.

## Contract

- **A store is a dumb sink.** It validates nothing and enforces no invariants; legality is decided by
  [`PropositionSet`](PropositionSet.md) before anything reaches here. In particular, `WriteAsync` must
  apply the whole batch or none of it, replace any existing row of a saved name, and do nothing when a
  deleted name is absent.
- **`Load` is synchronous; `WriteAsync` is not.** `Load` runs once at startup, on the same
  synchronous surface `RuleSet.Load()` uses, because the DI factory wall that constructs both sets
  cannot await. `WriteAsync` runs under the publish lock but off that surface, with a
  `CancellationToken` &mdash; a store that stops responding can be escaped rather than waited on
  forever.
- **`Load` should never throw.** A store that cannot be read is treated as empty, and every
  proposition it would have carried simply resolves to its compiled spec (or does not resolve at
  all, and is reported as such). Throwing here turns an unreadable file into a failure to boot,
  which is precisely what [quarantine](index.md#startup-quarantine-dont-crash) exists to avoid.
- **`WriteAsync` must propagate failures.** The asymmetry with `Load` is deliberate: a write that
  silently failed would publish a proposition with no durable record of it, and the next restart
  would quietly lose the edit. `PropositionSet` persists before it mutates anything in memory, so a
  thrown exception here leaves nothing live &mdash; see
  [`PropositionSet`](PropositionSet.md#remarks).
- **Never written in the same transaction as [`IRuleStore`](../live-rules/durability.md).** The two
  stores are symmetrical and coordinate independently; no operation spans both.

## Next Steps

- See [`PropositionSet`](PropositionSet.md) for the write path that calls `WriteAsync`, and the
  `Load()` that reads this back at startup.
- Wire a store in with [`AddPropositions()`](AspNetCore.md).
- See [Rule Durability](../live-rules/durability.md) for the rule-side twin of this store, and how the
  two coordinate independently.
- See the [Runtime Propositions overview](index.md) for what
  [quarantine](index.md#startup-quarantine-dont-crash) does with a document that survives the round
  trip but no longer binds.
