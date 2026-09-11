# Store fixture — the teardown that reached into every other test's connections — Design

**Date:** 2026-09-09
**Ticket:** [#219](https://github.com/karlssberg/Motiv/issues/219)
**Plan:** [`2026-09-09-store-fixture-pool-clear.md`](../plans/2026-09-09-store-fixture-pool-clear.md)
**Source:** bundle spec
[2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
§6 step 5 — the EF reference store. The store is sound; the fixture its conformance suite runs on was
not.
**Lineage:** observed in the baseline run for [#211](https://github.com/karlssberg/Motiv/pull/221) →
filed as #219 → here.

## What shipped

- `SqliteStoreFixture`, `DerivedContextTests` and `Motiv.Studio.Tests.StoreSchemaTests` build their
  connection strings with `Pooling=False` and no longer call `SqliteConnection.ClearAllPools()`.
- `src/testing/PoolClearGate/PoolClearGateTests.cs` — one shared source file, `Compile Include`d into
  the three test assemblies that keep throwaway SQLite files on disk, each asserting about itself.
- `SqliteStoreFixtureTests.Should_delete_its_database_file_at_teardown` — the deterministic half.

## The symptom was not the one the ticket predicted

The ticket's structural claim was right: `ClearAllPools()` is process-global, each fixture owns a
private GUID-named file, and xunit runs test classes in parallel, so one class's teardown reaches into
every other live fixture's pool. What it got wrong is what that *does*.

It predicted a lost write — the failing assertion is `Store.Load().Count.ShouldBe(1)` followed by
`Version.ShouldBe(2)`, and a vanished write is the obvious reading. It is also the impossible one:
`Name` is the primary key, so `Count` can never be 2, and a `Count` failure means **0**.

The actual failure, once provoked, is neither:

```
System.ObjectDisposedException : Cannot access a disposed object.
Object name: 'SQLitePCL.sqlite3'.
   at Microsoft.Data.Sqlite.SqliteConnection.Open()
   at Microsoft.EntityFrameworkCore.Storage.RelationalConnection.OpenInternal(Boolean errorsExpected)
   ...
   at Motiv.Serialization.EntityFrameworkCore.EfPropositionStore.Load()
```

`Clear()` disposes the `SQLitePCL.sqlite3` handle underneath a connection another thread has **already
leased** from the pool and is about to open. Nothing is corrupted; the victim simply cannot open the
connection it was handed. The window between lease and `Open()` is nanoseconds when uncontended, which
is precisely why the failure was reachable only under full-solution load, and why the project's 0.6 s
solo / 8 s loaded split was the signature worth reading.

This matters beyond bookkeeping. A fix aimed at a lost write — a retry, a stronger transaction, a
serialising `[Collection]` — would have made the flake disappear without touching the cause, and would
have looked like it worked. That is the outcome the ticket wrote its "reproduce it first" instruction
to prevent, and it earned it.

## Reproducing it was the whole problem

Repetition does not reproduce this. Twelve runs of the project at `xUnit.MaxParallelThreads=24` under
six spinning CPU burners were green, which is unsurprising: the target is a few nanoseconds wide and
the suite hits teardown a few dozen times per run.

What reproduces it is **turning the race into a chosen interleaving** — one thread doing the store's
ordinary work, another calling `ClearAllPools()` in a tight loop:

```csharp
var clearer = Task.Run(() => { while (!cts.IsCancellationRequested) SqliteConnection.ClearAllPools(); });
for (var i = 1; i <= 500; i++)
{
    await store.WriteAsync(PropositionBatch.Save(Stored("a", i)), default);
    store.Load()[0].Version.ShouldBe(i);
}
```

That fails in about four seconds, first time, every time. It is not in the shipped suite — see below —
but it is what made the diagnosis knowable, and it is here so it can be re-run.

## The fix is a convention this repository had already written down

`SqliteDecisionFixture`, in `Motiv.Serialization.Sql.Tests`, carries this in its remarks:

> Pooling is off so the file can be deleted at teardown. The alternative,
> `SqliteConnection.ClearAllPools()`, is process-global — and xunit runs test classes in parallel, so
> one fixture's teardown would reach into every other test's connections.

The diagnosis was already correct, already written, and already applied — in one of four places. The
other three each carried the same comment as each other:

> Pooled connections keep a handle on the file, so the delete below fails without this.

which is true, and is the reason the wrong fix keeps getting chosen. Windows refuses to delete a file
with an open handle; the first thing that makes the delete succeed is a global clear; the delete then
works and the suite is green. Nothing about that loop ever surfaces the cost.

So the fix is `Pooling=False` in the connection string. A fixture releases its handles by never taking
them, and teardown has nothing left to do.

`SqliteConnection.ClearPool(connection)` — the ticket's other suggestion — would also have been correct,
being pool-scoped rather than process-global. It was not chosen because it keeps the clear, and
therefore keeps the shape that has now been got wrong three times, in exchange for nothing.

## Two tests, and the one that was cut

The fix is the absence of a call. Nothing behavioural keeps an absence absent, so the durable guard is
a **gate**, following `HigherOrderSeamGateTests`: written against the assembly's **metadata** rather
than its source, so a comment cannot satisfy it. It reads the `MemberReference` table for the name
`ClearAllPools` and requires it to be absent — a row survives even when the call site is unreachable,
so the gate asks whether the assembly mentions the name at all rather than whether the call runs.

That it reads metadata rather than text is not incidental: `StoreSchemaTests` still contains the string
`ClearAllPools`, in the doc comment explaining why it does not call it, and the gate is green.

The claim is necessarily per-assembly — metadata is readable one assembly at a time — so it holds in
three places. Rather than three copies, it is **one source file compiled into three assemblies** via
`Compile Include`, the mechanism `src/testing/StoreConformance` already uses here. That is what makes
the three provably the same gate rather than three that have drifted. The third assembly is
`Motiv.Serialization.Sql.Tests`, which established the convention and was the least-guarded place for
the mistake to come back.

Its message is total over `EntityHandle`'s kinds. A `MemberRef` parent may be a `TypeRef`, `TypeDef`,
`TypeSpec`, `ModuleRef` or `MethodDef` (ECMA-335 §22.25), and a cast assuming one of them would replace
the gate's designed message with an `InvalidCastException` — on the failure path, which is the only
path that ever runs it.

The second test, `Should_delete_its_database_file_at_teardown`, guards the *reason* the shortcut keeps
getting reached for: it asserts the fixture removes its own file after real connections have been
opened over it. Deterministic everywhere, but its teeth are on Windows, which refuses to delete a file
with an open handle. macOS and Linux unlink open files happily, so it cannot be red-proved on this
machine — that is stated in its remarks rather than left for a reader to discover.

### Why the race is not in the suite

The obvious third test — a store working normally while a sibling fixture is torn down beside it, the
suite's own shape — was written, and then cut. It does not detect reliably enough to be worth its cost:

| Shape | Pre-fix redness | Cost |
|---|---|---|
| Direct probe (`ClearAllPools` in a tight loop) | reliable | ~4 s |
| Sibling teardown, 500 iterations, alone | **0 of 5** | ~1 s |
| Sibling teardown, 500 iterations, whole EF suite in parallel | **1 of 3** | ~1 s |
| Sibling teardown, 3000 iterations, alone | **2 of 3** | ~8 s |

The faithful shape is weaker than the probe for a mundane reason: the sibling's teardown pays a
`File.Exists` syscall per pass, so it clears the pool orders of magnitude less often than a loop that
does nothing else. Raising iterations buys detection at eight seconds a run and still does not reach
certainty.

A test that is red two times in three, forever, is not a regression net — it is a coin flip that a
future reader will use to "verify" a revert and be told the wrong thing one time in three. The gate
detects the same reintroduction deterministically, in milliseconds, in all three assemblies. So the
probe lives in this document, where its measurements are stated, and the suite carries only assertions
that mean what they say.

## Cost

The EF test project dropped from **14 s to 0.6 s** locally. Not the goal, but not a coincidence either:
every teardown was tearing down every other fixture's pooled connections, and every subsequent
operation in every other class was paying to reopen the file.

## The review round

A `code-simplifier` pass found the gate's `(TypeReferenceHandle)member.Parent` cast — a real
`InvalidCastException` reachable only on the failure path, i.e. only when the gate was about to report
something, which is the one moment a gate may not fail differently than designed. It also found that
nothing asserted the race test's teardown loop had actually run, which is what prompted measuring that
test's detection rate, which is what got it cut. Both findings were about the *guards* rather than the
fix — the same pattern 4I's review round produced.

Its suggestion to consolidate the two same-assembly `ConnectionString` helpers was declined; it rated
the point marginal itself, and it would make `DerivedContextTests` depend on a fixture it otherwise
does not use.

## What is not claimed

- That this was the only cause of that one observed failure. It is *a* cause, proven to produce a
  failure in that suite under that pressure, and it is now gone. The original run's assertion message
  was never captured, so the two cannot be matched exactly.
- That `Should_delete_its_database_file_at_teardown` was verified to fail against the unfixed fixture.
  It cannot be, on macOS. Windows CI is where it has teeth, and where the claim rests.
