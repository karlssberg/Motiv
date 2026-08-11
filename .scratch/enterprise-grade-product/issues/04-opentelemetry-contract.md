# The OpenTelemetry contract — what does the SDK emit?

Type: grilling
Status: resolved
Blocked by: —

## Question

`docs/observability/` exists but the SDK emits no traces, metrics, or structured logs today. An
enterprise adopter's first question after "is it secure" is "can I see it in Datadog".

**What is the SDK's observability contract — the signals an adopter can rely on across versions?**

Sub-questions:

1. **Granularity of the evaluation span.** One span per rule evaluation, or a span per node in the
   composition tree? The tree is the interesting part — that is Motiv's whole thesis — but a deeply
   nested rule would emit dozens of spans per request on a hot path. Is per-node an opt-in
   (and does it then coincide with the `audited` flag from ticket 15)?
2. **What are the metrics?** Candidates: evaluations by rule and outcome, evaluation duration,
   bind/validate failures, publish conflicts (`409`s), store latency, catalogue size. Which of these
   are contractual, and what are the attribute names? Attribute naming is an API surface — renaming
   one later breaks dashboards.
3. **Does the justification leak into telemetry?** A justification names the assertions that fired,
   which for a rule over a `Customer` may embed PII-adjacent facts. Do spans carry assertions, and
   if so under what control? This overlaps ticket 15 — decide once, not twice.
4. **Activity source and meter names.** Stable names, versioned how, and does the SDK take a
   dependency on `System.Diagnostics.DiagnosticSource` (in-box) rather than the OTel SDK (not)?
5. **What must the app add on top?** Health and readiness probes, and correlating an authoring
   action with the evaluations that followed it.

Independent of the storage and boundary questions — this is takeable now.

## Inherited from ticket 20

Multi-instance staleness is now a first-class concept, so it should be observable. The **generation**
each replica is serving is a natural gauge, and the lag between a replica's generation and the store's
is the metric that tells an operator their replicas have diverged. Consider also a counter for
refreshes performed and rebuild duration — a rebuild parses and binds every document, so it is the
one periodic CPU cost the SDK imposes.

## Grounded in the code — the premise is stale

**The SDK already emits telemetry, and it ships in published `Motiv`.** `docs/observability/` documents
a real contract, and `src/Motiv/Diagnostics/` implements it:

- **Span `motiv.evaluate`** (`ActivityKind.Internal`), **one per top-level evaluation** — tags
  `motiv.proposition`, `motiv.satisfied`, `motiv.reason`, `motiv.assertions`; on failure
  `error.type` + an `exception` event; cancellation handled per OTel conventions (not an error, a
  `motiv.cancelled` dimension).
- **`motiv.evaluations`** (Counter) and **`motiv.evaluation.duration`** (Histogram, `s`), tagged
  proposition / satisfied / error.type / cancelled.
- **`System.Diagnostics.DiagnosticSource`** (in-box), *not* the OTel SDK; source+meter both `"Motiv"`,
  assembly-versioned; **zero-alloc when unsubscribed** (`EvaluationScope` is a struct that opens no
  activity and takes no timestamp until a listener attaches).

So the ticket flips from "design the contract" to "**ratify what ships, freeze it, add what it doesn't
cover**", and **sub-4 is already answered by the code**.

## Answer

**Two telemetry surfaces on two version trains (mirroring ticket 06): the shipped core `Motiv`
source/meter, frozen as contract; and a new `Motiv.Serialization` source/meter for authoring, store,
multi-instance and decision-sink signals. One-span-per-evaluation stays default; per-node is opt-in and
rides `audited`. The one additive change to published `Motiv` is a PII control over explanation tags,
decided once with ticket 15.**

### The live finding — assertion text is an unguarded PII surface in published Motiv

`EvaluationScope.cs:64-65` emits `motiv.reason` and `motiv.assertions` **unconditionally** once a
tracing listener attaches. Assertions are author-controlled text: an author who wrote
`model => $"income is {model.Income}"` puts a customer's income on every span, in **published v8/v9**,
with no opt-out. Not the emergency the stack-overflow was — subscription is an explicit opt-in and
traces are ephemeral, not durable like the decision log — but it is exactly the surface ticket 15
governs. **Assertion text is PII iff authored to template model data.** This ticket must *add* the
control, not merely document it (see sub-3).

### The structural decision — two surfaces, two trains (ticket 06)

