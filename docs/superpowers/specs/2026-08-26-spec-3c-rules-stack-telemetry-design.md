# Spec 3C — The Rules-Stack Telemetry Surface — Design

**Date:** 2026-08-26 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** Step 3 of bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md)
§6 — *"Rules-stack telemetry surface + PII control + readiness/correlation"* — resolving ticket
[04](https://github.com/karlssberg/Motiv/issues/104). Shipped as
[#142](https://github.com/karlssberg/Motiv/pull/142). Follows
[#141](https://github.com/karlssberg/Motiv/pull/141) (Spec 3B), which left the decision-log readings
waiting for a meter to read them.

> **Written after the merge.** This slice predates the `CLAUDE.md` same-commit docs rule and is one of
> the eleven the [#169](https://github.com/karlssberg/Motiv/issues/169) ledger marks as owed. The
> decisions below are recovered from the shipped diff and its five review rounds, not proposed.
>
> **The user-facing surface is already documented** — [`docs/observability/rules-stack.md`](../../observability/rules-stack.md)
> shipped in this same PR and is the reference for every instrument, tag and enumeration value. This
> document does not restate it. It records the decisions behind it: the ones spec 3 §2 named and this
> slice ratified, and the larger number it did not name at all.

## Summary

Core `Motiv` reports what an *evaluation* decided. Nothing reported what the rules stack around it was
doing. This slice gives `Motiv.Serialization` its own activity source and meter, thirteen instruments
across four areas, a span that says which rule ran at which version, opt-in per-node spans, a readiness
health check, and — the one additive change reaching published `Motiv` — a PII posture that an adopter
states once and that applies to both durable records and ephemeral traces.

Spec 3 §2 settled the two big shapes: **two surfaces on two version trains**, and **PII coupled to
ticket 15's capture posture**. Almost everything else here was decided at build time.

## Decisions (locked)

### 1. Two sources, two version trains — ratified, not chosen

Spec 3 §2 already decided this, and the reason is worth restating because it is what the whole surface
hangs on: core `Motiv` is published and its signal names are **frozen as contract** — dashboards depend
on them — while the rules stack is 0.x and still churning. Sharing a source would tie one's stability
promise to the other's.

So both the source and the meter are named `Motiv.Serialization`, and an adopter subscribes to each
independently. Constants are exposed (`MotivRulesTelemetry.SourceName`, `.MeterName`) because a mistyped
subscription name is not an error — it is silence.

### 2. Thirteen instruments, where the spec named twelve

Spec 3 §2 lists twelve `motiv.rules.*` instruments. Twelve shipped as named. The thirteenth,
**`motiv.rules.decision_batches.failed`**, was added because Spec 3B's writer loop *survives a throwing
sink by design* — a log that silently stopped logging is the exact failure the decision log exists to
prevent — and having decided that, nothing else would ever tell an operator the sink was refusing
batches. The instrument is the other half of a decision 3B had already made.

### 3. Readings come from a weakly-held registry of live subjects

Most of these instruments are **readings**, not events: how deep the decision queue is, which generation
this replica serves, whether break-glass is on. A reading has no call site to push from, so the
observable instrument has to be handed the object that holds it.

`TelemetrySubjects<T>` is that registry, and it holds every subject **weakly**. This is the design, not a
precaution: a registry of strong references would keep every `RuleSet` a process ever built alive for as
long as the meter existed, so *merely subscribing to the meter* would turn a collectable object into a
leak. A subject that goes away simply stops being reported, which is the correct reading. `Add` returns a
handle so a subject that knows when it is finished — a disposed decision log — can say so at once rather
than wait for a collection.

### 4. `decisions.dropped` reads the log's own counter

It reports `DecisionLog.DroppedCount` directly rather than keeping a second tally beside it. That is the
same number every `DecisionGap` marker is written from, so the counter and the markers **cannot drift
apart**. A telemetry counter that disagreed with the durable gap markers would undermine both.

`QueueDepth` is new in this slice, and is the instrument worth alerting on *before* it hurts: depth
approaching `QueueCapacity` means backpressure is about to apply, and under the default `FailClosed`
posture that means audited evaluations are about to start throwing.

### 5. Break-glass reports even when it is off

Every `BreakGlass` registers with the gauge, **`BreakGlass.Off` included**, so an ordinary host reads `0`
rather than reporting nothing. Without this, "no series" is ambiguous between *the flag is off* and *this
replica's meter has stopped answering* — and those are opposite conclusions for the operator reviewing a
break-glass window.

Registering from `Off` needs a constructor body, so the positional record is written out longhand. That
carries an obligation the review round later enforced: a `with`-copy must **also** report, or a clone
would silently vanish from the gauge. The copy constructor calls `Report()` for exactly that reason.

`publishes_under_break_glass` counts one per **artefact**, not per change request, because an envelope
carrying a proposition and the rule referencing it changed two things and that is the number an operator
auditing the window wants. Only publishes that actually landed count: break-glass says the ceremony was
skipped, not that anything went live, and a stale base version still fails its own compare-and-set.

### 6. The PII ceiling is derived, monotone, and stated once

Spec 3 §2 required PII policy to be *"coupled to ticket 15's redaction posture so PII policy is set
once."* The shape of that coupling is this slice's:

- `DecisionCaptureRegistry` exposes a pure **`ExplanationCeiling`** — read it to learn what a registry
  implies without applying anything.
- **Constructing a `DecisionLog` applies it.** The adopter states a posture in one place and both the
  durable record and the ephemeral trace follow.
- **It only ever tightens.** An adopter who already chose something stricter keeps it, and the order a
  host configures things in cannot change the outcome. Where several model types carry different
  postures, the strictest wins — the setting it feeds is process-wide.
- **It never derives `ReasonOnly`.** That looks like the middle of three privacy settings and is not one:
  `Reason` is built from the same authored strings as `Assertions`, so dropping the array reduces volume
  and cost, not exposure. It stays available to set by hand for those reasons.
- **Nothing registered derives `Full`** — no statement has been made, so none is inferred.

The application is **process-wide and permanent**. There is nothing safe to restore it to. For a host,
which builds one log at startup, that is simply "configured at startup"; in a test suite it is a hazard,
and it produced this slice's hardest CI failure (see Outcome).

### 7. The rules layer parents core's span rather than versioning `SpecBase`

An operator holding a publish wants the evaluations that ran it. A `SpecBase` has no version, and giving
it one to answer that query would push a rules-stack concern into the published engine — which §2's
frozen-contract rule forbids.

So a named-rule evaluation opens `motiv.rules.evaluate`, tagged with name and version, and core's
version-agnostic `motiv.evaluate` lands *inside* it. Containment is the correlation.

There are **four entry points**, and two of them are `new` shadows: `PolicyRule.Evaluate` and
`AsyncPolicyRule.EvaluateAsync` return the narrower policy type, so they *hide* the base member rather
than override it. Instrumenting by walking the base declarations would have left every policy rule's
evaluation untraced, and silently — a shadow is invisible to exactly the sweep you would reach for.

### 8. Per-node spans carry structure, not timing

Off by default, and off by default *even for an audited rule*. When on they ride the `audited` flag
rather than having a switch of their own, so they follow the same governed, versioned decision that
turns the decision log on.

The caveat is stated everywhere the feature is: **Motiv evaluates a composition in one pass and never
times a sub-proposition**, so a node span's duration is the walk that emitted it and nothing else. What
is real is the shape. `motiv.evaluate`'s own duration is the only honest number in the waterfall.

Two consequences follow:

- **Truncation is announced.** Past `MaxNodeSpans` the walk stops and tags the evaluation span
  `motiv.rules.nodes.truncated`, because a waterfall that quietly stopped short reads as a complete
  picture of a smaller tree — worse than no picture.
- **The walk is iterative.** A result tree has no small upper bound, and a recursive walk over a deep one
  is precisely the uncatchable crash Spec 3A removed from the result-tree properties. Reintroducing one
  *inside instrumentation* — where it fires only in production, only under a listener — would be the
  worst available place for it.

### 9. Node spans are parented by `Activity.Id`, not `ActivityContext`

`Activity.Context` — and with it `SpanId` and `TraceId` — is populated **only under the W3C id format**.
.NET Framework still defaults to the older hierarchical format, where `Context` is `default`. Passing it
as each node's parent made every node span a **root** on net472: the tree an operator opened the
waterfall to see would have arrived flat, silently. `Activity.Id` is populated under both formats.

This is recorded as a decision rather than a bug fix because the naive choice is the one that reads
better and passes on Linux.

### 10. Readiness is registered, not offered, and is harsher than the refresh check

`AddMotivRules` registers a `motiv-store` health check tagged `ready`. It is registered rather than put
behind an opt-in because **a probe nobody remembered to enable is a replica that stays in rotation with
an unreachable database**.

The probe is a generation read: the cheapest thing a store can be asked that still proves the connection
works — one scalar, no rows, the same call the refresh poller already makes on a timer. A row read would
make readiness proportional to catalog size; a synthetic write would make a health check a writer.

It reports **`Unhealthy`** where the neighbouring `motiv-refresh` check reports `Degraded`, and the
asymmetry is deliberate. A replica serving an older *approved* world is still serving correctly, so
taking it out of rotation turns a stale pod into a missing pod. A replica that cannot reach its store can
neither publish nor converge and will not recover by being sent more traffic.

Both stores are probed, in turn rather than concurrently, so a failure names *which* store failed rather
than arriving as an `AggregateException`. Every exception is caught, **cancellation included**: a probe
the health endpoint cancelled for taking too long is a store that did not answer in time, which is
exactly what readiness exists to report — letting `OperationCanceledException` through would surface a
slow database as an unhandled fault in the health pipeline.

### 11. The store-call timing is a wrapper that cannot be half-written

The start/record pair around a store call was eight call sites of two statements bracketing a third. A
caller who wrote only the first had instrumented nothing — **silently, and identically to a store that
was never called**. It is now one wrapper, and it records in a `finally`, so a store that *threw* still
reports how long it took to fail. That is the latency someone diagnosing a timeout is actually looking
for.

## What this does not do

- **It does not time sub-propositions.** Decision 8; it is an engine property, not an instrumentation gap.
- **It does not read the store on a gauge callback.** `replica_lag` is measured against the generation the
  last refresh actually read, because a callback fires on the exporter's schedule and must not issue a
  round trip per collection. The consequence is stated rather than hidden: a replica whose poller has
  *stopped* reports its last known lag rather than a comforting zero.
- **It does not time `unchanged` refresh ticks.** `rebuild.duration` is recorded only when a rebuild
  happened; timing a no-op would report "no rebuild" as "a very fast rebuild" — the same number an
  operator reads as a healthy rebuild rate.
- **It does not add a `ReasonOnly` derivation.** Decision 6.
- **It does not make per-node spans the durable home of the structural tree.** That is the decision log:
  a record keeps the tree for the retention window and can be queried; a trace is sampled, dropped under
  load, and gone within days.

## Verification obligations

- Subscribing to `Motiv.Serialization` yields the thirteen instruments under their documented names and
  tags; nothing is emitted unsubscribed.
- A collected subject stops being reported and does not keep its holder alive.
- `motiv.rules.decisions.dropped` agrees with the `DecisionGap` markers, because it reads the same field.
- `BreakGlass.Off` reports `0`; a `with`-copy still reports.
- A registry naming `Redact` or `ReferenceOnly` yields an `ExplanationCeiling` of `None`; applying it only
  tightens, and is order-independent.
- A named-rule evaluation opens `motiv.rules.evaluate` carrying name and version, with `motiv.evaluate`
  beneath it — from **all four** entry points, the two shadowed ones included.
- Node spans nest under the evaluation span **on every id format**, asserted against `ParentId` and
  object identity rather than `ParentSpanId`.
- A tree past `MaxNodeSpans` truncates and tags the evaluation span.
- The readiness check reports `Unhealthy` when a store does not answer, cancellation included, and names
  which store.

## Outcome (recorded after the build)

Shipped as [#142](https://github.com/karlssberg/Motiv/pull/142): 40 files, **+3392 / −62**, across nine
commits. Ten new test files under `Diagnostics/`, plus the health-check and readiness-endpoint suites.

**One pre-existing flake found and fixed.** `DecisionLogTests` enqueued into a possibly-still-full queue
under the `Drop` posture, shedding the record it then went looking for — reproduced on *unmodified* main,
2 of 6 runs. `QueueDepth`, added here for its own reasons, made it deterministic.

**The hardest failure was decision 6's side effect, seen from a test suite.** CI failed on
`windows-latest`. Constructing a `DecisionLog` whose registry names `Redact` or `ReferenceOnly` tightens
`MotivTelemetry.ExplanationDetail` to `None` **for the rest of the process**, and four existing test
classes were doing that in parallel with two new telemetry tests that assert on explanation text. The
window is small enough to pass ten runs on a fast Linux box and lose on a slower Windows agent. The fix
is blunt on purpose — *if a class constructs a `DecisionLog`, it joins the serialized collection* —
because that is easier to keep true than a rule about which postures a log happened to be given. Proven
with a throwaway test before fixing: set `ReasonOnly`, construct a `ReferenceOnly` log, read `None` back.

**A CI change was needed to diagnose any of it.** At `--verbosity normal` the test step emits the full
`csc` command line per project per target framework — 146 KB where `minimal` emits 977 bytes, measured on
one project and one framework. GitHub serves only the *tail* of a job log, so two failing runs on this PR
were undiagnosable: the retrievable log covered the last twelve seconds of a three-minute step. Verified
that a failing test still prints its name, full assertion message and stack trace at `minimal`, by
deliberately breaking one.

**A test that could not have caught decision 9's defect.** It compared `node.ParentSpanId` to
`evaluation.SpanId` — under the hierarchical format both are all-zeros, so it asserted two defaults were
equal and passed. Only four correlation cases actually failed CI; the flat-tree defect was found by
reading *why* they failed.

**Codecov put `BreakGlass.cs` at 50% patch coverage.** The eight uncovered lines were the copy constructor
and `Deconstruct` — the two members written out by hand so the type could keep behaving like the
positional record it replaced. The PR body and the type's own doc both asserted that behaviour was
preserved and nothing tested it. Deconstruction, value equality, `with`-copying, and the clone's
reporting obligation from decision 5 are now pinned; the last was verified to have teeth by deleting the
`Report()` call and watching it fail.

**The simplification pass was done by hand** — no `code-simplifier` agent was available in that
environment. It produced decision 11's wrapper and moved the result-tree walk out to `NodeSpanWriter`,
which had been a cohesive unit sharing a file with instrument declarations and span helpers.
