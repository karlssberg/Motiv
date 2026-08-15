---
title: Refreshing a Replica
---

`RefreshAsync` rebuilds a replica's whole world from both stores and swaps it in as a single reference
write. `AddRefresh()` calls it on a timer; `DecisionSnapshot` pins the world a decision runs against;
the `Motiv-Generation` header carries that world's identity to the caller. This page covers all four,
plus what happens when a rebuild would regress something already live.

## RefreshAsync

```csharp
public Task<RefreshReport> RefreshAsync(CancellationToken cancellationToken = default);
```

Declared on both `RuleSet` and `PropositionSet`, and it doesn't matter which one you call: the two
share one coordinator, so `rules.RefreshAsync()` and `propositions.RefreshAsync()` are the same
operation, rebuilding both halves together.

```csharp
var report = await rules.RefreshAsync();

var outcome = report.Outcome;         // RefreshOutcome — Unchanged, Applied, Aborted, or Contended
var generation = report.Generation;   // StoreGeneration — where both stores stood in the world now being served
var regressions = report.Regressions; // why an Aborted refresh aborted
var quarantined = report.Quarantined; // what's still quarantined, carried forward — populated on Applied too
var converged = report.IsConverged;   // true for Unchanged/Applied; false for Aborted and Contended
```

### Why a whole rebuild, not a re-read

**A refresh rebuilds everything, not just the rows that moved.** That's not an implementation
shortcut — it's the reason this exists instead of `Load()` running a second time. `Load()` binds each
stored head over its compiled default *in place*: a row that binds writes an overlay entry and graph
edges into the live world as it goes. Running that twice on a row that binds on one pass and quarantines
on the next would leave those edges and that overlay entry behind — the quarantine path clears neither
— so the live world would end up with debris from a bind that no longer holds. `RefreshAsync` sidesteps
the problem entirely: it builds a whole successor world off to the side, from both stores' current
heads over the compiled defaults, and only then swaps it in as one atomic reference write. Nothing in
the world being served is ever mutated in place.

Because the successor is a full rebuild, propositions are always built before rules within it — the
same order `Add` and `Load` already establish — so a rule document that references an authored
proposition binds against a world where that proposition already exists.

### The four outcomes

