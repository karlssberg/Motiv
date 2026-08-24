---
title: The Sink and the Queue
description: IDecisionSink, DecisionLog, DecisionLogOptions, DecisionBackpressure, and AddDecisionLog — how records leave the evaluation path, what happens when the sink cannot keep up, and where an adopter plugs in their own log pipeline.
---

## `IDecisionSink`

```csharp
public interface IDecisionSink
{
    Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken);
    Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken);
}
```

The SDK owns the queue, the batching and the backpressure posture. An implementation of this owns
nothing but the writing, and is called on a background writer rather than on the evaluation that
produced the records.

This is also the **"emit, don't store" seam**. An adopter who wants decisions in a SIEM, an outbox or a
message bus implements this rather than asking for a feature — and an adopter who needs true zero-loss
implements it over a durable queue, because the in-process queue in front of it is a bounded
crash-loss window by construction.

Implementations are called from one writer loop at a time and need not be thread-safe against
themselves. They must not throw for recoverable conditions: a throwing sink costs its batch and
increments `DecisionLog.FailedBatchCount`, and the loop continues — so a permanently failing sink loses
records. Fail fast at construction instead.

`InMemoryDecisionSink` is the reference implementation, for development, tests and the sample. It keeps
`Records` and `Gaps` separately, because a gap is evidence about the log rather than a decision.

## `DecisionLog`

```csharp
var log = new DecisionLog(sink, options);
```

A bounded queue and one background writer. `Enqueue` is what an audited evaluation calls; it is
synchronous and, in the ordinary case, cheap.

| Member | Description |
|---|---|
| `Enqueue(DecisionRecord)` | Hands a record to the queue, applying the posture when it is full. |
| `Capture` | The capture registry, for reading the configured posture. |
| `DroppedCount` | Records shed under `Drop`, cumulative. The sum of every gap written equals it. |
| `FailedBatchCount` | Batches the sink refused. A rising count is a sink that needs attention. |
| `DisposeAsync()` | Stops accepting records and drains the queue into the sink. |

`DroppedCount` is **monotonic**: taking a gap marker reports a run, it does not forgive it.

Dispose the log at shutdown — that is what closes the crash-loss window on purpose. Registered through
`AddDecisionLog()` the container does it for you.

## `DecisionLogOptions`

| Option | Default | Description |
|---|---|---|
| `QueueCapacity` | `1024` | How many records may wait for the sink. **This is the size of the crash-loss window**, not a throughput dial. |
| `MaxBatchSize` | `64` | The largest batch handed to the sink in one call. |
| `Backpressure` | `FailClosed` | What a full queue means. See below. |
| `Capture` | *(empty)* | The capture postures. Not optional — see [Capture Postures](./capture.md). |

## `DecisionBackpressure`

| Posture | On a full queue | Protects |
|---|---|---|
| `FailClosed` *(default)* | the evaluation throws `DecisionNotLoggedException` | the evidence |
| `Block` | the caller waits for capacity | the evidence, at the cost of latency |
| `Drop` | the record is shed, and a `DecisionGap` is written | latency |

`FailClosed` is the default because `audited` is a claim that the record is load-bearing. If losing it
were acceptable, the rule did not need the flag.

`Block` on the synchronous evaluation path means **blocking a thread** on queue capacity. That is what
choosing it asks for; quietly degrading to `Drop` would be worse than the wait. Asynchronous rules await
instead, and get the same semantics without holding a thread.

`Drop` is never silent. The run of shed records becomes a `DecisionGap` written immediately ahead of the
batch that follows it, turning a compliance hole into a provable one.

Once the log is **disposed**, every posture fails — `Drop` included. Capacity is never coming back, so
`Block` would hang forever, and a disposed log cannot write the gap marker that makes a drop provable,
so dropping there would be exactly the silent loss `Drop` exists to avoid. Disposal is a lifecycle
event, not backpressure, and `DecisionNotLoggedException`'s message says which of the two happened.

## Wiring It Up

Without ASP.NET, pass the log to the rule set:

```csharp
var options = new DecisionLogOptions { Backpressure = DecisionBackpressure.Block };
options.Capture.ReferenceOnly<Customer>(customer => customer.CustomerId);

await using var log = new DecisionLog(new InMemoryDecisionSink(), options);
var rules = new RuleSet(registry, store, decisionLog: log).Add(new CanCheckoutRule());
```

With ASP.NET, `AddDecisionLog()` registers it as a singleton, so the container drains the queue at
shutdown — a scoped log would take its unwritten records down with every request:

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddDecisionLog(new InMemoryDecisionSink(), log =>
    {
        log.Backpressure = DecisionBackpressure.Block;
        log.Capture.ReferenceOnly<Customer>(customer => customer.CustomerId);
    })
    .AddRule<CanCheckoutRule>();
```

An overload takes `Func<IServiceProvider, IDecisionSink>`, for a durable sink that needs a connection, a
context factory or a client of its own.

With no decision log registered at all, an audited rule document does not bind — which is the intended
fail-closed behaviour, not a wiring bug.
