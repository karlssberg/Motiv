---
title: Rules-Stack Telemetry
description: The motiv.rules.* signals emitted by Motiv.Serialization — authoring, storage, replication, break-glass and the decision log — plus the motiv.rules.evaluate span that carries a rule's name and version, and how the PII posture is set once for both.
---

Core `Motiv` reports what an *evaluation* decided. `Motiv.Serialization` reports what the *rules stack around it*
is doing: which documents will not bind, which publishes were refused, how far behind the store this replica is,
how full the decision queue is, and whether break-glass is bypassing the approval gate.

These live on their own activity source and meter, both named `Motiv.Serialization`. That separation is
deliberate: core `Motiv` is published and its signal names are frozen as contract, while the rules stack is 0.x
and still churning. Sharing a source would tie one's stability promise to the other's.

## Enabling

Subscribe to both if you want both. Use the constants rather than string literals &mdash; a mistyped name is not
an error, it is silence:

```csharp
using Motiv.Diagnostics;
using Motiv.Serialization;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(MotivTelemetry.SourceName)          // "Motiv"          — evaluations
        .AddSource(MotivRulesTelemetry.SourceName)     // "Motiv.Serialization" — rules
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(MotivTelemetry.MeterName)
        .AddMeter(MotivRulesTelemetry.MeterName)
        .AddOtlpExporter());
```

As with core, nothing is emitted until something subscribes.

## Instruments

Everything is tagged `motiv.rules.kind` (`rule` or `proposition`) wherever both halves of the catalog can produce
it. The two stores are never written in the same transaction, so they are always counted apart.

### Authoring

| Instrument | Type | Tags | What it tells you |
|---|---|---|---|
| `motiv.rules.bind_failures` | `Counter<long>` | `motiv.rules.kind`, `motiv.rules.phase` | A stored document would not bind |
| `motiv.rules.publish_conflicts` | `Counter<long>` | `motiv.rules.kind` | A publish was refused because the head had already moved &mdash; the 409s |
| `motiv.rules.store.duration` | `Histogram<double>` (`s`) | `motiv.rules.kind`, `motiv.rules.operation` | How long a store call took |

`motiv.rules.phase` is `load` (the one-shot read at startup), `refresh` (a poller's rebuild) or `publish` (an
authoring write that was refused). `motiv.rules.operation` is `load`, `append` or `generation`.

Two things are worth knowing about `bind_failures`:

- **It counts documents, not errors.** One row that fails validation five ways is one failure.
- **A row still broken on the next rebuild is counted again.** That is what makes
  `rate(motiv.rules.bind_failures{phase="refresh"}) > 0` a usable alert: a row counted once and never again would
  leave a wedged replica looking healthy the moment the first tick scrolled off the dashboard. A tick that finds
  neither store moved rebuilds nothing and so reports nothing &mdash; the instrument is a function of the catalog,
  not of your poll interval.

### Replication

| Instrument | Type | Tags | What it tells you |
|---|---|---|---|
| `motiv.rules.catalog.size` | `ObservableUpDownCounter<long>` | `motiv.rules.kind` | How many rules and propositions this replica has |
| `motiv.rules.generation` | `ObservableUpDownCounter<long>` | `motiv.rules.store` | The store generation this replica is *serving* |
| `motiv.rules.replica_lag` | `ObservableUpDownCounter<long>` | `motiv.rules.store` | How far behind the store it was at its last refresh; `0` is converged |
| `motiv.rules.refreshes` | `Counter<long>` | `motiv.rules.outcome` | Refresh attempts, by what each one did |
| `motiv.rules.rebuild.duration` | `Histogram<double>` (`s`) | `motiv.rules.outcome` | How long rebuilding a world took |

`motiv.rules.store` is `rules` or `propositions`. `motiv.rules.outcome` is `unchanged`, `applied`, `aborted` or
`contended`.

`replica_lag` is measured against the generation the last refresh actually read, not against a fresh store read: a
gauge callback fires on your exporter's schedule and must not issue a database round trip per collection. So it
answers "how far behind was I when I last looked" &mdash; which means a replica whose poller has *stopped* reports
its last known lag rather than a comforting zero.

