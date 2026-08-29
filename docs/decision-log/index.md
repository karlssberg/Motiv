---
title: The Decision Log
description: Documentation for Motiv's decision log — the audited flag on a rule document, the three anchors that pin behaviour, the input-capture postures that decide what a record may keep of a model, and the bounded queue that keeps a database write off the evaluation path.
---

Motiv builds a complete answer on every evaluation — `Satisfied`, `Reason`, `Assertions`, `Values`,
`Justification`, the explanation tree — and then drops it on the floor. The decision log stores that
answer for rules that ask to be stored, so an operator can answer the question the feature exists for:
*why was this customer declined, on the 3rd, at 14:07?*

It ships in the `Motiv.Serialization` package (`DecisionRecord`, `IDecisionSink`, `DecisionLog`, the
capture postures); the DI seam ships in `Motiv.Serialization.AspNetCore` (`AddDecisionLog()`).

## Why This Exists

[Rule durability](../live-rules/durability.md) records every *edit*: who published what, when, and at
which version. It says nothing about what any of those versions ever *did*. A compliance question is
almost never about an edit — it is about one evaluation, on one input, at one moment, and whether the
answer it gave can still be explained. That needs a different record, and this is it.

The payload is not new. `ResultSerializer.ToEvaluationResult(...)` has always produced a serialisable
projection of a result, and `/api/checkout` in Studio built two of them per request and threw them
away. The **envelope** around that payload is what this adds.

## Opting In: `audited` On The Document

Logging is opt-in per rule, and total when it is on: an audited rule records *every* evaluation in
full. Sampling is worthless for a question about one specific customer.

```json
{
  "audited": true,
  "rule": { "and": [ { "spec": "customer.is-active" }, { "spec": "customer.has-orders" } ] }
}
```

The flag lives on the **rule document**, not in host configuration, and three things follow from that
placement alone:

- It is **versioned** with the rule, so *when auditing was turned on* is itself a fact in version
  history — which is a compliance question, not a footnote to one.
- Toggling it is a **governed change**, because every document change is. `RuleDocumentComparer` counts
  `audited` as structural, so a change that flips it is never classified `change.is-metadata-only` and
  never travels under the lighter ceremony reserved for typo fixes.
- **Audited implies a stored document, by construction.** A rule running on a compiled default has no
  document to hold the flag, so marking it audited means transcribing that default into a stored,
  versioned document first. Nothing enforces this rule; it is simply not expressible otherwise.

## The Record, And Its Three Anchors

```csharp
public sealed record DecisionRecord(
    Guid Id,
    string CorrelationId,
    DateTimeOffset TimestampUtc,
    string? Caller,
    string RuleName,
    int RuleVersion,                                           // anchor 1
    string BuildId,                                            // anchor 2
    IReadOnlyList<PropositionVersion> ReferencedPropositionVersions,   // anchor 3
    DecisionInput? Input,
    RuleEvaluationResult<object?> Outcome);
```

Reconstructing what a rule did needs **three anchors, not one**:

1. **`RuleVersion`** — the rule's own composition, guaranteed to be a stored document by the flag's
   placement.
2. **`BuildId`** — the compiled specs the document references. A rule that resolves a name to a C#
   delegate changes behaviour when the code is redeployed with no version bump, and a delegate has
   nothing stable to fingerprint, so the build is recorded instead.
3. **`ReferencedPropositionVersions`** — what those names *meant* when the rule ran. A rule version
   pins the rule's composition; it does not pin what `customer.is-active` said. That is a fact about
   the evaluation rather than the edit, which is why it lives here and not in the version log.

Anchor 3 is the **transitive** closure, not the rule's direct references. A rule reaching
`customer.is-active` only through `pricing.eligible` changes behaviour when either is republished, so
a record pinning one hop would claim to identify behaviour and would not.

It costs one graph walk per *binding*, not per evaluation: republishing anything in the closure rebinds
every referrer and produces a new binding, so a pin computed against one cannot go stale while that
binding is live.

## What A Record May Keep Of The Model

Replay is impossible without the input; storing the input means storing whatever the model holds. Motiv
cannot make that trade for you — it depends on your data and your regime — so it is a seam, with
**no default**:

