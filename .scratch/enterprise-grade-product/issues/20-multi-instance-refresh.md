# Multi-instance: whole-rebuild refresh and change notification

Type: grilling
Status: resolved
Blocked by: —

## Question

Spun out of ticket 09's sub-question 4, which that ticket flagged as *"the real question hiding behind
all of the above — the difference between 'durable' and 'deployable'."* Durability is settled;
this is what makes it survive a second replica.

**Today there is no way to refresh a replica from the store short of restarting the process.**
`PropositionSet.Load()` refuses to run twice, by explicit design:

> *"Load has already been called on this PropositionSet. It reads the store once, at startup, before
> rules are added; it is not a refresh."*

And the reason is precise: a row that binds on the first pass and quarantines on the second has
already written its overlay entry and graph edges, and the quarantine path clears neither — *"leaving
the catalog reporting it broken while the evaluator still resolves the stale binding. A refresh would
have to be a whole rebuild, so refuse rather than half-do it."*

So a `PUT` on replica A is invisible to replica B until B restarts. Two replicas silently serve
different rules, and nothing surfaces the divergence.

### What makes this tractable

**The dependency graph is derived, not authoritative.** `StoredProposition` carries no references
field; edges are recomputed from documents (`DocumentReferences.From`, `ReferencesOf`). `Load` already
parses every document up front *"purely to order the binding"*, runs `OrderByDependency`, quarantines
cycles, then binds. So a replica re-reading the store reconstructs the correct graph by itself —
**change notification never needs to carry graph deltas or invalidation sets, only "something
changed."** That is the cheap end of cache coherence.

**And the read splits where the write could not** (ticket 09). A read has no atomicity relationship
with in-memory state, so there is nothing to interleave with and no persisted-but-not-committed
hazard:

```csharp
var rows = await store.LoadAsync(ct);   // pure I/O, no gate, touches nothing
Locked(() => SwapIn(BuildFrom(rows)));  // pure CPU: parse, order, bind, graph — then atomic swap
```

That is exactly the "whole rebuild" the doc comment says a refresh would have to be.

## The session must resolve

1. **What is the refresh unit?** A whole new `PropositionOverlay` + `DependencyGraph` + participant
   map built off to the side and swapped under the gate, or something finer? The doc comment argues
   for whole-rebuild; confirm and record why, since a partial refresh is the tempting cheap option
   that is specifically unsafe.
2. **What happens to live rules mid-swap?** Evaluation is lock-free (`Volatile.Read` over an
   immutable snapshot), so readers never block — but a rebuild produces *new* bound specs for every
   rule. Is the swap one reference assignment, or N? If N, evaluations spanning the swap could see a
   mixed generation. This is the correctness crux.
3. **What triggers it?** Polling a version/etag column, a database change feed, a message bus, or an
   explicit admin endpoint. Note the SDK cannot own a background poller without owning a lifecycle
   (`Motiv.Serialization` is a plain library) — so the trigger probably belongs in the app, with the
   SDK exposing only `RefreshAsync()`.
4. **Does the writing replica need to do anything differently?** If A publishes and B refreshes, A's
   version numbers must be authoritative — which they are, since `Version` is persisted (ticket 02).
   But two replicas could accept concurrent conflicting writes: the in-memory CAS protects within a
   process, and nothing protects across processes. **Does optimistic concurrency need to move into
   the store** (a conditional update on the version column) rather than living in memory?
   *This may be the sharpest question on the ticket* — it is where "durable" and "deployable" actually
   diverge.
5. **Is single-instance an acceptable documented constraint for v1?** A legitimate answer, if said out
   loud and enforced (a startup warning, or a lease). Silent divergence is the unacceptable option,
   not single-instance itself.
6. **`Load()`'s DI factory wall.** A refresh does not run inside `Func<IServiceProvider, T>`, so it
   can be async freely — but startup still cannot. Does startup keep a synchronous `Load()` while
   refresh is async, or does startup move to an `IHostedService` phase and give up
   `MapMotivRules`'s eager fail-fast on invalid defaults?

Feeds ticket 16 (the reference implementation must expose whatever the trigger needs) and the fog
patch "where the tenancy seam sits relative to `BindingScope`".

