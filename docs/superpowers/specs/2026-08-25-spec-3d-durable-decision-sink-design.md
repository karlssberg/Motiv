# Spec 3D — The Durable Decision Sink — Design

**Date:** 2026-08-25
**Status:** Approved (design)
**Source:** The app surface of bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
resolving the durable half of ticket [15](https://github.com/karlssberg/Motiv/issues/115) and sub-3 of
ticket [16](https://github.com/karlssberg/Motiv/issues/116). Tracked as
[#138](https://github.com/karlssberg/Motiv/issues/138). Follows
[#141](https://github.com/karlssberg/Motiv/pull/141) (Spec 3B), which shipped the seam.

## Summary

Spec 3B shipped the decision log's *shape* — the `audited` flag, the record with its three anchors, the
capture postures, the bounded queue, the backpressure ladder, and `IDecisionSink` — and one
implementation of that seam, `InMemoryDecisionSink`, which is honest about being a reference
implementation: everything it holds dies with the process, and it has no window to enforce.

This slice ships the durable half. A `SqlDecisionSink` appends records to **its own database**, over a
connection the adopter supplies, under a **retention window it refuses to be constructed without**, and
purges past that window on a loop it starts itself.

Three things about that sentence are the whole design, and each is a decision rather than a detail.

## Decisions (locked)

### 1. Its own database, its own package, and no provider dependency

Ticket 16 settled that the decision log is "a separate sink, a separate connection, and may target a
different database or engine entirely", for three reasons that have not moved: its volume profile is
machine-rate against the authoring store's human-rate, so a decision-write storm co-located with
authoring would degrade authoring reads; its retention is a compliance window against version history's
*forever*; and ticket 10's three-records model already has the decision log merely *referencing*
version history rather than living beside it.

So this does not go in `Motiv.Serialization.EntityFrameworkCore`. Putting it there would put an EF
dependency in front of an adopter who wants only the sink, and — worse — would make the separateness a
convention rather than a fact, one `DbSet` away from being violated by someone doing the obvious thing.

It ships as **`Motiv.Serialization.Sql`**, and it references **no database provider at all**. The whole
implementation is `System.Data.Common`: the adopter hands it a `Func<DbConnection>`, and the sink opens,
writes and closes. That is what makes "a different engine entirely" true rather than aspirational — the
package cannot express a preference it does not have a reference to.

What *is* provider-specific is the SQL, so there is a `DecisionSqlDialect` seam with three built-ins —
`Sqlite`, `PostgreSql`, `SqlServer` — matching the authoring store's three providers. A dialect owns the
handful of places the three genuinely differ: identifier quoting, column type names, `CREATE TABLE IF
NOT EXISTS` (which SQL Server spells as a guard around the statement), and the row-limiting clause.
Everything else is one string.

**Why not EF, once more:** ticket 16's answer for the authoring store — one entity model migrated across
three providers, change-tracking overhead irrelevant at human write-rate — inverts here. The write is
one `INSERT` per record with no identity to track, at machine rate, and the read is a bounded page. EF's
wins do not apply and its costs do.

### 2. Retention is a constructor argument, not a setting

"There must be no unbounded default" is the requirement. A `TimeSpan` property defaulting to something
sensible would violate it; a property defaulting to `TimeSpan.Zero` would satisfy the letter of it and
produce a sink that deletes everything.

So `SqlDecisionSinkOptions.Retention` is `TimeSpan?`, defaults to **null**, and the sink's constructor
throws when it is null. This is the same shape as the capture posture in Spec 3B — required, absent by
default, refused at the boundary — and for the same reason: what the product cannot choose on the
adopter's behalf, it must not appear to have chosen.

`IDecisionSink`'s own contract asks for exactly this: *"they must not throw for recoverable conditions
… fail fast at construction instead."* A missing retention window is not recoverable at 3am on the
writer loop. It is recoverable at startup, loudly.

The window is validated as finite and positive. A record past it cannot be replayed, which ticket 15
already established as the **correct** post-retention state, not a loss.

### 3. The sink owns its purge loop

The purge could have been an `IHostedService` in the AspNetCore package, mirroring Spec 2B's refresh
poller. It is not, for one reason: a purge you forgot to register is an unbounded table — precisely the
failure the mandatory window exists to prevent. Retention travels with the implementation that can
honour it, and honouring it is not a separate registration an adopter can omit.

So the sink starts its loop in its constructor, exactly as `DecisionLog` starts its writer loop, and
stops it in `DisposeAsync`. It works in a console host, a test, or no host at all.

**Disposal stops the purge and nothing else.** The write path stays open — its schema bootstrap
included — because a `DbConnection` is opened per operation and there is nothing to close, and because
the container disposes singletons in reverse creation order, so a sink created before the
`DecisionLog` that drains into it would otherwise be torn down first and swallow the drain. That
extends to the bootstrap's own lock: a `SemaphoreSlim` throws once disposed, so disposing it would
make the invariant hold everywhere except the zero-config path, where the first write *is* the
bootstrap. A sink that keeps writing after its purge has stopped is the failure mode you want at
shutdown; the reverse is not.

The corollary for a host: register the sink through a factory, so the container owns it. An instance
handed to `AddSingleton` is never disposed, and the purge loop then runs until the process exits.

### 4. Two tables, and gaps are purged with the records they mark

`MotivDecision` and `MotivDecisionGap`, mirroring `InMemoryDecisionSink.Records` and `.Gaps` and for the
same reason: a gap is evidence *about* the log, not a decision, and counting it among decisions would
corrupt every query the log exists to answer.

The envelope goes in columns — `CorrelationId`, `TimestampUtc`, `Caller`, `RuleName`, `RuleVersion`,
`BuildId`, `Satisfied` — because those are what "why was *this* customer declined, on the 3rd, at 14:07?"
actually filters on. The outcome, the referenced proposition versions and the captured input go in as
JSON text, following ticket 16's document-as-text reasoning exactly: the sink never queries *into* them,
so a native JSON column would fork the schema per provider for no capability we use.

`Satisfied` is lifted out of the outcome JSON and given its own column. It is the one field inside the
payload that a query filters on rather than reads, and "show me the declines" should not be a table
scan through serialised justification trees.

Two indexes, and only two: `TimestampUtc` (the purge's own predicate, and every time-range question) and
`CorrelationId` (the pivot from one decision to every rule that took part in it). An append-heavy table
pays for each index on every insert, so the third one has to earn itself later.

Gaps are purged on the same window, keyed on `LastDroppedUtc`. Keeping a marker for a hole among records
that have themselves aged out would leave the log claiming a gap in a period it no longer covers.

### 5. Reading is on the sink, not on the seam

`IDecisionSink` stays write-only. It is the "emit, don't store" seam, and a sink that forwards to a SIEM
has nothing to read back — putting a query on the interface would make every such implementation lie.

`SqlDecisionSink` exposes a bounded `ReadAsync(DecisionQuery, …)` of its own, as `InMemoryDecisionSink`
exposes `Records`. The query filters on correlation id, rule name, verdict and a time range, is capped,
and returns newest first. That is enough for the sample's `/api/decisions` to stop being the one place the
reference implementation is not the durable one, and small enough that it does not become a reporting
API by accident.

Round-tripping is faithful with one documented exception: `DecisionInput.Value` and the outcome's
`Values` are `object?`, so what went in as a domain type comes back as a `JsonElement`. The alternative
is a type discriminator in the log, which would pin the adopter's assembly identity into their
compliance record — a worse trade than telling them what they get.

## What this does not do

- **It does not close the crash-loss window.** The queue in front of the sink is bounded by
  construction (`DecisionLogOptions.QueueCapacity`); a durable sink narrows the window, it does not
  close it. True zero-loss is an adopter `IDecisionSink` over a durable *queue* — an outbox or a broker
  — which ticket 15 already named as an adopter choice rather than something to build here.
- **It does not add telemetry instruments.** `motiv.rules.*` is a contract in one assembly, and the
  purge's readings (`PurgedCount`, `FailedPurgeCount`, `LastPurgeUtc`) are exposed on the sink for a
  host to surface. Write failures already reach an operator through
  `motiv.rules.decision_batches.failed`, which Spec 3C wired to `DecisionLog.FailedBatchCount`.
- **It does not migrate.** The schema is created once, idempotently, on the first write or on an
  explicit `EnsureSchemaAsync()` at startup. Ticket 16's Identity-pattern migration story is the
  *authoring* store's, where an adopter adds columns to a context they derive; there is nothing here to
  derive from and nothing yet to migrate.

## Verification obligations

From bundle spec 3 §7, the ones this slice owns:

- An audited rule's record survives the process: written through `DecisionLog` into a real database and
  read back with all three anchors and the captured input intact.
- A record past the retention window is gone, one inside it is not, and the purge says how many it took.
- A `Drop` under load leaves a gap marker in the durable log, honoured as carefully as a record.
- The schema is producible on all three dialects — the same structural proof `ProviderSchemaTests` gives
  the authoring store, since behavioural conformance runs on SQLite alone.