`rebuild.duration` is recorded only when a rebuild actually happened. Timing an `unchanged` tick would report "no
rebuild" as "a very fast rebuild", which is the same number an operator reads as a healthy rebuild rate.

### The decision log

| Instrument | Type | What it tells you |
|---|---|---|
| `motiv.rules.decisions.dropped` | `ObservableCounter<long>` | Audited decisions shed under the `Drop` posture |
| `motiv.rules.decision_queue.depth` | `ObservableUpDownCounter<long>` | Records waiting for the sink |
| `motiv.rules.decision_batches.failed` | `ObservableCounter<long>` | Batches the sink refused |

These are read straight off the live `DecisionLog` rather than pushed from a call site. `decisions.dropped` reads
its `DroppedCount` &mdash; the same number every `DecisionGap` marker is written from &mdash; so the counter and
the markers cannot drift apart.

`decision_queue.depth` is the one number here that can fall, and the one worth alerting on before it hurts: depth
approaching `DecisionLogOptions.QueueCapacity` means backpressure is about to apply, and under the default
`FailClosed` posture that means audited evaluations are about to start throwing.

A rising `decision_batches.failed` is a sink that needs attention. The writer loop survives a throwing sink by
design &mdash; a log that silently stopped logging is the failure the decision log exists to prevent &mdash; so
nothing else will tell you.

### Break-glass

| Instrument | Type | Tags | What it tells you |
|---|---|---|---|
| `motiv.rules.break_glass.active` | `ObservableUpDownCounter<long>` | &mdash; | `1` while break-glass is bypassing the gate, `0` otherwise |
| `motiv.rules.publishes_under_break_glass` | `Counter<long>` | `motiv.rules.kind` | Artefacts published while it was |

Every `BreakGlass` registers, `BreakGlass.Off` included &mdash; so an ordinary host reads `0` rather than
reporting nothing, and "no series" stops being ambiguous between the flag being off and the replica's meter
having stopped answering.

`publishes_under_break_glass` counts one per *artefact*, not per change request: an envelope carrying a
proposition and the rule that references it changed two things, and an operator reviewing a break-glass window
is trying to establish exactly that number. Only publishes that actually landed are counted &mdash; break-glass
says the ceremony was skipped, not that anything went live, and a stale base version still fails its own
compare-and-set.

## Tracing: which rule, at which version

Every named-rule evaluation opens a `motiv.rules.evaluate` span:

| Tag | Description |
|---|---|
| `motiv.rules.name` | The rule's name |
| `motiv.rules.version` | The version of the binding that was evaluated |

Core's `motiv.evaluate` span lands *inside* it. That containment is the point: a `SpecBase` has no version, and
giving it one to satisfy an operator's query would push a rules-stack concern into the published engine. So the
rules layer parents the evaluation instead, and an operator holding a publish can find the evaluations that ran
what it produced.

```text
motiv.rules.evaluate   motiv.rules.name=can-checkout  motiv.rules.version=7
└── motiv.evaluate     motiv.proposition=…  motiv.satisfied=false
```

### Per-node spans

Off by default, and off by default *even for an audited rule*:

```csharp
MotivRulesTelemetry.NodeSpans = true;      // process-wide; set once at startup
MotivRulesTelemetry.MaxNodeSpans = 1000;   // the default
```

When on, an **audited** rule emits one `motiv.rules.node` span per causal node of its result tree, nested under
the evaluation span and tagged `motiv.satisfied` and `motiv.reason`. They ride the `audited` flag rather than
having a switch of their own, so they follow the same governed, versioned decision that turns the decision log
on.

> [!IMPORTANT]
> **These spans carry structure, not timing.** Motiv evaluates a composition in one pass and never times a
> sub-proposition, so a node span's duration is the walk that emitted it and nothing else. What is real is the
> shape: which sub-propositions were causal, how they nest, and which way each one went. `motiv.evaluate`'s own
> duration is the only honest number in the waterfall.

