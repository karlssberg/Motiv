# The store contract under the publish lock — sync or async?

Type: grilling
Status: resolved
Blocked by: 02

## Question

`IPropositionStore` is synchronous on purpose. Its own documentation:

> Deliberately narrow and synchronous, to match the synchronous publish path: implementations are
> called while the publish lock is held, so they must be quick.

A database-backed store is not quick and is not synchronous. This is a direct collision, and it is
the reason durable storage is more than "write an EF Core class".

**What is the contract for a store that talks to a database?**

Candidate resolutions, each with a real cost:

| Option | Cost |
|---|---|
| Make the interface async | Async-all-the-way through `PropositionSet.Publish` and `RuleSet.Update`; the publish lock becomes an async lock; `BindingScope`'s all-or-none rebind must survive it |
| Keep sync, persist outside the lock | Two-phase: validate and bind under the lock, persist after. Opens a window where memory and store disagree — and a crash in that window loses a published change |
| Keep sync, persist *before* publishing | Write-ahead: durable first, then bind and publish. A bind failure leaves a persisted row that was never live |
| Store is the source of truth | Invert it — the store transacts, and in-memory state is a cache invalidated on commit. Cleanest semantics, largest rewrite |

The session must resolve:

1. **What is the atomicity guarantee across a proposition edit that rebinds N rules?** Today it is
   all-or-none in memory. Across a database that is a transaction; across a database *and* memory it
   is a distributed commit unless one of them is authoritative.
2. **Does the answer differ for propositions and rules?** If 02 unified them, this is one decision.
3. **What does a store failure mean?** A publish that validated, bound, and then failed to persist —
   does it roll back the in-memory publish? Can it?
4. **Multi-instance.** Two app replicas each hold in-memory state. A `PUT` on replica A does not
   reach replica B. Is single-instance a documented constraint, or does this need change
   notification? **This may be the real question hiding behind all of the above** — and it is the
   difference between "durable" and "deployable".

Blocks: 15 (decision log), 16 (reference implementation).

## Inherited from ticket 02

- **The premise "a proposition edit that rebinds N rules" spans one store, not two.** A rebind
  re-binds a rule's existing document and never writes a rule row, so sub-question 2 above is
  narrower than charted: there is no distributed commit across the two stores to design.
- The established ordering is bind prospectively → check dependents → **persist** → mutate memory →
  commit. The "persist before publishing" option is therefore not hypothetical and does *not* leave
  a persisted row that was never live — binding has already succeeded by the time the store is called.
  The charting note claiming otherwise was wrong.
- The rule store's record is a head row `(Name, Version, DocumentJson?)`.

## Answer

**Two-tier exclusion: an outer `SemaphoreSlim` on `BindingScope` for await-safe serialisation of
operations, the existing inner Monitor left untouched for thread-safe mutation of data structures.
The authoring write path becomes async. Do it before 1.0.**

```csharp
public async Task<RuleUpdateResult> UpdateAsync(string name, string json, int expected, CancellationToken ct)
{
    await _publishGate.WaitAsync(ct);              // outer — async, serialises the whole sequence
    try
    {
        var prepared = Locked(() => Prepare(...)); // inner Monitor — unchanged, still reentrant
        await _store.SaveAsync(...);                // awaited, still inside the exclusion
        return Locked(() => Commit(prepared));      // inner Monitor — unchanged
    }
    finally { _publishGate.Release(); }
}
```

The gate belongs on **`BindingScope`**, not on `RuleSet` and `PropositionSet` separately — a
proposition publish cascades into rules, so two independent gates would let a rule update interleave
with a cascade.

### Why not simply replace the Monitor

A pure swap deadlocks immediately, and not in an edge case. `Monitor` stores *(owning thread,
recursion count)*; `SemaphoreSlim` stores *an integer* and so cannot know it is already held by the
caller. **All five `Enrol`/`Withdraw` call sites are already inside a `Locked` region:**
`RuleSet.cs:196` and `:201` (via `Track`, from both `Add` and `Mutate`), `PropositionSet.cs:269`
(withdraw), `:397` (via `LoadOne` from `Load`), `:586` (via `Publish`).