## Answer

**Snapshot isolation within a replica; eventual consistency across replicas, made detectable by a
store-derived monotonic token.**

### 1. Refresh unit — whole rebuild, atomically swapped

Confirmed as the doc comment argues. A partial refresh is specifically unsafe: a row that binds on
the first pass and quarantines on the second has already written its overlay entry and graph edges,
and the quarantine path clears neither. Build the replacement off to the side, swap it in.

### 2. Cross-rule and cross-proposition — snapshot isolation, and it is *necessary*

Not a preference. Without it the justification **tree** can be internally inconsistent, which for a
product whose promise is explainability is the one failure that cannot be tolerated: a coherent-looking
explanation of a rule combination that no approval ever authorised, logged against version numbers
never simultaneously in force.

Distinguish it carefully from the cross-replica choice — they are **different in kind**:

| | what a client sees | explicable? |
|---|---|---|
| cross-replica lag | a coherent set that *was* published, just not newest | yes — "you got yesterday's policy" |
| cross-rule straddle | a combination that **never existed** anywhere | no |

Staleness is time-travel; straddle is incoherence.

**Propositions get this nearly free.** `PropositionOverlay`'s doc comment already specifies the
mechanism — *"copy-construction is how a publish stays atomic without partial mutation… swapped in
whole or discarded"* — but `CommitClosure` does not follow it: it mutates the **live** overlay one
entry at a time (`Overlay.Set(entry)` per commit), using the prospective clone only for preparation.
Making the code match its stated contract is one reference swap, no per-lookup indirection.

**Rules are where the cost sits.** They are DI-injected instances holding their own `_state`, so a
handler holds the instance, not a lookup — N writes by construction. Cross-rule atomicity therefore
requires every rule to read through one shared reference. *Implementation note, not a decision:* a
dictionary lookup per evaluation would be the naive form, but each rule can cache its index into the
generation's array, reducing it to one volatile read plus an array index. Worth measuring before
assuming the hot-path cost is real.

### 3. Trigger — `RefreshAsync()` in the library, hosted poller in the hosting package

`Motiv.Serialization` exposes `RefreshAsync(ct)`. `Motiv.Serialization.AspNetCore` ships an **opt-in
`IHostedService`** that polls and calls it. The earlier objection — "a plain library cannot own a
lifecycle" — applies to `Motiv.Serialization` and **not** to the AspNetCore package, which already
registers singletons and maps endpoints. Two-sidedness puts mechanism in one and lifecycle in the
other, and adopters get convergence by configuration rather than by each writing the same poller.

**The poll reads a generation marker, not the store.** One scalar per replica per interval;
the expensive path — full `LoadAsync`, parse, `OrderByDependency`, bind, swap — runs only when it
moves. So the store contract grows a cheap `GetGenerationAsync()` beside `LoadAsync()`.

### 4. Startup — keeps its synchronous `Load()`

The store exposes both a synchronous `Load()` for startup and `LoadAsync()` for refresh. Startup
blocks one thread, once, before any traffic is served — harmless — and `MapMotivRules`'s eager
fail-fast on invalid defaults survives untouched. **The DI factory wall stops being a problem to solve
and becomes a constraint that costs nothing.**

### 5. The fencing token

A **store-derived** monotonic generation — not replica-local, which would be useless for skew
detection since two replicas' counters are unrelated numbers. Surfaced to clients so they get
**monotonic-read consistency**: a client can detect it has been routed backwards and retry rather than
silently accepting an older world. One store-side sequence serves both refresh triggering and skew
detection.

### 6. Cross-process write coordination — deferred, see ticket 21

The version CAS stays in memory, so two replicas can each accept a write both believe non-conflicting,
and the loser is lost silently with both authors seeing `200 OK`. The window is bounded by the refresh
interval. Deliberately accepted for now.

### The race hypothesised during this session does **not** exist

Recorded so nobody re-investigates. The overlay is read only under the publish lock
(`RuleSet.Update`, `PrepareRebind`); live evaluation reads a *bound* spec via `Volatile.Read`, never
the overlay. `/validate` and `/evaluate` did not read it either — for the unrelated reason that they
bound over the bare registry, which was a real defect, fixed separately in PR #93.
