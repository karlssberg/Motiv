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
    Task<PropositionWriteResult> WriteAsync(PropositionBatch batch, CancellationToken cancellationToken);
}

public sealed record PropositionBatch(
    IReadOnlyList<StoredProposition> Saves, IReadOnlyList<PropositionDeletion> Deletes);

public sealed record PropositionDeletion(string Name, int Version);

public sealed record StoredProposition(
    string Name, string ModelType, string DocumentJson, int Version, string? Description);
```

`ModelType` is carried explicitly because it is not in the document &mdash; a rule takes its model
from its C# class, and an authored proposition has no class.

A `PropositionBatch` is one store round trip: everything a single publish changes, applied all at
once or not at all. A name never appears in both `Saves` and `Deletes`, and never twice in either
&mdash; a publish either writes a row or removes it, once. `PropositionBatch.Save(proposition)` and
`PropositionBatch.Delete(name, version)` build the single-row shape most writes need.

Every entry carries a version, and the store compares it against the version it holds. That is what
makes a write a compare-and-set rather than a blind overwrite &mdash; see
[Concurrency](#concurrency) below.

## The Default

`InMemoryPropositionStore` is used when [`AddPropositions()`](AspNetCore.md) is called without one.
Propositions then live for the lifetime of the process, as rules do.

## Writing One

```csharp
public sealed class JsonFilePropositionStore(string path) : IPropositionStore
{
    public IReadOnlyList<StoredProposition> Load() => ReadAll();

    public Task<PropositionWriteResult> WriteAsync(
        PropositionBatch batch, CancellationToken cancellationToken)
    {
        var stored = ReadAll();

        // A save must be past the stored version; a deletion must equal it. One stale entry
        // refuses the whole batch, and nothing is written.
        if (FindConflict(batch, stored) is { } conflict)
            return Task.FromResult(conflict);

        // Every name the batch speaks for, whether to replace it or drop it.
        var superseded = new HashSet<string>(
            batch.Deletes.Select(deletion => deletion.Name), StringComparer.Ordinal);
        foreach (var proposition in batch.Saves)
            superseded.Add(proposition.Name);

        Write([.. stored.Where(existing => !superseded.Contains(existing.Name)), .. batch.Saves]);
        return Task.FromResult(PropositionWriteResult.Written);
    }

    // Note the asymmetry: ReadAll swallows everything a filesystem can do (a missing,
    // hand-edited or half-written file all read as "no propositions"), while Write lets
    // failures out. See the contract below.
}
```

Studio ships exactly this, with the `try`/`catch` and locking spelled out &mdash; see
`src/Motiv.Studio/JsonFilePropositionStore.cs`. Note that it *reports* what it
swallows: because `WriteAsync` rewrites the file from whatever `ReadAll` returned, an unreadable file
that went unmentioned would be overwritten at the next write rather than kept for repair.

## Contract

- **A store is a dumb sink for *semantic* legality.** It validates no document and enforces no
  proposition-level invariant; legality is decided by [`PropositionSet`](PropositionSet.md) before
  anything reaches here. It is not dumb about *structure*: `WriteAsync` must apply the whole batch or
  none of it, and must enforce the version compare-and-set described under
  [Concurrency](#concurrency).
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

## Concurrency

Every batch entry is a **compare-and-set against the version the store holds for that name** (`0` when
it holds no row), and one stale entry refuses the whole batch:

- a **save** claims a position no writer has claimed yet, so it lands only when its
  `StoredProposition.Version` is *strictly greater* than the stored one;
- a **deletion** names a position that must still be the writer's, so it lands only when its
  `PropositionDeletion.Version` *equals* the stored one.

A refusal is a value, not an exception: `PropositionWriteResult.Conflict(name, currentVersion)`
carries the version the store is actually at, so an editor can re-base rather than guess.
`PropositionSet` turns it into `PropositionUpdateOutcome.VersionConflict`, and the
[approval workflow](../governance/index.md) turns it into `ChangeRequestOutcome.VersionConflict`.

You do not have to re-derive that predicate in a store of your own. `PropositionBatch.FindConflict`
applies it to a whole batch — duplicate names within the batch included — given only a
`Func<string, int>` returning the version your store holds for a name (`0` when it holds none). Every
store shipped here calls it, which is what keeps their refusals identical:

```csharp
if (batch.FindConflict(name => _versions.TryGetValue(name, out var v) ? v : 0) is { } conflict)
    return conflict;
```

This is the *same* predicate `IRuleStore` enforces with its `(Name, Version)` primary key. Rule
versions are contiguous per name, so "this version is not already taken" and "this version is past the
head" are the same statement — the two stores enforce one predicate against two schemas.

`PropositionSet` also compares the caller's `expectedVersion` against live memory before it prepares a
write, and that check stays: it is what makes the prepared document bind against the writer's own
basis. But it is blind to every other replica, which is why *enforcement* lives in the store. Two
replicas that both read v1 and both publish v2 get one `Updated` and one `VersionConflict`; before
enforcement moved here, both got `Updated` and one edit vanished.

## The Asymmetry with `IRuleStore`

The two stores are twins, but they are not mirror images, and the difference is deliberate rather
than an oversight:

| | `IRuleStore` | `IPropositionStore` |
|---|---|---|
| History | An append-only version log, kept forever | One row per name, replaced in place |
| Rollback | `RestoreAsync` re-publishes a recorded version | None &mdash; a superseded document is gone |
| A second writer | `RuleAppendResult.Conflict`, carrying the version the store is actually at | `PropositionWriteResult.Conflict`, the same |
| Compare-and-set | `(Name, Version)` primary key | The row's version, checked on every entry |

What remains asymmetric is **history**, not concurrency. The rule log exists for rollback and audit
under an [approval gate](../governance/index.md); propositions offer neither, so a superseded
proposition document is not recoverable. Whether they should be is an open question, tracked
separately &mdash; it is a schema and retention decision, not a concurrency one.

## Next Steps

- See [`PropositionSet`](PropositionSet.md) for the write path that calls `WriteAsync`, and the
  `Load()` that reads this back at startup.
- Wire a store in with [`AddPropositions()`](AspNetCore.md).
- See [Rule Durability](../live-rules/durability.md) for the rule-side twin of this store, and how the
  two coordinate independently.
- See the [Runtime Propositions overview](index.md) for what
  [quarantine](index.md#startup-quarantine-dont-crash) does with a document that survives the round
  trip but no longer binds.
