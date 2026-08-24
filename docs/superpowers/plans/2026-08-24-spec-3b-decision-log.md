# Spec 3B — The Decision Log — Implementation Plan

**Design:** [2026-08-24-spec-3b-decision-log-design.md](../specs/2026-08-24-spec-3b-decision-log-design.md)
**Ticket:** [15](https://github.com/karlssberg/Motiv/issues/115)

## Global constraints

- **Every `dotnet` command** runs against net10.0 (`-f net10.0`) for filtered runs. `netstandard2.0`
  must still *build* — the new queue uses `System.Threading.Channels`, which needs a package reference
  on that TFM only.
- **TDD throughout.** Failing test → confirm it fails for the right reason → minimum code → green.
- **Additive only.** No existing signature changes except the two the design names: `RuleSet`'s ctors
  gain an optional trailing parameter, and `DecisionSnapshot` gains properties. Nothing that already
  compiles stops compiling.
- **Four entry points, every time.** `Rule.Evaluate`, `PolicyRule.Evaluate`, `AsyncRule.EvaluateAsync`,
  `AsyncPolicyRule.EvaluateAsync`. Two are shadows, not overrides. Any behaviour added to one is
  tested against all four.
- **Run the whole solution at the end.** Per CLAUDE.md the example projects assert justification
  strings; this slice does not change them, which is a claim to be checked rather than assumed.

## File structure

```
src/Motiv.Serialization/Decisions/DecisionRecord.cs                (new)
src/Motiv.Serialization/Decisions/DecisionGap.cs                   (new)
src/Motiv.Serialization/Decisions/IDecisionSink.cs                 (new)
src/Motiv.Serialization/Decisions/DecisionLog.cs                   (new)
src/Motiv.Serialization/Decisions/DecisionLogOptions.cs            (new)
src/Motiv.Serialization/Decisions/DecisionBackpressure.cs          (new)
src/Motiv.Serialization/Decisions/DecisionCaptureRegistry.cs       (new)
src/Motiv.Serialization/Decisions/DecisionInput.cs                 (new)
src/Motiv.Serialization/Decisions/DecisionNotLoggedException.cs    (new)
src/Motiv.Serialization/Decisions/InMemoryDecisionSink.cs          (new)
src/Motiv.Serialization/Decisions/ResultProjection.cs              (new, internal)
src/Motiv.Serialization/RuleDocument.cs                            (Audited)
src/Motiv.Serialization/RuleDocumentParser.cs                      (parse "audited")
src/Motiv.Serialization/RuleErrorCode.cs                           (AuditCaptureNotConfigured)
src/Motiv.Serialization/ResultSerializer.cs                        (delegate to ResultProjection)
src/Motiv.Serialization/Governance/RuleDocumentComparer.cs         (compare Audited)
src/Motiv.Serialization/Propositions/DecisionSnapshot.cs           (CorrelationId, Caller, Current)
src/Motiv.Serialization/Propositions/DependencyGraph.cs            (ReferenceClosure)
src/Motiv.Serialization/Rules/Rule.cs, AsyncRule.cs                (State.Audited, the pin, recording)
src/Motiv.Serialization/Rules/RuleBase.cs                          (internal DecisionLog)
src/Motiv.Serialization/Rules/RuleSet.cs                           (ctor parameter, PinSnapshot overload)
schemas/rule.v1.json                                               ("audited")
src/Motiv.Serialization.Tests/Decisions/*                          (new suites)
src/examples/Motiv.RulesEngine.Sample/*                            (sink, capture, audited rule, endpoint)
```

---

### Task 1: `audited` on the document

1. `RuleDocumentParserTests` — `"audited": true` parses; absent defaults to false; a non-boolean is a
   `RuleError` at `$.audited` with `InvalidNode`; an unknown-property error is *not* raised for it.
2. Watch them fail (`unknown property 'audited'`).
3. `RuleDocument.Audited`, the parser case, and `"audited": { "type": "boolean" }` in
   `schemas/rule.v1.json`.
4. Green, plus the schema round-trip test the suite already runs against every fixture.

**Care:** the schema has `"additionalProperties": false`, so the schema and the parser must gain the
property together or the existing schema-conformance test fails on a document the parser accepts.

### Task 2: the comparer sees it

1. `RuleDocumentComparerTests` — two documents differing only in `audited` are **not** structurally
   equal. Then the governance-level test: a `ChangeRequest` toggling only `audited` is **not**
   `change.is-metadata-only`.
2. Watch them fail (the comparer ignores the field, so both currently report "equal"/"metadata-only").
3. One clause in `StructurallyEqual`.
4. Green. Run the whole `Governance` suite — this is a behaviour change to an existing classifier.

### Task 3: the record and the sink contract

1. `DecisionRecordTests` — construction, the envelope's shape, `PropositionVersion` equality.
   `InMemoryDecisionSinkTests` — records accumulate in order, gaps accumulate separately, both are
   readable back.
2. Watch them fail (nothing exists).
3. `DecisionRecord`, `PropositionVersion`, `DecisionGap`, `DecisionInput` + `DecisionInputKind`,
   `IDecisionSink`, `InMemoryDecisionSink`, `DecisionNotLoggedException`.
4. Green.

**Care:** `DecisionRecord.Outcome` is `RuleEvaluationResult<object?>`. Move `ResultSerializer`'s
explanation mapping into an internal `ResultProjection` used by both, so the log and the HTTP payload
cannot drift. `ResultSerializer`'s own tests must stay green untouched — that is the check that the
move was behaviour-preserving.

### Task 4: `DecisionLog` — the queue, the writer, the three postures

1. `DecisionLogTests`, with a stub sink that can be made to hang:
   - records reach the sink in enqueue order, batched no larger than `MaxBatchSize`;
   - `FailClosed` on a full queue throws `DecisionNotLoggedException` from `Enqueue`;
   - `Block` on a full queue returns once the sink drains;
   - `Drop` on a full queue returns immediately, and the next batch is **preceded** by a `DecisionGap`
     whose `DroppedCount` equals what was shed and whose timestamps bracket it;
   - `DisposeAsync` drains what is queued before returning;
   - a sink that throws does not kill the writer loop or the process.
2. Watch them fail.
3. `DecisionBackpressure`, `DecisionLogOptions` (including the internal `Clock` seam so timestamps are
   deterministic in tests), `DecisionLog`. `System.Threading.Channels` for `netstandard2.0` in the
   csproj and `Directory.Packages.props`.
4. Green.

**Care:** the gap must be emitted **once per contiguous run of drops**, not once per dropped record,
and it must be ordered ahead of the batch that follows it — a gap marker after the records it precedes
is a lie about where the hole is. Test the ordering explicitly, not just the count.

### Task 5: the capture registry, and the bind-time refusal

1. `DecisionCaptureTests` — `StoreWhole<T>` yields `DecisionInputKind.Whole` carrying the model;
   `Redact<T>(p)` yields `Redacted` carrying the projection's output; `ReferenceOnly<T>(k)` yields
   `Reference` carrying the key string; an unregistered model type yields nothing.
   Then the refusal: a rule whose document is `audited` over a model type with no registered capture
   fails to bind — at `UpdateAsync` (a rejected update with `AuditCaptureNotConfigured`), at `Load`
   (reported in the load report), and on refresh (quarantined, not fatal).
2. Watch them fail.
3. `DecisionCaptureRegistry`, `RuleErrorCode.AuditCaptureNotConfigured`, and the check wherever a rule
   binds a document. `RuleSet`'s optional `DecisionLog?` parameter and `RuleBase`'s internal handle
   assigned by `Add`.
4. Green.

**Care:** the refusal must also fire when there is *no* `DecisionLog` at all — an audited document in a
host that configured no log is the same fail-closed case with a different message, not an accident that
silently drops the flag.

### Task 6: `DecisionSnapshot` carries the decision

1. `DecisionCorrelationTests` — a `PinSnapshot(correlationId, caller)` is visible as
   `DecisionSnapshot.Current`; a nested pin does not replace it; disposal restores the outer one;
   no pin means `Current` is null. Async-flow: the ambient survives an `await`.
2. Watch them fail.
3. `CorrelationId`, `Caller`, the static `AsyncLocal` `Current`, and `RuleSet.PinSnapshot(correlationId,
   caller)` / `PropositionSet` likewise.
4. Green. Re-run the AspNetCore suite — `MotivGenerationFilter` constructs these per request.

**Care:** the existing parameterless `PinSnapshot()` must keep working and must now mint a correlation
id of its own. Nesting semantics follow the generation pin exactly: an inner pin reuses the outer
decision, and disposing the inner one does not end it.

### Task 7: the forward closure

1. `DependencyGraphTests` — `ReferenceClosure` returns a rule's direct references, then transitive ones,
   excludes the node itself, is stable under a cycle-free diamond, and terminates on a self-reference
   the cycle check would otherwise have refused.
2. Watch them fail.
3. `ReferenceClosure(NodeId)` — the forward mirror of `DependentClosure`.
4. Green.

### Task 8: recording, on all four entry points

1. `DecisionRecordingTests`, run against all four rule flavours:
   - an unaudited rule records nothing;
   - an audited rule records exactly one record per evaluation, carrying name, version, build id, the
     captured input, and the projected outcome;
   - the outcome in the record equals `ResultSerializer.ToEvaluationResult` of the same evaluation;
   - two rules under one pin share a correlation id; two unpinned evaluations do not;
   - `FailClosed` on a full queue means the *evaluation* throws — the caller gets no result.
   Then the anchor test: publish a new version of a proposition **two hops** down the chain, evaluate
   again, and assert the record's pinned version moved.
2. Watch them fail.
3. `State.Audited`, the lazy proposition pin on `State`, and the shared record-and-enqueue helper called
   from all four entry points.
4. Green, then the full `Motiv.Serialization.Tests` suite.

**Care:** the lazy pin is per-`State`, and its correctness rests on "a proposition republish rebinds
every referrer, producing a new State". The two-hop test is what proves that rather than assuming it. If
it fails, the pin becomes per-evaluation with a memo keyed on the generation sequence, and the cost is
recorded in the PR.

### Task 9: the sample app

1. `Motiv.RulesEngine.Sample.Tests` — `POST /api/checkout` against the audited `can-checkout` leaves a
   record whose input is the customer's id and nothing else; `GET /api/decisions` returns it; the
   correlation id ties the two rules of one checkout together.
2. Watch them fail.
3. `audited: true` in `can-checkout`'s default document, `InMemoryDecisionSink` +
   `ReferenceOnly<Customer>(c => c.Id)` registered in `Program.cs`, and the read endpoint behind
   `.RequireAuthorization()`.
4. Green.

**Care:** the sample's `can-checkout` currently runs on whichever default it was given. Marking it
audited means it must be on a *document* default (design decision 1), so check which it is before
editing and transcribe it if it is compiled.

### Task 10: documentation, verification, review

1. `README.md` — a short decision-log example under Core Features. `docs/` — a `docs/decision-log/`
   page set following the existing structure, plus `docs/toc.yml` and `docs/Overview.md` entries.
2. Measure the audited-vs-unaudited per-evaluation cost (design decision 3) and put the number in the
   PR body.
3. Full solution test run on net10.0, including the four example projects. `netstandard2.0` build of
   `Motiv.Serialization`.
4. The mandatory `code-simplifier` pass, its findings applied, tests re-run.
5. File the follow-ups: the durable sink + retention purge (next slice), the telemetry counters (build
   step 3), the PII explanation-tag mode (build step 3), and per-rule capture if it is ever wanted.

---

## Verification obligations (from bundle spec 3, §7)

- [x] An audited rule records a full `DecisionRecord` — verdict, justification, three anchors, input.
- [x] A `Drop` under load emits a gap marker whose count matches what was shed. *(The telemetry counter
      that must accompany it is build step 3's; the log-side half is this slice's.)*
- [x] `ReferenceOnly` capture leaves a decision record carrying no model data beyond the key, so subject
      erasure in the adopter's system of record makes replay correctly impossible.
- [x] `audited` is versioned and governed: toggling it is a document change, classified as a logic
      change rather than metadata-only.
- [x] Nothing about an unaudited rule's evaluation changes — same result, same cost.
