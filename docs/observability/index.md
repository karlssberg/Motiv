---
title: Observability
description: Documentation for Motiv's OpenTelemetry integration — the motiv.evaluate span emitted per top-level evaluation, the motiv.evaluations and motiv.evaluation.duration instruments, and how to enable them.
---

Motiv owns an `ActivitySource` and a `Meter`, both named `Motiv`, that report what every top-level evaluation
decided and why. Nothing is emitted unless something subscribes to them &mdash; there is no Motiv configuration
API to turn this on or off. Enabling it is entirely a matter of registering `"Motiv"` with your own OpenTelemetry
setup.

## Enabling

Add `.AddSource("Motiv")` to tracing and `.AddMeter("Motiv")` to metrics wherever you configure OpenTelemetry:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Motiv")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("Motiv")
        .AddOtlpExporter());
```

(Requires the `OpenTelemetry.Extensions.Hosting` and `OpenTelemetry.Exporter.OpenTelemetryProtocol` packages, or
swap `AddOtlpExporter()` for `AddConsoleExporter()` during local development.) Until something subscribes, Motiv
takes an uninstrumented path: no activity is started, no timestamp is taken, and nothing is allocated beyond what
the evaluation itself already does.

## What Gets Traced

Every top-level `Evaluate()` / `EvaluateAsync()` call opens exactly one activity named `motiv.evaluate`
(`ActivityKind.Internal`) &mdash; regardless of how deeply the proposition being evaluated is composed. A
proposition built from dozens of operators via `&`, `AndAlso()`, higher-order collection logic, and so on still
produces a single span rooted at the call to `Evaluate()`; Motiv does not emit a span per operand.

```csharp
var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
var isPositive = Spec.Build((int n) => n > 0).Create("is positive");
var composed = (isEven & isPositive).AndAlso(isEven.Not());

composed.Evaluate(4); // one "motiv.evaluate" span, however composed `composed` is
```

The span is tagged with the result's own explanation:

| Tag                 | Description                                          | Present |
|----------------------|-------------------------------------------------------|---------|
| `motiv.proposition`  | The propositional statement being evaluated            | Always |
| `motiv.satisfied`    | The boolean outcome (`Result.Satisfied`)               | On success |
| `motiv.reason`       | `Result.Reason`                                        | On success |
| `motiv.assertions`   | `Result.Assertions`, as a string array                 | On success |
| `error.type`         | The full type name of an exception that escaped evaluation | On failure |

On failure, the span status is set to `Error` and an `exception` event is added (`exception.type`,
`exception.message`, `exception.stacktrace`), then the original exception is rethrown unchanged; `motiv.satisfied`,
`motiv.reason`, and `motiv.assertions` are not set for a failed evaluation.

### Collections: One Span Per Model

Filtering a collection with [`Where()`](../collections/generic/Where.md) evaluates the proposition once per model,
and each of those evaluations is its own top-level decision &mdash; so it emits its own `motiv.evaluate` span:

```csharp
var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

var evens = new[] { 1, 2, 3 }.Where(isEven).ToList(); // three "motiv.evaluate" spans, one per model
```

### `Matches` Emits Nothing

`Matches()` and `MatchesAsync()` compute no explanation &mdash; no `Reason`, no `Assertions`, nothing to tag a span
with &mdash; so they emit no span and record no metric. A caller who chose the boolean-only fast path has already
opted out of the explanation this telemetry reports.

## Metrics

Alongside the span, every evaluation (again: `Matches`/`MatchesAsync` excluded) records to two instruments:

| Instrument                  | Type              | Unit          | Tags |
|-------------------------------|-------------------|---------------|------|
| `motiv.evaluations`          | `Counter<long>`   | `{evaluation}` | `motiv.proposition`, `motiv.satisfied` (success only), `error.type` (failure only) |
| `motiv.evaluation.duration`  | `Histogram<double>` | `s`         | `motiv.proposition`, `motiv.satisfied` (success only), `error.type` (failure only) |

Both instruments are gated the same way as the span: recording is skipped entirely unless something is listening.

## Sensitive Data

`motiv.reason` and `motiv.assertions` are derived from your model and your proposition's assertion text &mdash;
they can carry data you don't want leaving the process (customer names, account numbers, anything a predicate's
explanation happens to mention). Motiv exposes no redaction knob by design: there's already a standard place to
do this, and duplicating it would just be a second, less flexible version of the same mechanism. Strip or hash
the tags you don't want exported with an ordinary OpenTelemetry processor:

```csharp
public sealed class RedactMotivAssertionsProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        if (activity.Source.Name != "Motiv") return;

        activity.SetTag("motiv.reason", null);
        activity.SetTag("motiv.assertions", null);
    }
}
```

```csharp
tracing.AddProcessor(new RedactMotivAssertionsProcessor());
```

## Dependencies and Cost

On `netstandard2.0`, Motiv brings in `System.Diagnostics.DiagnosticSource` to supply the `Activity`/`ActivitySource`
types. On `net8.0`, `net9.0`, and `net10.0`, those types are already part of the shared framework, so targeting
those TFMs adds no new dependency at all. The OpenTelemetry SDK itself is never referenced by Motiv &mdash; it's
entirely your application's choice whether and how to collect what Motiv emits.

With nothing listening, instrumentation costs nothing measurable: across all 22 benchmarks shared between the
pre-telemetry baseline and this instrumentation, the allocation delta is zero, byte for byte.

Enabling tracing (an `ActivityListener` subscribed to `"Motiv"`) forces `motiv.reason` and `motiv.assertions` to be
resolved on every evaluation, which for an unnamed explanation proposition means its `WhenTrue`/`WhenFalse`
delegates run. Keep those delegates pure: one that counts calls, writes a log line, or populates a cache runs zero
times with only metrics attached (or nothing attached at all), and once per evaluation the moment a tracing
listener is added.

## Next Steps

- Read about [building propositions](../builder/index.md) and [`EvaluateAsync()`](../async/EvaluateAsync.md), the
  calls this telemetry wraps.
- See [`Where()`](../collections/generic/Where.md) for the collection-filtering path that emits one span per model.
