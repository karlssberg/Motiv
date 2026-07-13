# Production Observability (OpenTelemetry) — Design

**Date:** 2026-07-13
**Status:** Approved design, pending implementation plan

## Context

Motiv results already explain themselves — `Satisfied`, `Reason`, `Assertions`,
`Justification`. But that explanation dies inside the process unless the caller
manually bridges it into their logs. In production, the questions that matter —
*why was this request rejected?*, *which proposition is slow?*, *how often does
this rule fire?* — are unanswerable without that bridge.

This is the second of four planned capability initiatives:

1. ~~Async propositions~~ — shipped (#71); changed the evaluation model
2. **Production observability** (this spec) — OpenTelemetry integration
3. Rule externalization (serialization of rules/results)
4. Compile-time power (source generators, AOT)

Observability follows async because the instrumented boundary must cover the
final evaluation model — sync and async — rather than be retrofitted to it.

## Goals

| Goal | Signal that serves it |
|---|---|
| Decision auditing — "why did this get rejected?" | Span tags: outcome, reason, assertions |
| Performance diagnosis — where does evaluation time go? | Span duration + duration histogram |
| Rule analytics — hit rates, drift over time | Evaluation counter tagged by proposition and outcome |
| Debugging composition | Root span carries the full decomposed reason; per-node spans are a designed-for future escalation |

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Packaging | Instrument inside core `Motiv` | `ActivitySource`/`Meter` are in-box on net8/9/10; no package seam between sync, async, and higher-order paths |
| Dependency | `System.Diagnostics.DiagnosticSource`, conditioned on `netstandard2.0` only | net8/9/10 consumers gain nothing new; the OpenTelemetry SDK is never referenced |
| Opt-in | Listener-driven; no Motiv API | Nothing is emitted unless something subscribes. The user's only step is `.AddSource("Motiv")` / `.AddMeter("Motiv")` in their existing OTel setup |
| Granularity | One root span per top-level `Evaluate` / `EvaluateAsync` | The result already carries its own decomposed explanation; N spans per node would be chatty and costly for no added information |
| Root-only enforcement | Composites re-route to an internal, uninstrumented evaluation entry | Deterministic and free for inner nodes, unlike an ambient `Activity.Current` guard |
| Entry points | `Evaluate` / `EvaluateAsync` only | `Matches` / `MatchesAsync` are the zero-allocation boolean-only fast paths (#70); a caller who chose them opted out of explanation, and telemetry follows that intent |
| Errors in metrics | `error.type` attribute on the existing instruments | OTel semantic conventions; keeps error rate queryable without trace-sampling loss and adds no third instrument |

## Instrumented Boundary

The public evaluation methods become the instrumentation boundary. Composite and
higher-order propositions — which today re-enter the *public* `Evaluate` on their
operands (e.g. `AndSpec.cs:44`) — are switched to a new `internal` entry that
calls the abstract evaluation method directly and emits nothing.

| Public boundary (instrumented) | Internal entry (silent) |
|---|---|
| `SpecBase<TModel>.Evaluate` | `EvaluateInternal` |
| `SpecBase<TModel, TMetadata>.Evaluate` | `EvaluateInternal` |
| `PolicyBase<TModel, TMetadata>.Evaluate` | `EvaluatePolicyInternal` |
| `AsyncSpecBase<TModel, TMetadata>.EvaluateAsync` | `EvaluateSpecAsyncInternal` |
| `AsyncPolicyBase<TModel, TMetadata>.EvaluateAsync` | `EvaluatePolicyAsyncInternal` |

Consequences:

- A deeply composed proposition emits **exactly one** span per top-level
  evaluation, regardless of tree depth.
- Every composite, higher-order, and decorator call site that today calls
  `.Evaluate(model)` on an operand must be migrated. This is mechanical but
  broad (~20 files); per the CLAUDE.md batch-refactoring note, verify the
  changed-file set with `git diff --stat` against the planned set.
- `Matches` / `MatchesAsync` are untouched — they neither emit telemetry nor
  route through the instrumented boundary.
- Escalating to per-node child spans later is a change localised to the internal
  entry. That is the reason the boundary is a method rather than an ambient check.

A new internal static `Motiv/Diagnostics/MotivTelemetry.cs` owns the
`ActivitySource`, the `Meter`, the two instruments, and the record helpers.

## Span Schema

- **Source name:** `Motiv`
- **Activity name:** `motiv.evaluate` (deliberately low-cardinality; the
  proposition goes on a tag, not in the name)
- **Kind:** `Internal`

| Tag | Type | Value |
|---|---|---|
| `motiv.proposition` | string | The proposition's statement (`Description.Statement`) |
| `motiv.satisfied` | bool | The result's `Satisfied` |
| `motiv.reason` | string | The result's `Reason` |
| `motiv.assertions` | string[] | The result's `Assertions` |
| `error.type` | string | Exception type name; present only when evaluation threw |

Assertion and reason text is model-derived and may carry sensitive data. That is
accepted by default — decision auditing is the primary goal — and users who deem
it sensitive strip it with a standard OTel processor or span filter. Motiv itself
exposes no configuration for this (see *Opt-in* above).

## Metric Schema

- **Meter name:** `Motiv`

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `motiv.evaluations` | `Counter<long>` | `{evaluation}` | `motiv.proposition`, `motiv.satisfied`, `error.type` (on failure) |
| `motiv.evaluation.duration` | `Histogram<double>` | `s` | `motiv.proposition`, `motiv.satisfied`, `error.type` (on failure) |

Cardinality is bounded by the propositions declared in code; statements are
static text, including composed ones (`"(a & b) | !c"`).

## Errors and Cancellation

- The instrumented boundary wraps evaluation in `try`/`catch`. On exception it
  sets span status `Error`, records the exception event, tags `error.type`, and
  records both metric instruments with the `error.type` dimension.
- The exception is **rethrown unwrapped** — telemetry never changes propagation
  semantics. This matches the async design's "predicate exceptions propagate
  exactly as sync predicate exceptions propagate — no additional wrapping".
- A cancelled `EvaluateAsync` surfaces `OperationCanceledException` and is
  recorded like any other failure (`error.type=System.OperationCanceledException`).

## Cost When Nobody Is Listening

- `ActivitySource.StartActivity()` returns `null` when no listener has subscribed
  — no `Activity` is allocated.
- The duration timestamp is taken only when the histogram reports `Enabled`.
- Result-derived tags (`Reason`, `Assertions`) are materialised **only** inside
  the `activity is not null` branch, so an unobserved evaluation never forces the
  work that the perf initiative (#67–#69) removed.
- Net cost of an unobserved `Evaluate`: one null check plus the instruments'
  `Enabled` flags.

This must be **demonstrated by benchmark**, not asserted — the existing
BenchmarkDotNet suite gains a no-listener case compared against the current
baseline.

## Testing Strategy

- **Harness:** `ActivityListener` and `MeterListener` capture emitted signals in
  tests; no OpenTelemetry SDK is referenced by the test project either.
- **Root-only:** a deeply composed proposition (nested `&`, `|`, `!`, higher-order)
  emits exactly one span. Regression-proofs the internal-entry migration.
- **Silence:** `Matches` / `MatchesAsync` emit no span and no metric;
  no evaluation emits anything when no listener is attached.
- **Tag fidelity:** span tags equal the returned result's `Satisfied`, `Reason`,
  and `Assertions` — telemetry never invents its own explanation text.
- **Async parity:** async, lifted-sync, and `*Concurrently` compositions produce
  telemetry identical to their sync equivalents, mirroring the async equivalence
  matrix. Concurrency is an evaluation detail, never a telemetry one.
- **Errors:** predicate exceptions and cancellation set span status `Error`,
  tag `error.type`, record both instruments, and rethrow unwrapped.
- **Perf:** no-listener benchmark shows no allocation or throughput regression.
- Full solution suite green, including example projects; `netstandard2.0` builds
  with the conditioned dependency and all other TFMs gain none.

## Out of Scope (v1)

- **Per-node child spans** — the internal evaluation entry is the designed hook;
  escalation can be added without re-plumbing the boundary.
- **A Motiv-owned configuration API** — filtering, redaction, and sampling are
  OTel's job.
- **`ILogger` / log signal** — spans and metrics only.
- **Baggage propagation** and cross-service context beyond what `Activity`
  already does ambiently.
- **Expression-tree-specific tags** (`Spec.From` clause decomposition on the span).
- Telemetry on `Matches` / `MatchesAsync`.