| Posture | Replay | Privacy |
|---|---|---|
| `StoreWhole<T>()` | complete | stores raw PII — **development only** |
| `Redact<T>(projection)` | as far as the mask left it | you decide what survives |
| `ReferenceOnly<T>(keySelector)` | via your own system of record | **recommended for production** |

```csharp
options.Capture.ReferenceOnly<Customer>(customer => customer.CustomerId);
```

**A rule marked `audited` over a model type with no posture registered does not bind.** The refusal is
`RuleErrorCode.AuditCaptureNotConfigured`, and putting it at bind time puts it in three places at once:
a governed publish is rejected with a readable reason, a startup load reports it, and a replica deployed
without the posture quarantines the rule and says why rather than silently logging whatever the model
happens to hold. A whole-model default that applied by omission would be a privacy leak nobody chose.

`ReferenceOnly` is recommended for production because it lets erasure and audit coexist: erase the
subject in your system of record, and the decision record survives without personal data while replay
correctly becomes impossible. **The posture you choose is the replay ceiling.**

## Off The Evaluation Path

An audited rule on a checkout path must not pay a database write per evaluation. Records go into a
bounded in-process queue, and one background writer batches them into an `IDecisionSink`.

What that buys in latency it owes in durability: everything queued is in memory, so
`DecisionLogOptions.QueueCapacity` is the size of a **crash-loss window**. True zero-loss is an
`IDecisionSink` over a durable queue — which is the same seam you implement to emit into a SIEM, an
outbox, or a message bus rather than storing at all.

When the queue is full, one of three postures applies:

| `DecisionBackpressure` | On a full queue | Protects |
|---|---|---|
| `FailClosed` *(default)* | the evaluation throws `DecisionNotLoggedException` | the evidence |
| `Block` | the caller waits for capacity | the evidence, at the cost of latency |
| `Drop` | the record is shed and a `DecisionGap` is written | latency |

`FailClosed` is the default because `audited` is a claim that the record is load-bearing: an audited
decision that was not logged did not happen. `Drop` is never silent — the run of shed records is
written to the log ahead of the batch that follows it, so the hole is provable rather than invisible.

Once the log is **disposed**, every posture fails, `Drop` included: capacity is never coming back, and a
disposed log cannot write the gap marker that would make a drop provable.

## One Decision, One Correlation Id

`DecisionSnapshot` already meant "one decision's world, held still". It now carries that decision's
identity too, so several rules evaluated inside one pin share one `CorrelationId` — because they were
one decision.

```csharp
using var _ = rules.PinSnapshot(http.TraceIdentifier, http.User.Identity?.Name);

var eligibility = canCheckout.Evaluate(customer);
var screening   = await fraudScreening.EvaluateAsync(customer, cancellationToken);
```

Hosts using `MapMotivRules` get this for free: the per-request filter pins the world and names the
decision from the request's trace identifier and authenticated subject. An unauthenticated caller is
recorded as `null` rather than `"unknown"` — a record that admits what it does not know is better than
one that claims otherwise.

With no pin open, each evaluation still gets a fresh correlation id. A single-rule decision is still a
decision.

## Available Types and Methods

| Type / Method | Description |
|---|---|
| [The Record](./record.md) | `DecisionRecord`, `PropositionVersion`, `DecisionInput`, and `DecisionGap`. |
| [Capture Postures](./capture.md) | `DecisionCaptureRegistry`, the three postures, and the bind-time refusal. |
| [The Sink and the Queue](./sink.md) | `IDecisionSink`, `DecisionLog`, `DecisionLogOptions`, `DecisionBackpressure`, and `AddDecisionLog()`. |
| [The Durable Sink and Retention](./durable.md) | `SqlDecisionSink`, `SqlDecisionSinkOptions`, `DecisionSqlDialect`, `DecisionQuery`, and the retention purge. |

## What This Does Not Do Yet

- **A zero-loss queue.** The durable sink ([here](./durable.md)) makes the log outlive the process, but
  the queue in front of it is still a bounded crash-loss window by construction. Closing it needs a
  durable *queue* — an outbox or a broker — which is your own `IDecisionSink` over that transport.
- **Replay.** The three anchors and the captured input are what make replay *possible*. Motiv does not
  yet ship the replayer.