| `RefreshOutcome` | Meaning |
|---|---|
| `Unchanged` | Neither store had moved since this replica's world was last built. The common case, every tick. |
| `Applied` | A new world was built and swapped in. |
| `Aborted` | The rebuild would have regressed a live, unquarantined binding to its compiled default, so it was discarded — see [The Abort Policy](#the-abort-policy). |
| `Contended` | A publish landed while the rebuild was being built, so the swap lost the race. The store already moved to at least as new a world; retry (or wait for the next poll tick). |

`RefreshReport.IsConverged` is `true` only for `Unchanged` and `Applied`. `Contended` is deliberately
excluded even though nothing is *wrong* — the replica is momentarily behind the moment it's asked, and
a caller polling for convergence should retry rather than read it as success.

## Polling With AddRefresh

```csharp
public MotivRulesBuilder AddRefresh(TimeSpan? interval = null);
```

`AddRefresh()` registers an `IHostedService` that polls `GetGenerationAsync()` on both stores and calls
`RefreshAsync()` when either has moved:

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddRuleStore(new JsonFileRuleStore("rules.json"))
    .AddPropositions(new JsonFilePropositionStore("propositions.json"))
    .AddRule<CanCheckoutRule>()
    // Opt-in: a single-replica host doesn't need it. Polls, and rebuilds this replica whenever
    // another one has published — without this, only a restart would pick the change up.
    .AddRefresh();
```

It's opt-in because a single-replica host has nothing to converge with, and starting a timer nobody
asked for isn't a default worth having. Call it once — a second call throws, since DI registration is
last-wins and a second `AddRefresh()` would silently start a second poll loop against the same
singleton rather than layering onto the first.

### Choosing an interval

Pass an interval explicitly to override the default: `.AddRefresh(TimeSpan.FromSeconds(2))` in place of
the parameterless call above. The default is five seconds. The interval is the bound on how long two replicas can disagree — shorter
means fresher, at the cost of one extra scalar read per store per replica per tick. Since
`GetGenerationAsync()` is a cheap read rather than a full store load, a short interval is inexpensive;
pick a value proportional to how long your organization is comfortable with two replicas serving
different answers, not to the size of the store.

The poll loop never throws out of `ExecuteAsync`. A store outage, a cancelled rebuild, or a rebuild
that lost its swap are all logged and retried on the next tick rather than taking the host down — a
background poller failing once is not a reason to turn a stale replica into a missing one.

### The health check

`AddRefresh()` also registers an `IHealthCheck` named `"motiv-refresh"`, reporting the last tick's
outcome. Map it alongside your other checks:

```csharp
app.MapHealthChecks("/healthz");
```

Before the first tick it reports `Healthy` ("Motiv has not polled yet.") rather than degraded — a
freshly started replica is correctly serving its loaded defaults, and reporting it degraded before the
poller has even run once would be a false alarm on every cold start. See
[The Abort Policy](#the-abort-policy) for what it reports once refreshes are actually running.

## The Pin

A decision that evaluates more than one rule performs more than one read of the live world. Without
something holding that world still, a swap could land between those reads — one rule resolved from the
world before the swap, the other from the world after it. That combination never existed anywhere: not
staleness, which is explicable ("you got yesterday's policy"), but incoherence, which is not.
`DecisionSnapshot` closes that gap:

```csharp
// sealed class DecisionSnapshot : IDisposable
StoreGeneration Generation { get; }   // what a response stamps as its fencing token
void Dispose();                       // releases the pin

// on both RuleSet and PropositionSet
DecisionSnapshot PinSnapshot();
```

Every rule evaluated while a `DecisionSnapshot` is open resolves against the generation it pinned, no
matter how many reads the decision performs. The pin follows the async flow, so it survives `await`,
and nesting is safe — an inner pin reuses the outer one, and disposing it doesn't end the decision.

**`MapMotivRules` opens one per request automatically.** A handler that evaluates several rules inside
one HTTP request already gets a coherent world with no extra code — including when one of those rules
is async, since the pin follows the async flow and survives the `await`:

```csharp
app.MapPost("/api/checkout", async (
    CanCheckoutRule canCheckout, FraudScreeningRule fraudScreening,
    Customer customer, CancellationToken cancellationToken) =>
{
    // Both evaluations, whatever the request, see one pinned world — no straddle possible,
    // and no different because the second read happens after an await.
    var eligible = canCheckout.Evaluate(customer);
    var screened = await fraudScreening.EvaluateAsync(customer, cancellationToken);
    return Results.Json(new { approved = eligible.Satisfied && screened.Satisfied });
});
```

**An in-process caller — outside a request, or spanning more than one — opens its own** with
`PinSnapshot()`:

```csharp
async Task RecordCheckoutDecisionAsync(Customer customer, CancellationToken cancellationToken)
{
    using var snapshot = rules.PinSnapshot();

    var eligible = canCheckout.Evaluate(customer);
    var screened = await fraudScreening.EvaluateAsync(customer, cancellationToken); // pin survives the await

    RecordDecision(snapshot.Generation, eligible, screened);
}
```

Without a pin, two independent reads inside a background job, a message handler, or any code path that
isn't behind `MapMotivRules` can straddle a refresh the same way an HTTP handler could. If a call site
evaluates more than one rule (or proposition) and needs them to agree on which world they came from,
pin first.

## The Motiv-Generation Header

`MotivRulesEndpoints.GenerationHeader` (`"Motiv-Generation"`) is stamped on every response the Motiv
endpoints themselves produce, carrying the pinned `StoreGeneration`'s wire form (`ToToken()`, e.g.
`"r7.p3"`):

```
Motiv-Generation: r7.p3
```

A client behind a load balancer has no other way to tell it was routed to a replica that hasn't caught
up — the response otherwise looks perfectly well-formed. Comparing the header against the highest
generation it has already seen is how it finds out:

```ts
import { parseGeneration } from '@motiv-rules/core';

function logGeneration(response: Response) {
  const observed = parseGeneration(response.headers.get('motiv-generation'));
  console.log(observed); // { rules: 7, propositions: 3 }
}
```

`@motiv-rules/core`'s `RulesApiClient` does this tracking for you: it keeps the highest generation any
response has carried and calls `onStaleGeneration(observed, highest)` when a later response reports one
that's behind in *either* component — the same non-total-order rule `StoreGeneration.IsBehind` encodes
server-side:

```ts
import { RulesApiClient } from '@motiv-rules/core';

const client = new RulesApiClient({
  baseUrl: '/api/rules',
  onStaleGeneration: (observed, highest) =>
    console.warn(`served ${JSON.stringify(observed)}, expected at least ${JSON.stringify(highest)}`),
});
```

Two things worth stating honestly about the header:

- **A `401` carries no header.** `RequireAuthorization()`'s refusal for an unauthenticated caller
  happens in ASP.NET middleware, ahead of routing — before the filter that stamps the header ever runs.
  This is the right outcome, not a gap: a caller with no credentials has no generation worth comparing
  against anyway. A document *this endpoint* rejects — a `404` unknown rule, a `400` invalid document —
  is different; that refusal happens inside the pipeline the filter wraps, so it's stamped like any
  other response.
- **A successful `PUT`'s header names the *pre-write* generation**, not the one the write just
  produced. The pin is taken at request start, before the publish commits, so the header reports the
  world the request was read against while the response body reports the version the write produced —
  two true facts about two different moments, not a bug. That direction is deliberately the safe one:
  understating what a response carries can only make a client miss a genuine improvement it just
  received (a false negative on skew detection); overstating would have it record a generation it was
  never actually served, so the very next correct response would look like a regression and raise a
  false alarm. The one real cost: a writer can't use its own write's response header to tell whether a
  *later* read is stale — that comparison needs the header from the later read, not this one.

## The Abort Policy

This is the most surprising behaviour, so it's stated plainly: **a refresh aborts when applying it
would quarantine something that is not quarantined today** — that is, when a stored document that
currently binds and is live would fail to bind in the world being built, dropping a rule or proposition
back to its compiled default. When that happens:

- The rebuild is discarded. The replica keeps serving exactly what it was serving before the tick.
- It does **not** converge on its own — not on the next tick, not ever — until either the store is
  repaired (the offending document is fixed or reverted) or this build is redeployed with a compiled
  default the document can bind against again.
- `RefreshReport.Regressions` names every row that would have regressed, and `RefreshReport.Quarantined`
  carries forward anything that was *already* quarantined in the world now still being served — the
  same pass finds both, and an already-quarantined row is a plausible cause of one that just regressed.

The `"motiv-refresh"` health check reports this as `Degraded`, not `Unhealthy`, naming what blocked it:

```
Motiv is stuck on generation r7.p3: can-checkout would regress a live binding. 1 document(s) still quarantined: fraud-screening.
```

**Degraded rather than Unhealthy is deliberate.** The replica is serving a coherent, fully approved
world correctly — it's just not the newest one. Marking it `Unhealthy` would pull it out of load
balancer rotation, which turns a stale pod into a *missing* pod: strictly worse, since the traffic that
pod would have served correctly now goes nowhere at all (or piles onto whichever replica is left).

**Why abort rather than quarantine the regressing row and move on.** The obvious alternative — carry
on, quarantining whatever fails to bind — reads as the safe choice and isn't: a single hand-edited row
that never bound in the first place would then abort convergence for *everything else* on every tick
forever, since the same row keeps failing to bind on every rebuild attempt. Aborting the whole refresh
instead means one bad row costs exactly its own row today, and the operator sees one clear reason
rather than a cascade.

**A row that is already quarantined is carried forward and blocks nothing.** It has no live binding to
protect — nobody approved its current compiled-default behaviour as *the* behaviour, they're already
aware it's degraded — so it costs nothing to carry across a refresh. The alternative, refusing to
converge until every historically bad row is fixed, would stall a replica permanently on one
hand-edited document nobody has gotten around to repairing.

**`Contended` is not a failure and does not trigger this.** A publish landing mid-rebuild and winning
the swap means the replica is already on a world at least as new as the one being built; the next tick
proceeds normally, and the health check reports it the same as `Applied`/`Unchanged`.

## Next Steps

- See [Multi-Instance Refresh](index.md) for what a generation is and why it's a pair.
- See [Rule Durability](../live-rules/durability.md) for the version log and the startup quarantine
  this abort policy protects against regressing.
- See [Runtime Propositions](../propositions/index.md) for the proposition-side quarantine semantics a
  refresh carries forward the same way.
- See [ASP.NET Core Integration](../live-rules/AspNetCore.md) for `MapMotivRules()`, `AddMotivRules()`,
  and the rest of the hosting surface `AddRefresh()` builds on.