- **Core `Motiv` source/meter (shipped) — frozen contract.** `motiv.evaluate`, `motiv.evaluations`,
  `motiv.evaluation.duration` and their tag names are now public API (dashboards depend on them), so
  they are snapshotted under ticket 06's approved-API discipline and never renamed. New core signals
  are additive only.
- **New `Motiv.Serialization` source/meter (rules stack, its own train).** Everything the ticket lists
  that is an authoring/store concern and does not exist in core:

| Instrument | Kind | Source ticket |
|---|---|---|
| `motiv.rules.bind_failures` | counter | this |
| `motiv.rules.publish_conflicts` | counter | 21 (the 409s) |
| `motiv.rules.store.duration` | histogram | 09 |
| `motiv.rules.catalog.size` | gauge | this |
| `motiv.rules.generation` / `motiv.rules.replica_lag` | gauge | 20 |
| `motiv.rules.refreshes` / `motiv.rules.rebuild.duration` | counter / histogram | 20 |
| `motiv.rules.decisions.dropped` / `motiv.rules.decision_queue.depth` | counter / gauge | 15 |
| `motiv.rules.break_glass.active` + publishes-under-break-glass | gauge + counter | 14 |

Namespaced `motiv.rules.*` to distinguish from core `motiv.*`; names snapshotted like core.

### Sub-1 — granularity: one span/eval default; per-node opt-in via `audited`

One span per top-level evaluation stays default (the hot-path cost of dozens of spans on a deep rule is
real). Three tiers by cost:
1. the flat `motiv.assertions` tag (shipped) — the causal tree *as a list*, for live tracing;
2. **per-node spans — opt-in, gated by `audited`** (ticket 15), and off by default even then;
3. the **decision log (15)** is the structural tree's durable home.

So per-node *does* coincide with `audited` as the ticket suspected — but the tree's primary home is the
decision log, not the trace waterfall; per-node spans are a deep-debug escape hatch, not a default.

### Sub-2 — metrics: core frozen, rules-stack enumerated

Core two are frozen (above). The rules-stack set is the table above. Attribute naming is API surface, so
every name is snapshotted; the 20-derived ones inherit ticket 20's naming. This is where ticket 15's
"backpressure policy must be visible in ticket 04's telemetry" is discharged
(`motiv.rules.decisions.dropped` + queue depth), and where ticket 21's 409s become a metric.

### Sub-3 — PII, decided once with ticket 15

Add a telemetry control over explanation-tag emission — `full` / `reason-only` / `none` — **coupled to
ticket 15's redaction posture**, so an adopter sets PII policy *once* for both the durable decision log
(input capture) and ephemeral traces (assertions). Default stays `full` (backward-compatible, and
subscription is already an explicit opt-in), but the contract documents the surface loudly and
recommends `none` / `reason-only` wherever assertions template model data. **This is the one additive
change to published `Motiv`** — additive, so non-breaking.

### Sub-4 — answered by shipped code, plus the rules-stack source

Names `Motiv` / `Motiv`, assembly-versioned; `System.Diagnostics.DiagnosticSource` (in-box, not the OTel
SDK); subscribe-to-enable, no Motiv config API. The rules stack adds a `Motiv.Serialization` source/meter
on its own train. Ratified and extended.

### Sub-5 — what the app adds

- **Health/readiness probes:** readiness = the store answers `GetGenerationAsync()` (ticket 16); the EF
  connection is the probe target.
- **Authoring→evaluation correlation:** the rules-stack layer tags *named-rule* evaluation spans with
  `motiv.rules.name` + `motiv.rules.version`, so an operator pivots from a publish to the evaluations
  that ran the new version. Core stays version-agnostic (it spans *any* proposition, not only named live
  rules), so the correlation lands in the rules-stack layer — consistent with the two-surface split.

## Downstream

- **Closes the Operability & evidence bundle** (04, 15, 19).
- **The one code change to published `Motiv`:** the additive explanation-tag PII control (sub-3) — worth
  a follow-up beyond the plan, coupled to ticket 15's redaction seam. Everything else in core is
  ratification of shipped behaviour.
- **To ticket 06:** telemetry attribute names join the approved-API snapshot; the `Motiv.Serialization`
  meter is a new rules-stack surface on the 0.x train.
- **To ticket 15:** its promised "visible in ticket 04 telemetry" backpressure signals are named here.
- **Offer:** `docs/observability/` already documents core; extending it with the `motiv.rules.*` surface
  and the PII control is a docs task for when the rules stack ships.
