# Spec 3B — The Decision Log — Design

**Date:** 2026-08-24
**Status:** Approved (design)
**Source:** Build step 2 of bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
resolving ticket [15](https://github.com/karlssberg/Motiv/issues/115). Follows
[#134](https://github.com/karlssberg/Motiv/pull/134) (Spec 3A), which closed build step 1.

## Summary

An operator cannot today answer "why was *this* customer declined, on the 3rd, at 14:07?" The engine
constructs the whole answer on every evaluation — `Satisfied`, `Reason`, `Assertions`, `Values`,
`Justification`, the explanation tree — and then drops it on the floor. `/api/checkout`
(`Program.cs:279`) builds two `ToEvaluationResult`s per request and returns them to one caller, whose
browser then forgets them.

This slice stores that answer for rules that ask to be stored, and does it without turning an
evaluation into a database write.

The shape, in one paragraph: a rule opts in with an **`audited` flag on its document** — versioned,
therefore governed, therefore itself auditable. Every evaluation of an audited rule produces a
**`DecisionRecord`**: the outcome payload that already exists, wrapped in an envelope that pins
behaviour with **three anchors** — the stored document's version, the build, and the versions of every
authored proposition it resolved through. The **input** is captured through an adopter-chosen
strategy — whole, redacted, or by reference — and there is **no default**: marking a rule audited
without choosing one is a bind error, not a silent decision to store PII. Records leave the hot path
through a **bounded queue** drained by a background writer into an **`IDecisionSink`**, and when the
queue is full the default is to **fail the decision**, because an audited decision that was not
logged did not happen.

## What is already here, and what is not

| Piece | State today |
|---|---|
| The outcome payload | `RuleEvaluationResult<TMetadata>` — exists, complete, serialisable |
| The projection from a live result | `ResultSerializer.ToEvaluationResult` — exists |
| The build anchor | `BuildIdentity.Current` — exists, added by Spec 2A for the version log |
| The rule-version anchor | `Rule.State.Version` — exists |
| Proposition versions | `AuthoredProposition.Version` per name in `ScopeGeneration.Authored` — exists |
| Forward reference edges | `DependencyGraph._outgoing` — exists, but has no forward-closure query |
| A decision's identity | `DecisionSnapshot` — exists as a *world* pin; carries no correlation id |
| `audited`, `DecisionRecord`, `IDecisionSink`, capture strategies | **Nothing. All new.** |

The envelope is the new work. The payload is a re-use, and that is the ticket's own framing: "this
ticket is about *storing* what the code already constructs".

## Decisions (locked)

### 1. `audited` is a document field, and that is load-bearing three times over

`"audited": true` sits beside `"name"` and `"rule"` in the rule document. Not host configuration, not
an attribute, not a registration-time call. Three consequences follow from the placement alone:

- **It is versioned** (Spec 2A's log), so *when auditing was turned on* is a fact in version history —
  which is the compliance question, not a footnote to it.
- **Toggling it is a governed `ChangeRequest`** (Spec 1's gate), because every document change is.
- **It forces "audited ⟹ stored document"**, because a rule running on a compiled default has no
  document to hold the flag. Ticket 02's open question — should `audited` require a stored document? —
  is answered as a consequence of placement rather than as a separate rule anyone has to enforce.

`RuleDocumentComparer.StructurallyEqual` **must compare it**. That comparer feeds
`change.is-metadata-only`, which exists to give a typo fix in an assertion string a lighter gate than a
logic change. Turning audit on is not a typo fix. Omitting `Audited` from the comparer would let an
adopter disable the audit trail under the metadata-only ceremony, which is precisely the ceremony
chosen for changes that cannot matter.

### 2. Three anchors, and the third is a fact about the *evaluation*

```
DecisionRecord(
  Id, CorrelationId, TimestampUtc, Caller,     // envelope
  RuleName, RuleVersion,                        // anchor 1 — the document
  BuildId,                                      // anchor 2 — the compiled specs it references
  ReferencedPropositionVersions,                // anchor 3 — what those names meant at the time
  Input?,                                       // the capture seam
  Outcome )                                     // RuleEvaluationResult<object?>
```

A rule version pins the rule's *own* composition. It does not pin what `customer.is-active` said when
the rule ran — that proposition has its own version and its own publish history. Anchor 3 is therefore
not redundant with anchor 1, and it belongs on the *evaluation* record rather than on the version-log
row, because it is a fact about the moment the rule ran, not about the moment it was edited.

**It is the transitive closure, not the direct references.** A rule referencing `pricing.eligible`,
which itself references `customer.is-active`, changes behaviour when either is republished. Pinning
only the first would leave a record that claims to identify behaviour and does not.

**Cost:** the closure is computed **once per bound state**, not once per evaluation, and this is sound
rather than an optimisation. Republishing any proposition in the closure rebinds every referrer
(`DependencyGraph.DependentClosure` → `PrepareRebind`), and a rebind produces a *new* `State`. So the
closure and its versions are constant for a `State`'s whole lifetime, and a `Lazy` field on the state
pays for it on the first audited evaluation and never again.

`DependencyGraph` gains `ReferenceClosure(NodeId)` — the forward walk it has the edges for but has
never been asked for.

### 3. The outcome is projected eagerly, on the calling thread

The tempting move is to enqueue the live `BooleanResultBase` and let the background writer call
`ToEvaluationResult` — moving the projection cost off the request path too, not just the write.

**Rejected.** The result tree memoises as it is read (`Explanation`, the description formatters, and
after Spec 3A the fold memos), and none of that memoisation is documented thread-safe. Handing a
half-read result to a writer thread while the request thread is still reading it is a data race in the
one subsystem whose output is the product. The projection is materialised on the calling thread, and
what crosses the queue is an immutable record of strings.

This is a real cost and it is stated rather than hidden: an audited rule pays a full
`Assertions`/`Justification`/`Explanation` materialisation per evaluation. It is bounded, it is what
"audited means total" costs, and a follow-up may revisit it if the tree is ever made explicitly
thread-safe.

### 4. Input capture is a seam with no default

```csharp
options.Capture
    .ReferenceOnly<Customer>(c => c.Id)      // GDPR-clean — recommended for production
    .Redact<Order>(o => new { o.Total })     // adopter masks
    .StoreWhole<CartLine>();                 // dev only
```

Keyed by **model type**, which is where the typed projection has to live, and which matches the
ticket's framing of a posture chosen once per deployment.

**Enabling `audited` with no capture registered for the rule's model type is a bind error**
(`RuleErrorCode.AuditCaptureNotConfigured`), reported through the existing `RuleError` machinery. That
puts the refusal in three places for free: a governed publish is rejected with a readable message, a
startup load reports it, and a replica loading a document from a store it is not configured for
**quarantines the rule and says why** rather than silently logging whatever the model happens to hold.
A whole-model default that is on by omission is the default-credentials trap wearing a compliance
badge.

`ReferenceOnly` is the recommended production posture because it makes erasure and audit coexist:
erase the subject in the adopter's system of record, the decision log keeps a non-PII record of the
decision, and replay correctly becomes impossible. **The strategy is the replay ceiling** — the adopter
trades privacy against replay fidelity explicitly, per deployment.

### 5. `FailClosed` is the default, and `Drop` is never silent

A bounded queue, a background writer, and one of three postures when the queue is full:

| Posture | On a full queue | Protects |
|---|---|---|
| `FailClosed` *(default)* | the evaluation **throws** `DecisionNotLoggedException` | the evidence |
| `Block` | the calling thread waits for capacity | the evidence, at the cost of latency |
| `Drop` | the record is shed, and a **gap marker** is written | the latency |

`FailClosed` is the default because `audited` is a claim that the record is load-bearing. If it were
acceptable to lose it, the rule did not need the flag.

`Drop` writes `DecisionGap(FirstDroppedUtc, LastDroppedUtc, DroppedCount)` through the sink's second
method, ahead of the next successful batch — a provable hole in the log instead of an invisible one.
The **telemetry counter** the ticket also requires (`motiv.rules.decisions.dropped`,
`motiv.rules.decision_queue.depth`) belongs to build step 3, which is the telemetry slice; the gap
marker is the part that lives in the log itself and it lands here, so the log is never silently
incomplete even before the counters exist.

The in-memory queue is a **bounded crash-loss window**, documented as such. True zero-loss is a durable
queue, which is an adopter `IDecisionSink` — the same seam as "emit, don't store".

### 6. `Block` blocks, on the synchronous path, deliberately

`Rule.Evaluate` is synchronous. `Block` there means blocking a thread on queue capacity. That is
exactly what the adopter asked for by choosing it, and pretending otherwise (silently degrading to
`Drop`, say) would be worse than the latency. It is documented as the posture that risks the request
path. The async rules await instead, and get the same semantics without the thread.

### 7. The correlation id rides `DecisionSnapshot`

`DecisionSnapshot` already means "one decision's world, held still", already nests correctly, and the
ASP.NET endpoints already open one per request. Giving it a `CorrelationId` and a `Caller` makes it
mean "one decision", full stop — which is what its name says and what a correlation id is for. Several
rules evaluated inside one snapshot share one correlation id, because they were one decision.

It becomes ambient (`DecisionSnapshot.Current`, an `AsyncLocal`) so `Rule.Evaluate` can find it without
a parameter on the evaluation signature. With no snapshot open, each record gets a fresh correlation id
and a null caller — a single-rule decision is still a decision.

## Architecture

### `Motiv.Serialization` (new files)

```
Decisions/DecisionRecord.cs           DecisionRecord, PropositionVersion
Decisions/DecisionGap.cs              DecisionGap
Decisions/IDecisionSink.cs            WriteAsync(records), WriteGapAsync(gap)
Decisions/DecisionLog.cs              the queue, the writer, the backpressure posture
Decisions/DecisionLogOptions.cs       QueueCapacity, Backpressure, MaxBatchSize, Capture, Clock
Decisions/DecisionBackpressure.cs     FailClosed | Block | Drop
Decisions/DecisionCaptureRegistry.cs  StoreWhole<T> / Redact<T> / ReferenceOnly<T>
Decisions/DecisionInput.cs            DecisionInput, DecisionInputKind
Decisions/DecisionNotLoggedException.cs
Decisions/InMemoryDecisionSink.cs     the reference sink (tests, dev, the sample)
```

### `Motiv.Serialization` (changed)

| File | Change |
|---|---|
| `RuleDocument.cs` | `bool Audited` |
| `RuleDocumentParser.cs` | `case "audited"` — must be a boolean |
| `Governance/RuleDocumentComparer.cs` | compare `Audited` (see decision 1) |
| `RuleErrorCode.cs` | `AuditCaptureNotConfigured` |
| `Rules/Rule.cs`, `Rules/AsyncRule.cs` | `State` gains `Audited` + the lazy proposition pin; the four `Evaluate`/`EvaluateAsync` entry points record |
| `Rules/RuleBase.cs` | internal `DecisionLog?`, assigned by `RuleSet.Add` |
| `Rules/RuleSet.cs` | optional `DecisionLog?` ctor parameter; `PinSnapshot(correlationId, caller)` |
| `Propositions/DecisionSnapshot.cs` | `CorrelationId`, `Caller`, ambient `Current` |
| `Propositions/DependencyGraph.cs` | `ReferenceClosure(NodeId)` |
| `ResultSerializer.cs` | the explanation mapping moves to an internal projector shared with the log |
| `schemas/rule.v1.json` | `"audited": { "type": "boolean" }` |

`System.Threading.Channels` is added for the `netstandard2.0` target only — it is in-box from .NET Core
3.0 onward, and the project already carries `System.Text.Json` on the same condition.

### Data flow

```
Rule.Evaluate(model)
  ├─ state = StateIn(Scope.Active)          (unchanged)
  ├─ result = state.Spec.Evaluate(model)    (unchanged)
  └─ if (state.Audited):
        capture   = log.Capture.For(typeof(TModel))     -> DecisionInput
        anchors   = state.PropositionPin.Value          -> [(name, version), ...]  (lazy, once)
        outcome   = ResultProjection.Project(result)    -> RuleEvaluationResult<object?>
        log.Enqueue(record)   -> TryWrite
                                  ├─ ok    -> return result
                                  ├─ full + FailClosed -> throw DecisionNotLoggedException
                                  ├─ full + Block      -> wait, then write
                                  └─ full + Drop       -> count the gap, return result

DecisionLog background writer
  └─ drain up to MaxBatchSize -> [gap first, if any] -> sink.WriteAsync(batch)
```

### `Motiv.RulesEngine.Sample`

`can-checkout` is marked `audited` in its default document; the app registers an
`InMemoryDecisionSink` and a `ReferenceOnly<Customer>(c => c.Id)` capture; a
`GET /api/decisions` lists what the log holds so the demo can show the trail. That is the
two-sidedness obligation met at reference-implementation strength — the *durable* sink is the next
slice (below).

## Testing

- **`RuleDocumentParserTests`** — `audited` parses, defaults false, rejects a non-boolean, and round-trips
  through the schema.
- **`RuleDocumentComparerTests`** — a document differing only in `audited` is **not** structurally equal,
  and `change.is-metadata-only` is therefore false for that change.
- **`DecisionLogTests`** — the queue: FIFO order, batching, `FailClosed` throws and the evaluation does
  not return, `Block` waits and then succeeds, `Drop` sheds and the next batch is preceded by a
  `DecisionGap` whose count matches what was shed, disposal drains.
- **`DecisionCaptureTests`** — the three strategies produce the three `DecisionInput` kinds; an audited
  rule over an unregistered model type fails to bind with `AuditCaptureNotConfigured`, at update, at
  load, and as a quarantine on refresh.
- **`DecisionRecordTests`** — the three anchors. The interesting one: publish a new version of a
  proposition two hops down the reference chain, evaluate again, and assert the record's pinned
  version moved — the transitive-closure claim of decision 2, tested rather than asserted.
- **`DecisionCorrelationTests`** — two rules under one `PinSnapshot` share a correlation id; two
  unpinned evaluations do not; the caller is carried; a nested pin does not start a second decision.
- **Async parity** — every behavioural test above runs against `AsyncRule` too. The four entry points
  are four places to forget something.

## Explicitly out of scope

- **The durable sink and the retention purge.** The bundle's app surface wants a raw-append writer
  against the separate decision database, with a mandatory retention window. That is a database, a
  schema, a background job and a migration story — the same weight as Spec 2C, which was the
  authoring store on its own. It is the next slice, and this one ships the seam it plugs into.
  Retention is *mandatory* where records are durable; an in-memory reference sink has no window to
  enforce, so the requirement travels with the durable implementation rather than being weakened here.
- **Telemetry.** `motiv.rules.decisions.dropped`, `motiv.rules.decision_queue.depth`, and the rest of
  the `motiv.rules.*` instruments are build step 3. The gap marker is here because it lives in the log;
  the counters are not.
- **The PII explanation-tag mode** (`full` / `reason-only` / `none`) on published `Motiv`. It is the one
  additive change to the core package, it wants its own branch and release, and it is coupled to
  *traces*, not to the durable log. Build step 3.
- **Replay.** The three anchors plus the captured input are what makes replay *possible*. Building the
  replayer is a fog patch the bundle graduates, not a thing this slice does.
- **Per-rule capture strategies.** Capture is keyed by model type. If a deployment ever needs two
  postures for one model, that is a follow-up, not a speculative generality shipped today.

## Risks

- **The four entry points.** `Rule.Evaluate`, `PolicyRule.Evaluate` (a shadow, not an override),
  `AsyncRule.EvaluateAsync`, `AsyncPolicyRule.EvaluateAsync` (likewise). A shadowing method is exactly
  the shape that gets missed, and a missed one means a rule that says it is audited and is not. Mitigated
  by a table-driven test that walks all four.
- **`FailClosed` turns a logging failure into an evaluation failure.** That is the intent, and it is
  also a new way for a request to fail. The queue default is sized so that reaching it means the sink is
  genuinely not draining, and the exception says so.
- **The eager projection is a real per-evaluation cost** on audited rules. Measured in the PR, and
  benchmarked against the unaudited path so the number is on the record rather than in a footnote.
- **`Audited` in the structural comparer changes an existing behaviour.** A change that toggles audit
  now classifies as a logic change rather than metadata-only. That is the intended reading, and the
  test for it is written as the specification of that reading.

## Outcome (recorded after the build)

Built as designed. Five things the build changed or settled:

**1. The cost is the projection, and the projection is not new.** Decision 3 predicted the eager
materialisation would be the dominant cost and promised to measure it. It is, and it is almost exactly
`ResultSerializer.ToEvaluationResult`'s existing cost — the log's own overhead is under a microsecond.
Rough in-process timings, Release, one warm process (not BenchmarkDotNet — the variance between runs is
tens of percent, so read the columns against each other rather than as absolutes):

| Rule | `Evaluate` | `Evaluate` + `ToEvaluationResult` | audited `Evaluate` |
|---|---|---|---|
| 1 spec | ~0.6 µs | ~5 µs | ~6 µs |
| 10-operand `And` | ~2 µs | ~69 µs | ~35–100 µs |
| 50-operand `And` | ~3 µs | ~172 µs | ~165–200 µs |

The audited column tracks the projection column, not the evaluate column. So auditing costs what
serialising a result has always cost, because that is what it stores — the sample's `/api/checkout` has
been paying it twice per request all along. The superlinearity in the middle column is pre-existing and
is very likely [#137](https://github.com/karlssberg/Motiv/issues/137)'s metadata tier, which
`RuleEvaluationResult.Values` reads; a follow-up says so rather than a new ticket claiming it as this
slice's.

**2. `Drop` fails once the log is disposed.** The design said a disposed log fails `Block` (capacity is
never coming back) but did not settle `Drop`, whose contract is that the evaluation proceeds. It fails
too: a disposed log cannot write the gap marker, so dropping there would be exactly the silent loss
`Drop` exists to avoid. Disposal is a lifecycle event, not backpressure, and the exception now says
which of the two happened.

**3. `DroppedCount` had to become monotonic.** The first version reset it when a gap was taken, which
made the property mean "drops not yet reported" while reading like a total. Now the sum of every gap
written equals it — which is also what a telemetry counter in build step 3 will want.

**4. `EvaluateAsync` could not become an `async` method.** Making it one moved the unbound-rule
exception from a synchronous throw to a faulted task, which an existing test caught. It validates
eagerly and wraps only when there is something to record — so an unaudited async rule also keeps
forwarding the underlying `ValueTask` directly, which `AsyncPolicyRule`'s own documentation promises.

**5. The sample had to audit `loyalty-discount`, not `can-checkout`.** The plan assumed `can-checkout`
and said to transcribe its default if it turned out to be compiled. It is compiled, and the more honest
demonstration was to leave it that way and audit the rule that already has a document — so the sample
shows the constraint rather than working around it.

Two duplications were removed after the fact: `Record` and the proposition-closure walk were identical
to the character across `Rule` and `AsyncRule`, and moved to an internal `DecisionRecording`. That is
not the deliberate builder-path duplication CLAUDE.md protects — those bodies really do differ; these
did not.

The mandatory `code-simplifier` pass has no such agent available in this environment, so the review was
done by hand and its findings are the two extractions above.
