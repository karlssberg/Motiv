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
    void Save(StoredProposition proposition);
    void Delete(string name);
}

public sealed record StoredProposition(
    string Name, string ModelType, string DocumentJson, int Version, string? Description);
```

`ModelType` is carried explicitly because it is not in the document &mdash; a rule takes its model
from its C# class, and an authored proposition has no class.

## The Default

`InMemoryPropositionStore` is used when [`AddPropositions()`](AspNetCore.md) is called without one.
Propositions then live for the lifetime of the process, as rules do.

## Writing One

```csharp
public sealed class JsonFilePropositionStore(string path) : IPropositionStore
{
    public IReadOnlyList<StoredProposition> Load() => ReadAll();

    public void Save(StoredProposition proposition) =>
        Write([.. ReadAll().Where(existing => existing.Name != proposition.Name), proposition]);

    public void Delete(string name) =>
        Write([.. ReadAll().Where(existing => existing.Name != name)]);

    // Note the asymmetry: ReadAll swallows everything a filesystem can do (a missing,
    // hand-edited or half-written file all read as "no propositions"), while Write lets
    // failures out. See the contract below.
}
```

The sample host ships exactly this, with the `try`/`catch` and locking spelled out &mdash; see
`src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs`.

## Contract

- **A store is a dumb sink.** It validates nothing and enforces no invariants; legality is decided by
  [`PropositionSet`](PropositionSet.md) before anything reaches here. In particular, `Save` must
  replace any existing record of the same name, and `Delete` must do nothing when the name is absent.
- **Synchronous, and quick.** Calls happen while the publish lock is held, matching `RuleSet`'s
  synchronous publish. A slow store slows every write in the application.
- **`Load` should never throw.** A store that cannot be read is treated as empty, and every
  proposition it would have carried simply resolves to its compiled spec (or does not resolve at
  all, and is reported as such). Throwing here turns an unreadable file into a failure to boot,
  which is precisely what [quarantine](index.md#startup-quarantine-dont-crash) exists to avoid.
- **`Save` and `Delete` must propagate failures.** The asymmetry with `Load` is deliberate: a write
  that silently failed would publish a proposition with no durable record of it, and the next restart
  would quietly lose the edit.
