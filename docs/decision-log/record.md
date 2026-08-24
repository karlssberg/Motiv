---
title: The Record
description: DecisionRecord, PropositionVersion, DecisionInput, and DecisionGap — what one evaluation of an audited rule leaves behind, and why it takes three anchors rather than one to identify the behaviour that produced it.
---

## `DecisionRecord`

One evaluation of an audited rule, as it will be stored.

```csharp
public sealed record DecisionRecord(
    Guid Id,
    string CorrelationId,
    DateTimeOffset TimestampUtc,
    string? Caller,
    string RuleName,
    int RuleVersion,
    string BuildId,
    IReadOnlyList<PropositionVersion> ReferencedPropositionVersions,
    DecisionInput? Input,
    RuleEvaluationResult<object?> Outcome);
```

| Member | What it is for |
|---|---|
| `Id` | This record's own identity. |
| `CorrelationId` | The decision this evaluation belonged to. Rules evaluated inside one `DecisionSnapshot` share it. |
| `TimestampUtc` | When the evaluation completed. |
| `Caller` | Who the decision was taken for, or `null` when nothing named them. |
| `RuleName` / `RuleVersion` | The rule, and the version of its document. **Anchor 1.** |
| `BuildId` | The build that was live, from `BuildIdentity.Current`. **Anchor 2.** |
| `ReferencedPropositionVersions` | Every authored proposition the rule resolved through, transitively. **Anchor 3.** |
| `Input` | What the configured capture posture kept of the model. |
| `Outcome` | The verdict and its full justification. |

`Outcome` is `RuleEvaluationResult<object?>` rather than the rule's own metadata type: one log holds
records from every rule regardless of what each yields, and everything that reads it back — JSON, a
durable sink — is untyped anyway. It is produced by the same projection `ResultSerializer` uses, so a
record and the HTTP response that reported the same evaluation cannot describe it differently.

### Why the outcome is materialised on the calling thread

The projection could in principle be deferred to the background writer, moving its cost off the request
path along with the write. It is not. The result tree memoises as it is read, and none of that
memoisation is documented thread-safe — handing a half-read result to a writer thread races the caller
still reading it, in the one subsystem whose output is the product. What crosses the queue is immutable.

This is a real per-evaluation cost on audited rules, and it is the dominant one: it scales with the
size of the result tree, because `Justification` and the explanation tree are built over all of it.

## `PropositionVersion`

```csharp
public sealed record PropositionVersion(string Name, int Version);
```

A value, not a reference: these lists get compared when two records are reconciled, and reference
equality would make every such comparison quietly false.

A name that resolves to a **compiled** spec rather than an authored proposition contributes nothing
here — it has no version of its own, which is exactly what `BuildId` exists to pin. A rule composed
entirely of compiled specs therefore records an empty list, and that is an answer rather than a gap.

## `DecisionInput`

```csharp
public enum DecisionInputKind { Whole, Redacted, Reference }

public sealed record DecisionInput
{
    public DecisionInputKind Kind { get; }
    public object? Value { get; }

    public static DecisionInput Whole(object? model);
    public static DecisionInput Redacted(object? projection);
    public static DecisionInput Reference(string key);
}
```

The kind is stored beside the value rather than inferred from its shape, because what a record is worth
for replay is a fact about *how it was captured* — and because it is a standing statement of the privacy
posture that produced it. See [Capture Postures](./capture.md).

`Input` is `null` when no posture covered the model's type. In practice an audited rule cannot reach
that state, because a document that would produce it does not bind.

## `DecisionGap`

```csharp
public sealed record DecisionGap(
    DateTimeOffset FirstDroppedUtc,
    DateTimeOffset LastDroppedUtc,
    long DroppedCount);
```

A hole in the log, written where records were shed under `DecisionBackpressure.Drop`. A missing record
is otherwise indistinguishable from a decision that was never taken — which is precisely the ambiguity
an audit trail exists to remove.

One gap is written per contiguous run of drops, immediately **ahead** of the batch that follows it; a
marker written behind those records would misplace the hole. A gap is evidence about the log rather than
a decision, so `IDecisionSink` takes it through its own method and it is never countable among the
records.
