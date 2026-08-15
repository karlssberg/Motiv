---
title: Multi-Instance Refresh
description: Documentation for multi-instance refresh in Motiv — the generation a replica polls, RefreshAsync's whole-world rebuild, the per-request pin that keeps a decision coherent, and the Motiv-Generation header a client uses to detect stale routing.
---

Before this feature, a `PUT` on one replica was invisible to every other replica until it restarted:
two processes could silently serve different rules, and nothing said so. Multi-instance refresh closes
that gap for a host running more than one replica against the same durable
[rule](../live-rules/durability.md) and [proposition](../propositions/IPropositionStore.md) stores —
polling for another replica's publish, rebuilding this one's whole world when it moves, and telling a
caller which world a response came from.

Multi-instance refresh ships in the `Motiv.Serialization` package (`RefreshAsync`, `StoreGeneration`,
`DecisionSnapshot`); the poller, health check, and response header ship in
`Motiv.Serialization.AspNetCore` (`AddRefresh()`, the `Motiv-Generation` header).

## Why This Exists

[Rule Durability](../live-rules/durability.md) makes a publish survive a restart, but a *running*
replica never rereads the store on its own — `RuleSet.Load()` binds every stored head once, at startup,
and refuses to run again. Two replicas sharing the same store can therefore diverge for as long as they
both stay up: replica A publishes, replica B keeps serving what it loaded, and there is no restart to
force it to catch up. This feature adds the piece durability alone does not provide: a way for a
running replica to converge, and a way for a caller to tell when it hasn't.

## The Generation

`StoreGeneration` is where both stores stand, folded into one comparable value:

```csharp
public readonly record struct StoreGeneration(long Rules, long Propositions)
{
    public static StoreGeneration Zero { get; }
    public bool MovedFrom(StoreGeneration other);
    public bool IsBehind(StoreGeneration other);
    public string ToToken();                                   // "r7.p3"
    public static bool TryParseToken(string? token, out StoreGeneration generation);
}
```

It is a **pair, not a scalar**, because the rule store and the proposition store are never written in
the same transaction — there is no shared sequence to derive a single number from. That has a
consequence worth internalizing: comparison is component-wise, and deliberately not a total order.
"Am I behind?" (`IsBehind`) is answerable; "which of these two is newer?" is not — `r7.p3` and `r5.p9`
are each ahead of the other in one component, and inventing an answer would be a fiction a caller could
act on. `StoreGeneration` simply doesn't offer one.

Both `IRuleStore` and `IPropositionStore` expose a `GetGenerationAsync()` — a cheap scalar read that
moves whenever a write lands there, without rereading the whole store. That is what a poller checks on
every tick, and what a refresh compares against the world it is currently serving to decide whether
there is anything to rebuild.

## Available Types and Methods

| Page | Description |
|---|---|
| [RefreshAsync](refresh.md#refreshasync) | Rebuilds a replica's whole world from both stores and swaps it in as one reference write. |
| [AddRefresh()](refresh.md#polling-with-addrefresh) | The opt-in background poller and its interval. |
| [DecisionSnapshot / PinSnapshot()](refresh.md#the-pin) | Pins one world for the duration of a decision, so it can't straddle a concurrent refresh. |
| [The Motiv-Generation header](refresh.md#the-motiv-generation-header) | The fencing token stamped on every response, and what a client does with it. |
| [The Abort Policy](refresh.md#the-abort-policy) | What happens when a stored document can't be applied without regressing something already live. |

See [Refreshing a Replica](refresh.md) for the full detail on all five.

## Next Steps

- See [Rule Durability](../live-rules/durability.md) for the version log a refresh rebuilds from, and
  the quarantine semantics a refresh's abort policy builds on.
- See [Runtime Propositions](../propositions/index.md) for the proposition-side store a refresh also
  rebuilds from.
- See [ASP.NET Core Integration](../live-rules/AspNetCore.md) for `MapMotivRules()`, which pins a
  `DecisionSnapshot` per request automatically.
