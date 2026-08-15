# Spec 2B — Multi-Instance Refresh — Design

**Date:** 2026-08-15
**Status:** Approved (design)
**Source:** Build step 4 of bundle spec [2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
resolving ticket [20](https://github.com/karlssberg/Motiv/issues/120). Follows
[#125](https://github.com/karlssberg/Motiv/pull/125) (Spec 2A — Rule Durability), which named this
slice "plan 2B". The EF Core reference store (ticket 16) remains plan 2C.

## Summary

A `PUT` on replica A is invisible to replica B until B restarts. Two replicas silently serve
different rules and nothing surfaces the divergence. This slice makes a live replica able to rebuild
its world from the store, gives it a cheap way to know when it needs to, and makes the resulting
skew detectable by clients rather than silent.

The capability decomposes into three parts that must land together:

1. **A generation** — a store-derived monotonic scalar, polled cheaply, that says "you are behind".
2. **A whole rebuild** — `RefreshAsync`, because a partial refresh is specifically unsafe and both
   `Load()` methods already refuse a second pass for exactly that reason.
3. **Snapshot isolation** — one swappable world, plus a pin, so a rebuild cannot be observed
   half-applied.

## Why now, and why all three

Refresh without isolation is worse than no refresh. Today a governed envelope publishing two rules
performs two independent `Volatile.Write`s, so a straddle window already exists — but it spans one
publish. A refresh rebuilds *every* rule at once, so the same defect arrives at maximum blast radius.
Ticket 20 is explicit about why that is intolerable: a cross-replica lag is a coherent set that *was*
published ("you got yesterday's policy"), whereas a cross-rule straddle is a combination that **never
existed anywhere** — an internally inconsistent justification tree, which for a product whose promise
is explainability is the one failure that cannot be tolerated.

## A correction to ticket 20's mechanism

Ticket 20 says cross-rule atomicity "requires every rule to read through one shared reference", with
each rule caching its index into the generation's array. That is necessary and it is adopted here —
but it is **not sufficient**, and the design records why so nobody re-derives it.

A shared reference fixes N *writes*: a swap becomes one store rather than one per rule. It does not
fix N *reads*. A caller evaluating `ruleA` and then `ruleB` performs two independent volatile reads,
and a swap can land between them. The observable outcome — A from the new world, B from the old — is
still a combination that never existed. The shared reference shrinks the window from *the duration of
the commit loop* to *the gap between two reads*; it does not close it.

Closing it needs a second half the ticket does not name: a **pin**, a scope in which a caller holds
one generation across several evaluations. Mechanism goes in `Motiv.Serialization`; lifecycle goes in
`Motiv.Serialization.AspNetCore` as per-request middleware — the same split ticket 20 chose for
`RefreshAsync` and its poller.

## Decisions (locked)

1. **`IPropositionStore` becomes symmetrical with `IRuleStore`** — it gains `LoadAsync` and
   `GetGenerationAsync`. `IRuleStore` already declares both and documents them as contract-only
   forward surface awaiting exactly this plan. Breaking, and free: neither package has ever shipped
   (ticket 06), and both existing implementations satisfy the additions in three lines each.
2. **The refresh unit is the whole world**, built off to the side and swapped in. Confirmed rather
   than revisited: a row that binds on pass one and quarantines on pass two has already written its
   overlay entry and graph edges, and the quarantine path clears neither.
3. **One immutable generation object** holds what five separately-mutated structures hold today: the
   `PropositionOverlay`, the `DependencyGraph`, the participant map, `PropositionSet`'s `_authored`
   map, and the rule states. `BindingScope` holds one `_current` field; the rest become projections.
4. **Every publish builds a successor and swaps once.** Not new architecture — `PropositionOverlay`'s
   own doc comment already specifies it ("cloned, bound into freely, and either swapped in whole or
   discarded") while `CommitClosure` violates it by calling `Overlay.Set(entry)` per commit against
   the live overlay. Ticket 20 identified this gap; this slice closes it.
5. **Rules read through the generation by slot.** A slot index is assigned once at `Attach` and is
   stable for the rule's lifetime, so evaluation is one volatile read plus an array index.
6. **The pin is ambient and disposable**, nested pins reuse the outer one, and the AspNetCore package
   pins per request. A library caller that needs cross-rule coherence opens one explicitly.
7. **A refresh that cannot bind a row aborts entirely** — it discards the successor, keeps serving
   the current generation, and reports why. It does *not* mirror startup's per-row quarantine.
8. **The rebuild is optimistic, not locked.** Store reads and binding run with no lock held; the swap
   validates under the gate that no publish landed meanwhile, and discards and retries if one did.
9. **The generation is a pair**, `(Rules, Propositions)`, one scalar per store. There is no single
   sequence to derive because the two stores are never written in the same transaction.
10. **The pair is the client-facing fencing token**, stamped on responses from the *live* generation
    and never from a fresh store read. `@motiv-rules/core` tracks the highest it has seen and exposes
    a detectable backwards-routing signal.
11. **The poller is opt-in and lives in the AspNetCore package.** `Motiv.Serialization` is a plain
    library and cannot own a lifecycle; the hosting package already registers singletons and maps
    endpoints.
12. **Telemetry belongs to spec 3.** This slice emits logs, a refresh report, and a health check —
    no spans, no metrics.

## Architecture

### `Motiv.Serialization`

**`StoreGeneration`** — a value pair `(long Rules, long Propositions)` with a "did either component
move" comparison. Component-wise, deliberately not a total order: detection only needs "did I go
backwards in any component", and inventing a total order over two independent sequences would be a
fiction.

**`ScopeGeneration`** (internal, immutable) — the world: overlay, graph, participants, authored map,
and a rule-state array indexed by slot. Constructed complete; never mutated after publication.

**`BindingScope`** — holds `private ScopeGeneration _current`, read via `Volatile.Read`. `Overlay`,
`Graph` and the participant map stop being fields and become projections of `_current`. `Locked` and
`LockedAsync` keep their present meaning; the two-tier exclusion contract established in 2A is
unchanged, and the outer gate is still acquired only at public entry points.

**Rule slots** — `RuleBase` gains an internal slot assigned at `Attach`. `Rule<TModel,TMetadata>` and
its three siblings drop `private State? _state`; `Snapshot()` becomes a read of the pinned-or-current
generation plus an array index. `Publication` and `RebindCommit` stop writing to the rule and write
into the successor generation instead.

**The pin** — a public disposable handle over one `ScopeGeneration`, ambient for the duration of the
`using` block via `AsyncLocal`. Nesting reuses the outer pin, so a pinned caller that calls into a
library that also pins observes one world, not two.

**`RefreshAsync(ct)`** — exposed on both `RuleSet` and `PropositionSet`, both routing to the shared
scope, so either call rebuilds the whole world. Two entry points because a library user may hold only
one of the two sets; one implementation, because the world is one thing. Returns a report saying
applied / unchanged / aborted, with the resulting generation and any per-row failures.

### `Motiv.Serialization.AspNetCore`

- **An opt-in `IHostedService` poller** — configurable interval, polls both stores' scalar
  generation, rebuilds only when it moves, honours the host's stopping token, and never throws out of
  its loop.
- **Per-request pin middleware**, registered by `MapMotivRules`, so the app's own surface is coherent
  without ceremony.
- **A generation response header**, stamped from the live generation.
- **An `IHealthCheck`** reporting last refresh outcome and current generation, so "this pod is
  serving yesterday's policy" is an operational fact rather than a log-grep.

### `@motiv-rules/core`

The client records the highest generation seen and surfaces a detectable signal when a response
carries a lower one in either component. Detection only — the retry policy is the caller's.

### `Motiv.RulesEngine.Sample`

`JsonFilePropositionStore` gains the two new members (`JsonFileRuleStore` already has them, added by
2A for this plan). The host opts into the poller, so `docker compose up` converges.

## Data flow

```
poll tick → GetGenerationAsync × 2                 scalar reads, no lock held
  ├─ unchanged → done                              the cheap path, every tick
  └─ moved ↓
LoadAsync × 2                                      I/O, no lock held
build successor generation off to the side         parse → order → bind → graph
  ├─ anything won't bind → abort, report,          keep serving current generation
  │                        retry next tick
  └─ ok ↓
outer gate + inner monitor
  ├─ a publish landed since the rebuild began →    discard, retry (bounded), else next tick
  └─ Volatile.Write(_current, successor)           one write; the whole world moves at once
```

Holding the outer gate across the store read would let a slow store block every publish, which is
precisely the hazard 2A's async write contract exists to avoid. The rebuild therefore runs unlocked
and validates its assumption at the swap — a compare-and-swap on the world, mirroring how the store's
`(Name, Version)` primary key guards a row.

`Load()`'s "call once" refusal stays exactly as written. Refresh replaces it; it does not relax it.

## Error handling

| Failure | Response |
|---|---|
| Store unreachable on a poll tick | Log, keep serving, retry next tick. A store outage must not take the host down. |
| `LoadAsync` throws mid-rebuild | The same: nothing has mutated, so the current generation is untouched. |
| A row will not bind | Abort the whole rebuild, report per-row reasons, keep serving. |
| A publish lands during a rebuild | Discard the successor and retry, bounded; then defer to the next tick. |
| Concurrent `RefreshAsync` | Serialised by the outer gate; the loser sees the generation already moved and no-ops. |
| Host shutdown | The poller honours the stopping token; an in-flight rebuild is abandoned, never half-applied. |

**Why abort rather than quarantine.** At startup there is no live world to preserve, so quarantining
a row and running the compiled default is the only non-fatal option. A refresh is different: the
replica is already serving something correct and approved. Falling back to a compiled default there
would silently regress live behaviour to something no approval authorised — which ticket 02 called
indefensible — so the refresh keeps what it has and says so. The cost is accepted and named: one bad
row stalls convergence on that replica until it is repaired or the replica is redeployed. That is the
rolling-deploy case (an older replica cannot bind a document naming a spec only the new build has),
and a replica that cannot serve the new world should not pretend to.

## Testing

TDD throughout, per the repo's convention. The load-bearing tests:

- **Atomicity, deterministically.** A rebuild is blocked mid-flight while a reader evaluates two
  rules; every read is asserted all-old or all-new, never mixed. The deliberate break must
  reproduce — remove the pin and the test must go red, or it proves nothing. Latches take timeouts,
  closing 2A's disclosure 2 rather than repeating it.
- **`GetGenerationAsync` is a scalar read.** A counting store asserts a poll tick touches the
  generation and nothing else. The interface calls this out as load-bearing, so it earns a test
  rather than a comment.
- **Two replicas, one store, in-process.** Two `RuleSet`/`PropositionSet` pairs over one shared
  store: A publishes, B refreshes, B's versions and documents match A's.
- **Abort keeps the live world.** A stored row referencing a spec this build lacks leaves every rule
  evaluating exactly what it evaluated before, with the reason reported.
- **Optimistic rebuild.** A publish landing between the store read and the swap discards the
  successor; the published change survives and is not overwritten by the stale rebuild.
- **Slot stability** across an `Add` after load, and **pin nesting**.
- **The TS guard** — the client's monotonic-read detection, in `rules-core`'s vitest suite.

Verification runs the full solution suite plus an all-TFM build. 2A found that CI runs a bare
`dotnet test` on `windows-latest` including `net472`, which local `-f net10.0` commands hide.

## Build sequence

1. `IPropositionStore`'s async additions, both implementations, and the sample's store.
2. `ScopeGeneration` + `BindingScope._current`; publishes build a successor and swap.
   **Behaviour-preserving** — the existing 5,604 tests are the oracle.
3. Rule slots; rules read through the generation; per-rule `_state` deleted. Also
   behaviour-preserving.
4. The pin, with nesting.
5. `RefreshAsync`, its report, abort semantics, and the optimistic rebuild.
6. AspNetCore: poller, pin middleware, generation header, health check.
7. The TS client guard.
8. Sample wiring, two-replica tests, docs.

Steps 2 and 3 are pure refactors that must land green before refresh exists. The ordering is
deliberate: it isolates the risky mechanical change from the new behaviour, so a regression has only
one place it can have come from.

## Explicitly out of scope

- **The EF Core reference store** (ticket 16) — plan 2C.
- **Cross-process write coordination** beyond what 2A already shipped: the `(Name, Version)` primary
  key is the compare-and-set, and it landed in 2A.
- **Telemetry** — spans, metrics and the decision log are spec 3.
- **Studio UI for staleness** — surfacing "you are viewing an older replica" to a human is spec 4.
  This slice gives the client the signal; it does not draw it.
- **A second-host e2e.** Convergence is proved in-process. A real two-host Playwright test is
  stronger evidence and meaningfully more infrastructure; it is deferred, not forgotten.
- **Tenancy.** The generation is per-scope; where the tenancy seam sits relative to `BindingScope`
  remains open fog on the map.

## Risks

1. **The generation refactor touches the path 2A just stabilised.** Governed envelopes, rebind
   closures and quarantine all run through `BindingScope`. Mitigated by sequencing: steps 2 and 3
   change no behaviour, so the whole existing suite is the oracle, and refresh is built only once
   they are green.
2. **The pin adds an ambient read to rule evaluation.** `Rule.Evaluate` is a per-decision call rather
   than Motiv's inner loop, so the cost is expected to be immaterial — but it is a hot-path claim, so
   it is measured rather than assumed, as ticket 20 itself insists.
3. **Abort-on-bad-row can stall a replica indefinitely.** Accepted deliberately, and the reason it is
   tolerable is the health check: a stalled replica is visible, so the operator can act.
4. **Two entry points to one `RefreshAsync`.** `RuleSet` and `PropositionSet` both expose it and both
   rebuild everything. Documented on both; the alternative — a third coordinator type beside the
   scope that already coordinates — is worse.
