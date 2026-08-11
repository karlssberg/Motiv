# Bundle Spec — Operability & Evidence

Status: draft — synthesis of resolved decisions; no new architecture.
Source tickets: [04](../issues/04-opentelemetry-contract.md) · [15](../issues/15-decision-log-record.md) · [19](../issues/19-structural-caps-and-evaluation-limits.md)

## 1. Capability

An operator can **see** what the engine is doing (telemetry), **prove** why a specific decision was
made (the decision log), and rely on evaluation being **safe at any structural scale** (no uncatchable
crash, bounded work). Two of the three already partly ship in published `Motiv`; this bundle ratifies
what exists, adds the rules-stack signals, and closes the one live crash class.

## 2. SDK surface

### Telemetry (04) — two surfaces on two version trains
- **Core `Motiv` source/meter (already shipped, now frozen as contract):**
  - Span **`motiv.evaluate`** (`ActivityKind.Internal`), one per top-level evaluation; tags
    `motiv.proposition`, `motiv.satisfied`, `motiv.reason`, `motiv.assertions`; `error.type` + an
    exception event on failure; cancellation is not an error (`motiv.cancelled` dimension).
  - **`motiv.evaluations`** (counter) and **`motiv.evaluation.duration`** (histogram, `s`).
  - `System.Diagnostics.DiagnosticSource` (in-box, **not** the OTel SDK); source+meter both `"Motiv"`,
    assembly-versioned; **zero-alloc when unsubscribed**. These names are now public API — snapshotted,
    never renamed; new core signals additive only.
- **New `Motiv.Serialization` source/meter (rules stack, own train):** `motiv.rules.bind_failures`,
  `motiv.rules.publish_conflicts` (21's 409s), `motiv.rules.store.duration` (09),
  `motiv.rules.catalog.size`, `motiv.rules.generation` + `motiv.rules.replica_lag` (20),
  `motiv.rules.refreshes` + `motiv.rules.rebuild.duration` (20),
  `motiv.rules.decisions.dropped` + `motiv.rules.decision_queue.depth` (15's backpressure visibility),
  `motiv.rules.break_glass.active` + a publishes-under-break-glass counter (14).
- **Granularity**: one span per evaluation by default; **per-node spans are opt-in and ride the
  `audited` flag**, off by default even then — the structural tree's durable home is the decision log,
  not the trace waterfall.
- **PII control (the one additive change to published `Motiv`)**: an explanation-tag mode
  `full`/`reason-only`/`none`, **coupled to ticket 15's redaction posture** so PII policy is set once.
  Default `full` (backward-compatible; subscription is already opt-in), but documented loudly —
  assertion text is PII *iff* authored to template model data (`model => $"income is {model.Income}"`).

### The decision log (15)
- **Opt-in per rule via an `audited` flag on the rule document** — so it is versioned (10) and toggling
  it is a governed `ChangeRequest` (13). Placing the flag on the document *forces* "audited ⟹ stored
  document" for free (a compiled-default rule has no document to hold the flag).
- **Record**: `(Id, CorrelationId, TimestampUtc, Caller, RuleName, RuleVersion, BuildId,
  ReferencedPropositionVersions, Input?, RuleEvaluationResult)`. The existing
  `RuleEvaluationResult<string>` (Satisfied/Reason/Assertions/Values/Justification/Explanation) is the
  payload; the envelope is new. Behaviour is reconstructable from the **three anchors** (document +
  build + proposition versions).
- **Input capture is an adopter-chosen seam** — `StoreWhole` (dev) / `Redact(projection)` /
  `ReferenceOnly(keySelector)` (GDPR-clean, recommended prod). Enabling `audited` **requires** a choice;
  no silent whole-model default. The strategy sets the **replay ceiling**.
- **Off the hot path**: `IDecisionSink` fed by a bounded channel + background writer; **`FailClosed` by
  default** (an audited decision that couldn't be logged didn't happen); `Block` and `Drop` are
  configurable but `Drop` **never silent** — a gap-marker + a telemetry counter. The in-memory channel
  is a bounded crash-loss window; true zero-loss is a durable adopter sink.
- **Separate database** (16), raw-append (not EF). **Retention is mandatory** — adopter-set window,
  background purge; a record past the window can't be replayed (correct post-retention state).

### Structural safety (19)
- **One stack-safe traversal the public result-tree properties delegate to**, differential-tested
  against the current recursive code as a **perfect oracle** on randomly generated (incl.
  short-circuited) trees at safe depths — converts the short-circuit-fold risk into a checked invariant.
- **Single-dispatch, not a visitor**: child-selection is already the abstract `Causes` /
  `CausesWithValues` / `Underlying` / `UnderlyingWithValues` virtuals (memoized in
  `BinaryBooleanResult`); the ~9 recursive base-class bodies + `AssertionExtensions` helpers swap to one
  iterative driver consuming those virtuals. No `Accept`/`Visit`, no node-type changes.
- Fixes `UnderlyingMetadataSources`' missing memoization as a by-product.
- `MaxCompositionDepth` kept but **re-derived against result-tree size and raised** above 256.
- Allocation: a reused, closure-free working stack (poolable, `clearArray:true` if ever pooled); do not
  pool retained result arrays; measure before pooling. A **result-size bound counted in the traversal
  loop** replaces the crash that used to cap the amplification finding.

## 3. App surface (`Motiv.Studio`)

- Health/**readiness = the store answers `GetGenerationAsync()`** (16); the EF connection is the probe
  target.
- The durable `IDecisionSink` implementation writing raw-append to the separate decision DB, with the
  retention purge job.
- **Authoring→evaluation correlation**: the rules-stack layer tags *named-rule* evaluation spans with
  `motiv.rules.name` + `motiv.rules.version`, so an operator pivots from a publish to the evaluations
  that ran the new version (core stays version-agnostic).

## 4. Invariants (must hold)

- Every public result-tree property behaves identically at every depth (uniform stack-safety) — no
  "what fired but not why" asymmetry.
- An audited decision is either logged or fails (FailClosed) — a dropped record is always visible.
- PII posture is set once and applies to both the durable decision log and ephemeral traces.
- Telemetry attribute names never change once shipped (dashboards depend on them).
- The decision log is a separate database from the authoring store.

## 5. New machinery to build

- The stack-safe iterative traversal + the oracle differential test; `CausalChildren`-style consumption
  of existing virtuals.
- The `Motiv.Serialization` OTel source/meter + all `motiv.rules.*` instruments.
- The explanation-tag PII control (additive, published `Motiv`).
- The `audited` document field; `DecisionRecord` + envelope; the input-capture strategies; the
  bounded-channel `IDecisionSink`; the retention purge.

## 6. Build sequence

1. Stack-safe traversal + oracle test (19) — closes the one published crash class.
2. `IDecisionSink` + record + input strategies + FailClosed channel (15).
3. Rules-stack telemetry surface + PII control + readiness/correlation (04).

## 7. Verification obligations

- The traversal returns identical output to the recursive oracle on generated short-circuited trees;
  a 100k-deep composition no longer crashes and is bounded by the result-size limit.
- An audited rule records a full `DecisionRecord`; a `Drop` under load emits a gap-marker + increments
  `motiv.rules.decisions.dropped`.
- `ReferenceOnly` capture + subject erasure in the SoR leaves a non-PII decision record and makes replay
  correctly impossible.
- Subscribing to the `Motiv` source yields `motiv.evaluate` spans with the outcome; `none` mode omits
  `motiv.reason`/`motiv.assertions`.
