---
title: The Durable Sink and Retention
description: SqlDecisionSink, SqlDecisionSinkOptions, DecisionSqlDialect, DecisionQuery and DecisionPurgeReport — a raw-append decision log in its own database, a retention window it refuses to be built without, and the purge that honours it.
---

`InMemoryDecisionSink` is the reference implementation: enough for development, tests and the sample,
and explicitly not enough for production, where the log must outlive the process and be bounded by a
window. `SqlDecisionSink`, in the **`Motiv.Serialization.Sql`** package, is the durable half.

```csharp
builder.Services.AddSingleton(_ => new SqlDecisionSink(
    () => new SqliteConnection(decisionsConnectionString),
    new SqlDecisionSinkOptions
    {
        Dialect = DecisionSqlDialect.Sqlite,
        Retention = TimeSpan.FromDays(90)
    }));

builder.Services.AddMotivRules(registry, options)
    .AddDecisionLog(
        provider => provider.GetRequiredService<SqlDecisionSink>(),
        log => log.Capture.ReferenceOnly<Customer>(c => c.CustomerId))
    .AddRule<CanCheckoutRule>();
```

## Its Own Database

The decision log is a **separate database** from the authoring store — separate connection, and it may
be a separate engine entirely. That is an invariant, not a deployment taste:

- **Volume.** Decisions are machine-rate where authoring is human-rate, so a decision-write storm
  co-located with authoring would degrade authoring reads.
- **Retention.** Version history is kept forever; a decision record lives inside a compliance window.
- **Direction.** The decision log *references* version history. Merging them inverts the relationship.

Point this at the authoring database and all three properties are lost. It is also why the sink is not
EF Core: the authoring store's case for EF — one entity model migrated across three providers,
change-tracking overhead irrelevant at human write-rate — inverts here.

## No Provider Dependency

The package references **no database provider**. `SqlDecisionSink` is written against
`System.Data.Common`: you supply a `Func<DbConnection>`, and a `DecisionSqlDialect` supplies what the
engine needs beyond ADO.NET. Three dialects ship — `Sqlite`, `PostgreSql`, `SqlServer` — and a fourth
engine is a derived class rather than a feature request.

`Dialect` has **no default**. The connection factory says nothing about the engine behind it, so a
default would be a guess that fails at the first write rather than at startup.

## Retention Is Required

`Retention` is `TimeSpan?`, defaults to `null`, and the constructor throws when it is null.

Version history is kept forever; this is the record that is genuinely unbounded, because an audited
rule on a hot path is millions of rows. So there is no default: a window defaulting to something
sensible would be Motiv choosing your compliance posture, and one defaulting to zero would satisfy the
letter of "a window was set" while deleting everything. Infinite windows are rejected too —
`Timeout.InfiniteTimeSpan` is the obvious way to spell "keep forever", which is the one thing this
must not allow.

A record past the window cannot be replayed. That is the **correct** post-retention state, not a loss.

## The Purge Runs Itself

The sink starts its purge loop in its constructor and stops it in `DisposeAsync`, exactly as
`DecisionLog` starts its writer loop. It is not an `IHostedService` you register, because a purge you
forgot to register is an unbounded table — the failure the mandatory window exists to prevent.

The first pass waits out one `PurgeInterval` rather than running at startup, so a host learns that its
decision database is unreachable from its readiness probe rather than from a purge failure in its
first second. Each pass issues bounded deletes until nothing is left, so a purge after a long outage
does not hold one lock for minutes. The loop never dies: a failed pass increments `FailedPurgeCount`
and the next one runs.

**Disposal stops the purge and nothing else.** The write path stays open — including its schema
bootstrap — because a container disposes singletons in reverse creation order: a sink created before
the `DecisionLog` that drains into it is torn down first, and a sink that refused to write after
disposal would swallow that drain. Register it through a factory (`AddSingleton(_ => new
SqlDecisionSink(...))`) so the container owns it; a pre-built instance handed to `AddSingleton` is
never disposed, which leaves the purge loop running until the process exits.