A tree larger than `MaxNodeSpans` is truncated, and says so: the evaluation span is tagged
`motiv.rules.nodes.truncated`. A waterfall that quietly stopped short would read as a complete picture of a
smaller tree.

The structural tree's durable home is the [decision log](../decision-log/index.md), not the trace waterfall. A
decision record keeps it for the retention window and can be queried; a trace is sampled, dropped under load, and
gone within days.

## Sensitive data: stated once

`motiv.reason` and `motiv.assertions` carry text your proposition authored, which can interpolate the model
(`model => $"income is {model.Income}"`). Core exposes `MotivTelemetry.ExplanationDetail` to suppress it &mdash;
see [Sensitive Data](index.md#sensitive-data).

If you use the decision log, you have **already stated your PII posture**, on the capture registry. So you do not
state it twice: creating a `DecisionLog` applies a ceiling derived from that registry.

| Registered posture | Explanation ceiling | Why |
|---|---|---|
| `StoreWhole<T>()` | `Full` | Raw model data is already accepted in durable storage; trace text is strictly less exposure |
| `Redact<T>(…)` | `None` | Your projection is not applied to assertion text &mdash; the same values would reach the exporter untouched |
| `ReferenceOnly<T>(…)` | `None` | As above; the key selector never sees the assertion string |
| *nothing registered* | `Full` | No statement has been made, so none is inferred |

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddDecisionLog(sink, log => log.Capture.ReferenceOnly<Customer>(c => c.Id));

// MotivTelemetry.ExplanationDetail is now None. Nothing else to configure.
```

> [!IMPORTANT]
> Constructing a `DecisionLog` applies the ceiling **process-wide, and does not restore it** — there is
> nothing to restore it to that would be safe. A host builds one log at startup, so this is simply
> "configured at startup". It is worth knowing about in a test suite or any process that builds several
> logs: the strictest posture any of them names wins for the whole process, for the rest of its life.

The `ExplanationCeiling` property itself is pure — read it to find out what a registry implies without
applying anything.

Two properties of the coupling are worth relying on:

- **It only ever tightens.** An adopter who has already chosen something stricter keeps it, and the order you
  configure things in cannot change the outcome.
- **It never derives `ReasonOnly`.** That looks like the middle of three privacy settings and is not one:
  `Reason` is built from the same authored strings as `Assertions`, so dropping the array reduces volume and
  cost, not exposure. It stays available if you set it by hand for those reasons.

Where several model types are registered with different postures, the strictest wins &mdash; the setting it feeds
is process-wide.

## Readiness

`AddMotivRules` registers a health check named `motiv-store`, tagged `ready`, that asks each store for its
generation. Map it on its own endpoint:

```csharp
app.MapHealthChecks("/health/ready",
    new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
```

The generation read is the right probe because it is the cheapest thing a store can be asked that still proves
the connection works &mdash; one scalar, no rows, the same call the refresh poller already makes on a timer.

It is registered rather than offered because a probe nobody remembered to enable is a replica that stays in
rotation with an unreachable database.

Note the deliberate difference from the `motiv-refresh` check that
[`AddRefresh`](../multi-instance/index.md) adds:

| Check | Asks | Reports when unhappy |
|---|---|---|
| `motiv-store` | Does the store answer? | `Unhealthy` |
| `motiv-refresh` | Have I converged? | `Degraded` |

A replica serving an older *approved* world is still serving correctly, so taking it out of rotation would turn a
stale pod into a missing pod. A replica that cannot reach its store can neither publish nor converge, and will
not recover by being sent more traffic. Filter your load-balancer probe to the `ready` tag so it is answered by
the first question, not the second.

## Next Steps

- [Observability](index.md) &mdash; core `Motiv`'s evaluation span and metrics.
- [The Decision Log](../decision-log/index.md) &mdash; the durable home of what an audited rule decided.
- [Multi-Instance Refresh](../multi-instance/index.md) &mdash; the poller behind `refreshes` and `replica_lag`.