`RuleSet.Add → Scope.Locked → Track → Scope.Enrol → _gate.Wait()` waits on a permit the same thread
holds. The demo would hang at startup, before serving a request, with no exception and — because
`SemaphoreSlim` records no owner — no indication in a dump that the waiter is the holder.

Three further bugs the swap invites, recorded so nobody re-derives them:

1. **The obvious fix converts a hang into corruption.** Stripping the gate from `Enrol`/`Withdraw` so
   they assume it held works for all five sites — and makes them unsafe from anywhere else.
   `_participants` is a plain `Dictionary`, and `DependencyGraph` is explicitly *"not synchronized"*.
   One future unguarded call site gives concurrent `Dictionary` mutation, where a racing resize can
   corrupt the bucket chain and make a *later* lookup spin forever in `FindEntry` — a hang in a read
   path, far from the write that caused it.
2. **It trades a compiler-enforced property for a naming convention.** Today `Enrol` is correct from
   anywhere. After the swap it is correct only under the gate, and nothing in the type system
   distinguishes "acquires" from "assumes".
3. **Permit leak on exception.** `lock` releases on unwind; a hand-written gate needs `try`/`finally`.
   Not hypothetical — `RuleSet.Add` *deliberately* throws `RuleSerializationException` inside the
   locked region so an invalid default fails fast. Without `finally`, the first misconfigured rule
   permanently drains the gate and every later publish hangs.

Layering outside is immune to all four: the outer gate is acquired at public entry points only, none
of the five reentrant sites is an entry point, and the inner Monitor keeps its recursion counting.

### The invariant that must be documented

**The outer gate is acquired only at public entry points, and nothing inside may call one.**
`SemaphoreSlim` is not reentrant. Verified as holding today — `PrepareClosure → PrepareRebind → Bind`
never publishes — but nothing enforces it, so it needs stating in `BindingScope`'s remarks.

### Migration is all-or-nothing

Sync `Update` cannot sit beside async `UpdateAsync` through one gate: a sync caller's `_gate.Wait()`
blocks a pool thread for the whole duration of an async holder's round-trip. Seven public methods
change signature — `RuleSet.Add`/`Update`/`Revert`, `PropositionSet.Create`/`Update`/`Withdraw`/`Load`.

### Why async at all, given the benefit is small

Thread-time is *not* the argument and never was: the critical section is mostly CPU (each dependent's
`PrepareRebind` re-parses and re-binds a full document), so async frees the thread for ~5 ms of a
plausibly ~45 ms section. The two real reasons:

- **Cancellation.** `WaitAsync(ct)` is the answer to sub-question 3's hung-store problem, which the
  synchronous contract had no way to express — the bound otherwise lives in the driver's
  `CommandTimeout`, outside the code.
- **Timing.** The library is pre-1.0 and ticket 06 has not yet committed to a compatibility policy.
  Seven breaking signatures cost almost nothing now and a major version later.

### Scope of this decision

**The authoring path only.** Ticket 15's `IDecisionSink` is a different path — evaluation hot path,
machine-rate, never touches `BindingScope` or its gate. Every argument here (human-rate, already
serialised, evaluation lock-free) fails there. Do not inherit "synchronous" or "one gate" into it.

### Sub-questions 1–3

1. **Atomicity across a cascade** — unchanged, and narrower than charted (ticket 02: one store, not
   two). The outer gate spans prepare → persist → commit, so nothing interleaves.
2. **Rules vs propositions** — same contract, one shared gate.
3. **Store failure** — unchanged and already correct: everything fallible runs before anything that
   mutates, so a failed persist leaves nothing live behind it. Now also cancellable.

### Sub-question 4 is not answered here

Multi-instance spun out as ticket 20. `Load()` remains blocked by the DI factory wall
(`Func<IServiceProvider, T>` has no async form) — but the *read* splits cleanly in a way the write
path does not, and that split is the refresh mechanism. See ticket 20.

## Corrected by ticket 06

The rationale above says the seven breaking signatures are cheap because the library is "pre-1.0".
The real position is stronger: `Motiv.Serialization` has **never been published to NuGet** (only
`Motiv` has, at v8.0.0 over 22 versions). There are no adopters of these types at all, so the change
costs nothing rather than little. Ticket 06 also puts the rules stack on its own version train, so
breaking it no longer drags `Motiv`'s major.