| Option | Default | Description |
|---|---|---|
| `Retention` | *(none — required)* | How long a record is kept. Positive and finite. |
| `Dialect` | *(none — required)* | Which engine's SQL to write. |
| `PurgeInterval` | `1 hour` | How often the purge runs. The first pass waits one interval. |
| `PurgeBatchSize` | `5000` | The most rows one delete statement takes — how long any one lock is held, not a cap on the pass. |
| `EnsureSchema` | `true` | Whether the tables are created on first use. Turn it off where DDL is a deployment concern and call `EnsureSchemaAsync()` from a migration step. |
| `JsonOptions` | *(default)* | How the outcome, the proposition versions and the captured input are serialised. |

| Reading | Description |
|---|---|
| `PurgedCount` | Records purged since this sink was created. |
| `FailedPurgeCount` | Purge passes that failed. A rising count is a window no longer being enforced — worth an alert, because nothing else will say so and the table simply grows. |
| `LastPurgeUtc` | When the last pass completed, or null if none has. |

The purge has no `motiv.rules.*` instrument of its own — that contract lives in `Motiv.Serialization`
and this package is downstream of it — so these readings, and the `DecisionPurgeReport` returned by
`PurgeAsync()`, are how a host surfaces it. Write failures already reach an operator through
`motiv.rules.decision_batches.failed`.

## The Schema

Two tables, mirroring `Records` and `Gaps`, because a gap is evidence *about* the log rather than a
decision and counting one among decisions would corrupt every query the log exists to answer.

`MotivDecision` holds the envelope in columns — `CorrelationId`, `TimestampUtc`, `Caller`, `RuleName`,
`RuleVersion`, `BuildId`, `Satisfied` — and the outcome, the referenced proposition versions and the
captured input as JSON text. Nothing queries *into* those, so a native JSON column would fork the
schema per provider for a capability never used. `Satisfied` is lifted out of the outcome because it
is the one field inside the payload a query filters on rather than reads — "show me the declines"
should be a predicate the database applies, not a scan through serialised justification trees.

Two indexes, and only two: `TimestampUtc` (the purge's own predicate, and every time-range question)
and `CorrelationId` (the pivot from one decision to every rule that took part in it). An append-heavy
table pays for each index on every insert, and `Satisfied` gets none of its own: a two-valued column
is poor index material, and every question that asks it also names a window.

`MotivDecisionGap` holds `FirstDroppedUtc`, `LastDroppedUtc` and `DroppedCount`. Gaps are purged on
the same window, keyed on the last drop — a marker for a hole among records that have themselves aged
out would leave the log claiming a gap in a period it no longer covers.

## Reading It Back

Reading lives on `SqlDecisionSink`, **not** on `IDecisionSink`. That seam is also "emit, don't store",
and a sink forwarding to a SIEM has nothing to read back — a query on the interface would make every
such implementation lie.

```csharp
var records = await sink.ReadAsync(new DecisionQuery
{
    CorrelationId = "trace-abc",   // one decision, every rule that took part in it
    RuleName = "checkout.can-checkout",
    Satisfied = false,             // show me the declines
    FromUtc = when.AddMinutes(-1),
    ToUtc = when.AddMinutes(1),
    Limit = 100                    // newest first, always capped
});

var gaps = await sink.ReadGapsAsync();   // empty is the only healthy value
```

Round-tripping is faithful with one exception: `DecisionInput.Value` and the outcome's `Values` are
`object?`, so a `Whole` or `Redacted` capture comes back as a `JsonElement` rather than your own type.
A `Reference` capture is a string by construction and comes back as one. The alternative — a type
discriminator in the log — would pin your assembly identity into your compliance record.

## What It Still Does Not Do

**It does not close the crash-loss window.** The queue in front of the sink is bounded by construction
(`DecisionLogOptions.QueueCapacity`); a durable sink narrows that window, it does not close it. True
zero-loss needs a durable *queue* — an outbox or a broker — which is your own `IDecisionSink` over that
transport, using the same seam.
