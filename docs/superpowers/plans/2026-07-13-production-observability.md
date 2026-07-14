# Production Observability (OpenTelemetry) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Emit one OpenTelemetry span plus two metric instruments per top-level `Evaluate` / `EvaluateAsync`, carrying the decision's outcome, reason and assertions, without any Motiv-owned configuration API and without regressing the zero-allocation evaluation paths.

**Architecture:** A new internal `Motiv.Diagnostics` namespace owns an `ActivitySource` and `Meter` both named `"Motiv"`, and an `EvaluationScope` struct that starts/stops a span and records metrics. The public `Evaluate` / `EvaluateAsync` methods become the single instrumented boundary; every in-library operand evaluation (composites, decorators, adapters, higher-order resolvers) is re-routed to new `internal` entries that bypass instrumentation, so a deeply composed proposition emits exactly one span.

**Tech Stack:** C#, `System.Diagnostics.ActivitySource` / `System.Diagnostics.Metrics.Meter` (in-box on net8/9/10; `System.Diagnostics.DiagnosticSource` package on `netstandard2.0`), xUnit + Shouldly, BenchmarkDotNet.

**Spec:** `docs/superpowers/specs/2026-07-13-production-observability-design.md`

## Global Constraints

- The OpenTelemetry SDK is **never** referenced — not by `Motiv`, not by `Motiv.Tests`. Only BCL diagnostics primitives are used.
- The only new dependency is `System.Diagnostics.DiagnosticSource`, and it is conditioned on `'$(TargetFramework)' == 'netstandard2.0'`. net8.0 / net9.0 / net10.0 gain **no** new dependency.
- Central Package Management is in force: every `PackageReference` version lives in `Directory.Packages.props`.
- `netstandard2.0` is a target — no C# 8+ APIs that need runtime support. In particular **do not use `Stopwatch.GetElapsedTime`** (net7+); compute elapsed seconds as `(Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency`.
- Telemetry is **listener-driven**: no public Motiv API is added. Users opt in with `.AddSource("Motiv")` / `.AddMeter("Motiv")` in their own OTel setup.
- Telemetry never alters behaviour: exceptions are rethrown **unwrapped**, results are returned unchanged, and no evaluation result text is invented — span tags copy the result's own `Satisfied`, `Reason`, `Assertions`.
- `Matches` / `MatchesAsync` emit nothing and must not route through the instrumented boundary.
- Fixed names — copy verbatim:
  - ActivitySource / Meter name: `Motiv`
  - Activity name: `motiv.evaluate`, `ActivityKind.Internal`
  - Span tags: `motiv.proposition`, `motiv.satisfied`, `motiv.reason`, `motiv.assertions`, `error.type`
  - Instruments: `motiv.evaluations` (`Counter<long>`, unit `{evaluation}`), `motiv.evaluation.duration` (`Histogram<double>`, unit `s`)
- Full solution suite must be green, including the example projects (`Motiv.Poker.Tests`, `Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests`).

## File Structure

| File | Responsibility |
|---|---|
| `src/Motiv/Diagnostics/MotivTelemetry.cs` (new) | Owns the `ActivitySource`, `Meter`, both instruments, and the `IsEnabled` gate. Nothing else. |
| `src/Motiv/Diagnostics/EvaluationScope.cs` (new) | A `readonly struct` spanning one evaluation: `Start` → (`Complete` \| `Fail`). Holds the span, the start timestamp, and the proposition statement. |
| `src/Motiv/SpecBase.cs` (modify) | `SpecBase<TModel, TMetadata>.Evaluate` becomes the sync boundary; adds `internal EvaluateInternal`. |
| `src/Motiv/PolicyBase.cs` (modify) | `PolicyBase.Evaluate` becomes the policy boundary; adds `internal EvaluatePolicyInternal`. |
| `src/Motiv/AsyncSpecBase.cs` (modify) | `AsyncSpecBase<TModel, TMetadata>.EvaluateAsync` becomes the async boundary; adds `internal EvaluateSpecAsyncInternal`. |
| `src/Motiv/AsyncPolicyBase.cs` (modify) | `AsyncPolicyBase.EvaluateAsync` becomes the async policy boundary; adds `internal EvaluatePolicyAsyncInternal`. |
| ~50 composite/decorator/adapter/higher-order files (modify) | Operand evaluation re-routed to the internal entries. Enumerated in Tasks 2 and 3. |
| `src/Motiv/Motiv.csproj`, `Directory.Packages.props` (modify) | The `netstandard2.0`-conditioned dependency. |
| `src/Motiv.Tests/Diagnostics/TelemetryHarness.cs` (new) | `ActivityListener` + `MeterListener` capture harness used by every telemetry test. |
| `src/Motiv.Tests/Diagnostics/*Tests.cs` (new) | Span shape, root-only, silence, error, async-parity tests. |
| `src/examples/Motiv.Benchmark/EvaluationBenchmarks.cs` (modify) | No-listener benchmark case. |
| `README.md`, `docs/observability/*`, `docs/toc.yml`, `docs/Overview.md` (modify/new) | User-facing documentation. |

**Boundary rule (memorise — every task depends on it):** *public `Evaluate`/`EvaluateAsync` = instrumented; everything inside the library calls the `*Internal` entries.* The one deliberate exception is `EnumerableExtensions.Where` (`src/Motiv/EnumerableExtensions.cs:32`), which stays on public `Evaluate` — it is a user-facing API and each model there is its own decision, so one span per model is correct.

---

### Task 1: Telemetry primitives

Creates the `ActivitySource`, `Meter`, instruments and the `EvaluationScope` struct. Nothing is wired into evaluation yet — this task ends with primitives that are unit-tested in isolation.

**Files:**
- Create: `src/Motiv/Diagnostics/MotivTelemetry.cs`
- Create: `src/Motiv/Diagnostics/EvaluationScope.cs`
- Modify: `Directory.Packages.props`
- Modify: `src/Motiv/Motiv.csproj`
- Create: `src/Motiv.Tests/Diagnostics/TelemetryHarness.cs`
- Test: `src/Motiv.Tests/Diagnostics/EvaluationScopeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `internal static class Motiv.Diagnostics.MotivTelemetry` with `internal const string SourceName = "Motiv"`, `internal static readonly ActivitySource ActivitySource`, `internal static readonly Counter<long> Evaluations`, `internal static readonly Histogram<double> Duration`, `internal static bool IsEnabled { get; }`
  - `internal readonly struct Motiv.Diagnostics.EvaluationScope` with `internal static EvaluationScope Start(string proposition)`, `internal void Complete(BooleanResultBase result)`, `internal void Fail(Exception exception)`
  - `internal sealed class TelemetryHarness : IDisposable` (test-side) with `IReadOnlyList<Activity> Activities`, `IReadOnlyList<Measurement> Measurements`, and record `Measurement(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags)`

- [ ] **Step 1: Add the package version entry**

In `Directory.Packages.props`, under the `<!-- System packages -->` comment, add:

```xml
    <PackageVersion Include="System.Diagnostics.DiagnosticSource" Version="10.0.2" />
```

Verify that version exists on the feed before continuing:

Run: `dotnet package search System.Diagnostics.DiagnosticSource --exact-match --format json`
Expected: the listed versions include `10.0.2`. If not, use the newest stable `10.0.x` and use that version everywhere below.

- [ ] **Step 2: Reference it from Motiv, netstandard2.0 only**

In `src/Motiv/Motiv.csproj`, add a new `ItemGroup` next to the existing `<PackageReference Include="Converj" />` group:

```xml
    <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
        <PackageReference Include="System.Diagnostics.DiagnosticSource" />
    </ItemGroup>
```

Run: `dotnet build src/Motiv/Motiv.csproj`
Expected: build succeeds for all four TFMs, 0 warnings.

- [ ] **Step 3: Write the test harness**

Create `src/Motiv.Tests/Diagnostics/TelemetryHarness.cs`. This is infrastructure, not a test — it captures whatever the `Motiv` source and meter emit while it is alive.

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Motiv.Tests.Diagnostics;

internal sealed class TelemetryHarness : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly List<Activity> _activities = [];
    private readonly List<Measurement> _measurements = [];

    public TelemetryHarness()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Motiv",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Add(activity)
        };

        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Motiv")
                    listener.EnableMeasurementEvents(instrument);
            }
        };

        _meterListener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) => Capture(instrument, measurement, tags));
        _meterListener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) => Capture(instrument, measurement, tags));

        _meterListener.Start();
    }

    public IReadOnlyList<Activity> Activities => _activities;

    public IReadOnlyList<Measurement> Measurements => _measurements;

    public Activity SingleActivity() => _activities.Single();

    public Measurement SingleMeasurement(string instrument) =>
        _measurements.Single(measurement => measurement.Instrument == instrument);

    private void Capture(
        Instrument instrument,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copied = new Dictionary<string, object?>();
        foreach (var tag in tags)
            copied[tag.Key] = tag.Value;

        _measurements.Add(new Measurement(instrument.Name, value, copied));
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }

    internal sealed record Measurement(
        string Instrument,
        double Value,
        IReadOnlyDictionary<string, object?> Tags);
}
```

- [ ] **Step 4: Write the failing tests for the primitives**

Create `src/Motiv.Tests/Diagnostics/EvaluationScopeTests.cs`:

```csharp
using System.Diagnostics;
using Motiv.Diagnostics;

namespace Motiv.Tests.Diagnostics;

public class EvaluationScopeTests
{
    [Fact]
    public void Should_emit_no_activity_when_nothing_is_listening()
    {
        var result = Spec.Build((int n) => n % 2 == 0).Create("is even").Evaluate(2);

        var scope = EvaluationScope.Start("is even");
        scope.Complete(result);

        Activity.Current.ShouldBeNull();
        MotivTelemetry.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Should_report_enabled_when_a_listener_is_attached()
    {
        using var harness = new TelemetryHarness();

        MotivTelemetry.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Should_tag_a_completed_scope_with_the_results_own_explanation()
    {
        // The result is produced BEFORE the harness exists: from Task 2 onwards Evaluate is itself
        // instrumented, and an evaluation inside the harness would add a second, unrelated activity.
        var result = Spec.Build((int n) => n % 2 == 0).Create("is even").Evaluate(3);

        using var harness = new TelemetryHarness();

        var scope = EvaluationScope.Start("is even");
        scope.Complete(result);

        var activity = harness.SingleActivity();
        activity.OperationName.ShouldBe("motiv.evaluate");
        activity.Kind.ShouldBe(ActivityKind.Internal);
        activity.GetTagItem("motiv.proposition").ShouldBe("is even");
        activity.GetTagItem("motiv.satisfied").ShouldBe(false);
        activity.GetTagItem("motiv.reason").ShouldBe("is even == false");
        activity.GetTagItem("motiv.assertions").ShouldBe(new[] { "is even == false" });
        activity.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Fact]
    public void Should_record_both_instruments_on_a_completed_scope()
    {
        // Produced before the harness — see the note above.
        var result = Spec.Build((int n) => n % 2 == 0).Create("is even").Evaluate(2);

        using var harness = new TelemetryHarness();

        var scope = EvaluationScope.Start("is even");
        scope.Complete(result);

        var count = harness.SingleMeasurement("motiv.evaluations");
        count.Value.ShouldBe(1);
        count.Tags["motiv.proposition"].ShouldBe("is even");
        count.Tags["motiv.satisfied"].ShouldBe(true);
        count.Tags.ShouldNotContainKey("error.type");

        var duration = harness.SingleMeasurement("motiv.evaluation.duration");
        duration.Value.ShouldBeGreaterThanOrEqualTo(0);
        duration.Tags["motiv.proposition"].ShouldBe("is even");
    }

    [Fact]
    public void Should_mark_a_failed_scope_with_the_error_type()
    {
        using var harness = new TelemetryHarness();

        var scope = EvaluationScope.Start("is even");
        scope.Fail(new InvalidOperationException("boom"));

        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
        activity.Events.ShouldContain(activityEvent => activityEvent.Name == "exception");
        activity.GetTagItem("motiv.satisfied").ShouldBeNull();

        var count = harness.SingleMeasurement("motiv.evaluations");
        count.Tags["error.type"].ShouldBe("System.InvalidOperationException");
        count.Tags.ShouldNotContainKey("motiv.satisfied");
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~EvaluationScopeTests" -f net10.0`
Expected: build FAILS — `The type or namespace name 'Diagnostics' does not exist in the namespace 'Motiv'`.

- [ ] **Step 6: Write MotivTelemetry**

Create `src/Motiv/Diagnostics/MotivTelemetry.cs`:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Motiv.Diagnostics;

/// <summary>
/// Owns Motiv's OpenTelemetry primitives. Nothing is emitted unless a listener subscribes to the
/// <c>Motiv</c> activity source or meter, so instrumentation is inert by default.
/// </summary>
internal static class MotivTelemetry
{
    /// <summary>The name of both the activity source and the meter. Users subscribe with this name.</summary>
    internal const string SourceName = "Motiv";

    /// <summary>The name given to every evaluation activity.</summary>
    internal const string ActivityName = "motiv.evaluate";

    internal static readonly ActivitySource ActivitySource =
        new(SourceName, typeof(MotivTelemetry).Assembly.GetName().Version?.ToString());

    private static readonly Meter Meter =
        new(SourceName, typeof(MotivTelemetry).Assembly.GetName().Version?.ToString());

    internal static readonly Counter<long> Evaluations =
        Meter.CreateCounter<long>(
            "motiv.evaluations",
            "{evaluation}",
            "The number of proposition evaluations.");

    internal static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>(
            "motiv.evaluation.duration",
            "s",
            "The duration of proposition evaluations.");

    /// <summary>
    /// Gets a value indicating whether anything is listening. When <c>false</c>, evaluation must take the
    /// uninstrumented path — no activity, no timestamp, no result inspection.
    /// </summary>
    internal static bool IsEnabled =>
        ActivitySource.HasListeners() || Evaluations.Enabled || Duration.Enabled;
}
```

- [ ] **Step 7: Write EvaluationScope**

Create `src/Motiv/Diagnostics/EvaluationScope.cs`:

```csharp
using System.Diagnostics;

namespace Motiv.Diagnostics;

/// <summary>
/// Spans a single top-level evaluation. Created by <see cref="Start" />, then terminated by exactly one of
/// <see cref="Complete" /> or <see cref="Fail" />. A struct so that an unobserved evaluation allocates nothing.
/// </summary>
internal readonly struct EvaluationScope(Activity? activity, long startTimestamp, string proposition)
{
    /// <summary>Starts a scope, opening an activity if (and only if) something is listening.</summary>
    /// <param name="proposition">The propositional statement being evaluated.</param>
    /// <returns>A scope that must be terminated with <see cref="Complete" /> or <see cref="Fail" />.</returns>
    internal static EvaluationScope Start(string proposition)
    {
        var activity = MotivTelemetry.ActivitySource
            .StartActivity(MotivTelemetry.ActivityName, ActivityKind.Internal);

        activity?.SetTag("motiv.proposition", proposition);

        var startTimestamp = MotivTelemetry.Duration.Enabled ? Stopwatch.GetTimestamp() : 0L;

        return new EvaluationScope(activity, startTimestamp, proposition);
    }

    /// <summary>Terminates the scope with a successful evaluation, tagging the result's own explanation.</summary>
    /// <param name="result">The result produced by the evaluation.</param>
    internal void Complete(BooleanResultBase result)
    {
        if (activity is not null)
        {
            activity.SetTag("motiv.satisfied", result.Satisfied);
            activity.SetTag("motiv.reason", result.Reason);
            activity.SetTag("motiv.assertions", result.Assertions.ToArray());
            activity.Dispose();
        }

        Record(result.Satisfied, errorType: null);
    }

    /// <summary>Terminates the scope with a failed evaluation. The exception itself is rethrown by the caller.</summary>
    /// <param name="exception">The exception that escaped the evaluation.</param>
    internal void Fail(Exception exception)
    {
        var errorType = exception.GetType().FullName;

        if (activity is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag("error.type", errorType);
            activity.AddEvent(new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    { "exception.type", errorType },
                    { "exception.message", exception.Message },
                    { "exception.stacktrace", exception.ToString() }
                }));
            activity.Dispose();
        }

        Record(satisfied: null, errorType);
    }

    private void Record(bool? satisfied, string? errorType)
    {
        var countEnabled = MotivTelemetry.Evaluations.Enabled;
        var durationEnabled = MotivTelemetry.Duration.Enabled;

        if (!countEnabled && !durationEnabled) return;

        var tags = new TagList { { "motiv.proposition", proposition } };

        if (satisfied.HasValue)
            tags.Add("motiv.satisfied", satisfied.Value);

        if (errorType is not null)
            tags.Add("error.type", errorType);

        if (countEnabled)
            MotivTelemetry.Evaluations.Add(1, tags);

        if (durationEnabled)
            MotivTelemetry.Duration.Record(ElapsedSeconds(startTimestamp), tags);
    }

    private static double ElapsedSeconds(long startTimestamp) =>
        (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~EvaluationScopeTests" -f net10.0`
Expected: PASS, 5 tests.

Then confirm the legacy target still builds with the conditioned dependency:

Run: `dotnet build src/Motiv/Motiv.csproj -f netstandard2.0`
Expected: build succeeds, 0 warnings.

- [ ] **Step 9: Commit**

```bash
git add Directory.Packages.props src/Motiv/Motiv.csproj src/Motiv/Diagnostics src/Motiv.Tests/Diagnostics
git commit -m "feat: add Motiv activity source, meter and evaluation scope"
```

---

### Task 2: Instrument the synchronous boundary

Makes `SpecBase<TModel, TMetadata>.Evaluate` and `PolicyBase.Evaluate` the instrumented boundary, adds the silent internal entries, and migrates **every** in-library sync operand call site onto them. The root-only guarantee is what the tests here defend.

**Files:**
- Modify: `src/Motiv/SpecBase.cs:203`
- Modify: `src/Motiv/PolicyBase.cs:25`
- Modify: the sync call sites enumerated in Step 4
- Test: `src/Motiv.Tests/Diagnostics/SyncEvaluationTelemetryTests.cs`

**Interfaces:**
- Consumes: `MotivTelemetry.IsEnabled`, `EvaluationScope.Start/Complete/Fail` (Task 1).
- Produces:
  - `internal BooleanResultBase<TMetadata> SpecBase<TModel, TMetadata>.EvaluateInternal(TModel model)`
  - `internal PolicyResultBase<TMetadata> PolicyBase<TModel, TMetadata>.EvaluatePolicyInternal(TModel model)`

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Tests/Diagnostics/SyncEvaluationTelemetryTests.cs`:

```csharp
using System.Diagnostics;

namespace Motiv.Tests.Diagnostics;

public class SyncEvaluationTelemetryTests
{
    [Fact]
    public void Should_emit_exactly_one_span_for_a_deeply_composed_proposition()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var isPositive = Spec.Build((int n) => n > 0).Create("is positive");
        var isSmall = Spec.Build((int n) => n < 100).Create("is small");
        var composed = (isEven & isPositive).AndAlso(!isSmall | isEven);

        composed.Evaluate(4);

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity().GetTagItem("motiv.proposition").ShouldBe(composed.Description.Statement);
    }

    [Fact]
    public void Should_emit_exactly_one_span_for_a_higher_order_proposition()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var allEven = Spec.Build(isEven).AsAllSatisfied().Create("all even");

        allEven.Evaluate([2, 4, 6]);

        harness.Activities.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_tag_the_span_with_the_results_own_explanation()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var result = isEven.Evaluate(3);

        var activity = harness.SingleActivity();
        activity.GetTagItem("motiv.satisfied").ShouldBe(result.Satisfied);
        activity.GetTagItem("motiv.reason").ShouldBe(result.Reason);
        activity.GetTagItem("motiv.assertions").ShouldBe(result.Assertions.ToArray());
    }

    [Fact]
    public void Should_emit_one_span_per_policy_evaluation()
    {
        using var harness = new TelemetryHarness();

        var policy = Spec
            .Build((int n) => n % 2 == 0)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("is even");

        policy.Evaluate(2);

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity().GetTagItem("motiv.satisfied").ShouldBe(true);
    }

    [Fact]
    public void Should_emit_nothing_for_Matches()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var composed = isEven & Spec.Build((int n) => n > 0).Create("is positive");

        composed.Matches(2).ShouldBeTrue();

        harness.Activities.ShouldBeEmpty();
        harness.Measurements.ShouldBeEmpty();
    }

    [Fact]
    public void Should_record_both_instruments_once_per_evaluation()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

        isEven.Evaluate(2);
        isEven.Evaluate(3);

        harness.Measurements
            .Count(measurement => measurement.Instrument == "motiv.evaluations")
            .ShouldBe(2);
        harness.Measurements
            .Count(measurement => measurement.Instrument == "motiv.evaluation.duration")
            .ShouldBe(2);
    }

    [Fact]
    public void Should_emit_one_span_per_model_when_filtering_a_collection()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

        _ = new[] { 1, 2, 3 }.Where(isEven).ToList();

        harness.Activities.Count.ShouldBe(3);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~SyncEvaluationTelemetryTests" -f net10.0`
Expected: FAIL. `Should_emit_exactly_one_span_for_a_deeply_composed_proposition` fails with `Activities.Count` being 0 (nothing is instrumented yet), and `Should_emit_nothing_for_Matches` passes vacuously.

- [ ] **Step 3: Instrument the two sync boundaries**

In `src/Motiv/SpecBase.cs`, add `using Motiv.Diagnostics;` to the usings, then replace the body of `Evaluate` (currently line 203, `public new BooleanResultBase<TMetadata> Evaluate(TModel model) => EvaluateSpec(model);`) with:

```csharp
    public new BooleanResultBase<TMetadata> Evaluate(TModel model)
    {
        if (!MotivTelemetry.IsEnabled) return EvaluateSpec(model);

        var scope = EvaluationScope.Start(Description.Statement);
        try
        {
            var result = EvaluateSpec(model);
            scope.Complete(result);
            return result;
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
            throw;
        }
    }

    /// <summary>
    /// Evaluates the proposition without emitting telemetry. Used by composite, decorator and higher-order
    /// propositions when evaluating their operands, so that a composed proposition emits a single span at its
    /// root rather than one per node.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    internal BooleanResultBase<TMetadata> EvaluateInternal(TModel model) => EvaluateSpec(model);
```

In `src/Motiv/PolicyBase.cs`, add `using Motiv.Diagnostics;`, then replace `public new PolicyResultBase<TMetadata> Evaluate(TModel model) => EvaluatePolicy(model);` (line 25) with:

```csharp
    public new PolicyResultBase<TMetadata> Evaluate(TModel model)
    {
        if (!MotivTelemetry.IsEnabled) return EvaluatePolicy(model);

        var scope = EvaluationScope.Start(Description.Statement);
        try
        {
            var result = EvaluatePolicy(model);
            scope.Complete(result);
            return result;
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
            throw;
        }
    }

    /// <summary>
    /// Evaluates the policy without emitting telemetry. Used by composite, decorator and higher-order
    /// propositions when evaluating their operands, so that a composed proposition emits a single span at its
    /// root rather than one per node.
    /// </summary>
    /// <param name="model">The model to evaluate the policy against.</param>
    /// <returns>A result containing the metadata instance and the boolean result.</returns>
    internal PolicyResultBase<TMetadata> EvaluatePolicyInternal(TModel model) => EvaluatePolicy(model);
```

Leave `SpecBase<TModel>.Evaluate` (line 67) **unchanged** — it delegates to the generic `Evaluate`, which is now instrumented. Instrumenting it too would produce two nested spans.

- [ ] **Step 4: Migrate every sync operand call site**

Mechanical rule: inside `src/Motiv`, an evaluation of *another spec/policy that this type holds as an operand* becomes `EvaluateInternal` (when the local is used as a `BooleanResultBase<TMetadata>`) or `EvaluatePolicyInternal` (when it is used as a `PolicyResultBase<TMetadata>`).

Spec-typed sites — replace `.Evaluate(` with `.EvaluateInternal(`:

- `src/Motiv/And/AndSpec.cs:44,45`
- `src/Motiv/And/ExpressionAndSpec.cs:54,55`
- `src/Motiv/AndAlso/AndAlsoSpec.cs:32,37`
- `src/Motiv/AndAlso/ExpressionAndAlsoSpec.cs:54,59`
- `src/Motiv/Or/OrSpec.cs:45,46`
- `src/Motiv/Or/ExpressionOrSpec.cs:55,56`
- `src/Motiv/OrElse/OrElseSpec.cs:45,51`
- `src/Motiv/OrElse/ExpressionOrElseSpec.cs:55,61`
- `src/Motiv/XOr/XOrSpec.cs:29,30`
- `src/Motiv/XOr/ExpressionXOrSpec.cs:40,41`
- `src/Motiv/Not/NotSpec.cs:26`
- `src/Motiv/Not/ExpressionNotSpec.cs:34`
- `src/Motiv/ChangeModelType/ChangeModelTypeSpec.cs:15`
- `src/Motiv/DecoratorProposition/MinimalSpecDecoratorProposition.cs:18`
- `src/Motiv/DecoratorProposition/SpecDecoratorProposition.cs:20`
- `src/Motiv/DecoratorProposition/SpecDecoratorMultiMetadataProposition.cs:20`
- `src/Motiv/DecoratorProposition/SpecDecoratorMultiAssertionExplanationProposition.cs:25`
- `src/Motiv/DecoratorProposition/SpecDecoratorWithSingleTrueAssertionProposition.cs:20`
- `src/Motiv/ExpressionTreeProposition/ExpressionSpecDecorator.cs:19`
- `src/Motiv/ExpressionTreeProposition/ExpressionTreeTransformer.cs:206,207,209`
- `src/Motiv/MetadataToExplanationAdapter/MetadataToExplanationAdapterSpec.cs:17`
- `src/Motiv/Spec.cs:45,92`
- `src/Motiv/SyncToAsyncAdapter/SyncSpecAsyncAdapter.cs:53`
- `src/Motiv/Tap/TapSpec.cs:18`
- `src/Motiv/Tap/TapWhenTrueSpec.cs:18`
- `src/Motiv/Tap/TapWhenFalseSpec.cs:18`

Policy-typed sites — replace `.Evaluate(` with `.EvaluatePolicyInternal(`:

- `src/Motiv/OrElse/OrElsePolicy.cs:33,37`
- `src/Motiv/OrElse/ExpressionOrElsePolicy.cs:41,45`
- `src/Motiv/Not/NotPolicy.cs:26`
- `src/Motiv/Not/ExpressionNotPolicy.cs:33`
- `src/Motiv/ChangeModelType/ChangeModelTypePolicy.cs:18`
- `src/Motiv/DecoratorProposition/MinimalPolicyDecoratorProposition.cs:18`
- `src/Motiv/DecoratorProposition/PolicyDecoratorProposition.cs:20`
- `src/Motiv/DecoratorProposition/PolicyDecoratorMultiMetadataProposition.cs:20`
- `src/Motiv/DecoratorProposition/PolicyDecoratorMultiAssertionExplanationProposition.cs:25`
- `src/Motiv/DecoratorProposition/PolicyDecoratorWithSingleTrueAssertionProposition.cs:20`
- `src/Motiv/ExpressionTreeProposition/ExpressionPolicyDecorator.cs:19`
- `src/Motiv/Policy.cs:52`
- `src/Motiv/SyncToAsyncAdapter/SyncPolicyAsyncAdapter.cs:54`

Higher-order factories pass the evaluation as a **method group** (26 sites across 18 files under `src/Motiv/HigherOrderProposition/PropositionBuilders/`). Replace `spec.Evaluate,` with `spec.EvaluateInternal,` and `policy.Evaluate,` with `policy.EvaluatePolicyInternal,`. Find them with:

```bash
grep -rn "spec\.Evaluate,\|policy\.Evaluate," src/Motiv/HigherOrderProposition/PropositionBuilders
```

If the resolver parameter's type is `Func<TModel, PolicyResultBase<TMetadata>>`, `EvaluatePolicyInternal` is the right method group; if it is `Func<TModel, BooleanResultBase<TMetadata>>`, `EvaluateInternal` is. The compiler will tell you if you pick wrong.

**Do not change** `src/Motiv/EnumerableExtensions.cs:32` — `Where` is public API and each model is its own decision, so one span per model is intended (`Should_emit_one_span_per_model_when_filtering_a_collection` asserts this).

- [ ] **Step 5: Verify no sync operand call site was missed**

```bash
grep -rn "\.Evaluate(model\|\.Evaluate(modelSelector\|spec\.Evaluate,\|policy\.Evaluate," src/Motiv
```

Expected output: **only** `src/Motiv/EnumerableExtensions.cs:32` and the two boundary methods' own `Description`-free bodies (which no longer match this pattern). Anything else is a missed call site.

Also confirm the changed-file set matches the plan:

```bash
git diff --stat | tail -1
```
Expected: ~45 files changed (2 boundaries + 39 call-site files + 1 test file).

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~SyncEvaluationTelemetryTests" -f net10.0`
Expected: PASS, 7 tests.

Then the whole suite, because the call-site migration touched every composition path:

Run: `dotnet test Motiv.slnx`
Expected: all tests pass across every TFM and every example project.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv src/Motiv.Tests
git commit -m "feat: instrument synchronous evaluation with a single root span"
```

---

### Task 3: Instrument the asynchronous boundary

Same shape as Task 2, on the async hierarchy. The async boundary must keep the unobserved path free of an added async state machine, so the instrumented path lives in a separate `private async` method.

**Files:**
- Modify: `src/Motiv/AsyncSpecBase.cs:290`
- Modify: `src/Motiv/AsyncPolicyBase.cs:27`
- Modify: the async call sites enumerated in Step 3
- Test: `src/Motiv.Tests/Diagnostics/AsyncEvaluationTelemetryTests.cs`

**Interfaces:**
- Consumes: `MotivTelemetry.IsEnabled`, `EvaluationScope` (Task 1).
- Produces:
  - `internal Task<BooleanResultBase<TMetadata>> AsyncSpecBase<TModel, TMetadata>.EvaluateSpecAsyncInternal(TModel model, CancellationToken cancellationToken)`
  - `internal Task<PolicyResultBase<TMetadata>> AsyncPolicyBase<TModel, TMetadata>.EvaluatePolicyAsyncInternal(TModel model, CancellationToken cancellationToken)`

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Tests/Diagnostics/AsyncEvaluationTelemetryTests.cs`:

```csharp
namespace Motiv.Tests.Diagnostics;

public class AsyncEvaluationTelemetryTests
{
    [Fact]
    public async Task Should_emit_exactly_one_span_for_a_composed_async_proposition()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");
        var isPositive = Spec.BuildAsync((int n) => Task.FromResult(n > 0)).Create("is positive");
        var composed = isEven.AndAlso(isPositive);

        await composed.EvaluateAsync(4);

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity().GetTagItem("motiv.proposition").ShouldBe(composed.Description.Statement);
    }

    [Fact]
    public async Task Should_emit_exactly_one_span_when_a_sync_proposition_is_lifted_into_an_async_composition()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var isPositive = Spec.BuildAsync((int n) => Task.FromResult(n > 0)).Create("is positive");
        var composed = isPositive.And(isEven);

        await composed.EvaluateAsync(4);

        harness.Activities.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_emit_the_same_telemetry_for_concurrent_and_sequential_composition()
    {
        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");
        var isPositive = Spec.BuildAsync((int n) => Task.FromResult(n > 0)).Create("is positive");

        using var sequentialHarness = new TelemetryHarness();
        await isEven.And(isPositive).EvaluateAsync(4);
        var sequential = sequentialHarness.SingleActivity();
        var sequentialTags = new
        {
            Satisfied = sequential.GetTagItem("motiv.satisfied"),
            Reason = sequential.GetTagItem("motiv.reason"),
            Assertions = sequential.GetTagItem("motiv.assertions")
        };
        sequentialHarness.Dispose();

        using var concurrentHarness = new TelemetryHarness();
        await isEven.AndConcurrently(isPositive).EvaluateAsync(4);
        var concurrent = concurrentHarness.SingleActivity();

        concurrent.GetTagItem("motiv.satisfied").ShouldBe(sequentialTags.Satisfied);
        concurrent.GetTagItem("motiv.reason").ShouldBe(sequentialTags.Reason);
        concurrent.GetTagItem("motiv.assertions").ShouldBe(sequentialTags.Assertions);
    }

    [Fact]
    public async Task Should_emit_one_span_per_async_policy_evaluation()
    {
        using var harness = new TelemetryHarness();

        var policy = Spec
            .BuildAsync((int n) => Task.FromResult(n % 2 == 0))
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("is even");

        await policy.EvaluateAsync(2);

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity().GetTagItem("motiv.satisfied").ShouldBe(true);
    }

    [Fact]
    public async Task Should_emit_nothing_for_MatchesAsync()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");

        (await isEven.MatchesAsync(2)).ShouldBeTrue();

        harness.Activities.ShouldBeEmpty();
        harness.Measurements.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~AsyncEvaluationTelemetryTests" -f net10.0`
Expected: FAIL — `Activities.Count` is 0 for the composed-async test.

- [ ] **Step 3: Instrument the two async boundaries**

In `src/Motiv/AsyncSpecBase.cs`, add `using Motiv.Diagnostics;`, then replace the `EvaluateAsync` at line 290 with:

```csharp
    public new Task<BooleanResultBase<TMetadata>> EvaluateAsync(
        TModel model,
        CancellationToken cancellationToken = default) =>
        MotivTelemetry.IsEnabled
            ? EvaluateInstrumentedAsync(model, cancellationToken)
            : EvaluateSpecAsync(model, cancellationToken);

    private async Task<BooleanResultBase<TMetadata>> EvaluateInstrumentedAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var scope = EvaluationScope.Start(Description.Statement);
        try
        {
            var result = await EvaluateSpecAsync(model, cancellationToken).ConfigureAwait(false);
            scope.Complete(result);
            return result;
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously evaluates the proposition without emitting telemetry. Used by composite and adapter
    /// propositions when evaluating their operands, so that a composed proposition emits a single span at its root.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    internal Task<BooleanResultBase<TMetadata>> EvaluateSpecAsyncInternal(
        TModel model,
        CancellationToken cancellationToken) =>
        EvaluateSpecAsync(model, cancellationToken);
```

The ternary matters: when nothing is listening, `EvaluateAsync` still returns the underlying task directly, with no extra state machine — identical to today's cost.

In `src/Motiv/AsyncPolicyBase.cs`, add `using Motiv.Diagnostics;`, then replace the `EvaluateAsync` at line 27 with:

```csharp
    public new Task<PolicyResultBase<TMetadata>> EvaluateAsync(
        TModel model,
        CancellationToken cancellationToken = default) =>
        MotivTelemetry.IsEnabled
            ? EvaluateInstrumentedPolicyAsync(model, cancellationToken)
            : EvaluatePolicyAsync(model, cancellationToken);

    private async Task<PolicyResultBase<TMetadata>> EvaluateInstrumentedPolicyAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var scope = EvaluationScope.Start(Description.Statement);
        try
        {
            var result = await EvaluatePolicyAsync(model, cancellationToken).ConfigureAwait(false);
            scope.Complete(result);
            return result;
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously evaluates the policy without emitting telemetry. Used by composite and adapter
    /// propositions when evaluating their operands, so that a composed proposition emits a single span at its root.
    /// </summary>
    /// <param name="model">The model to evaluate the policy against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    internal Task<PolicyResultBase<TMetadata>> EvaluatePolicyAsyncInternal(
        TModel model,
        CancellationToken cancellationToken) =>
        EvaluatePolicyAsync(model, cancellationToken);
```

Leave `AsyncSpecBase<TModel>.EvaluateAsync` (line 42) unchanged — it delegates to the generic overload, which is now instrumented.

- [ ] **Step 4: Migrate every async operand call site**

Replace `.EvaluateAsync(model, cancellationToken)` with `.EvaluateSpecAsyncInternal(model, cancellationToken)` (or `.EvaluatePolicyAsyncInternal(...)` where the local is used as a `PolicyResultBase<TMetadata>`) at:

- `src/Motiv/And/AsyncAndSpec.cs:72,73,78,79`
- `src/Motiv/AndAlso/AsyncAndAlsoSpec.cs:44,49`
- `src/Motiv/Or/AsyncOrSpec.cs:74,75,80,81`
- `src/Motiv/OrElse/AsyncOrElseSpec.cs:62,68`
- `src/Motiv/OrElse/AsyncOrElsePolicy.cs:61,67` → policy entry
- `src/Motiv/XOr/AsyncXOrSpec.cs:70,71,76,77`
- `src/Motiv/Not/AsyncNotSpec.cs:29`
- `src/Motiv/Not/AsyncNotPolicy.cs:27` → policy entry
- `src/Motiv/AsyncSpec.cs:45,92`
- `src/Motiv/MetadataToExplanationAdapter/AsyncMetadataToExplanationAdapterSpec.cs:20`

Note the `cancellationToken` argument is positional on the internal entries (no default), so pass it explicitly.

- [ ] **Step 5: Verify no async operand call site was missed**

```bash
grep -rn "\.EvaluateAsync(" src/Motiv
```

Expected output: only the boundary definitions in `AsyncSpecBase.cs` (lines 42–47, which delegate) — no operand call sites remain.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~AsyncEvaluationTelemetryTests" -f net10.0`
Expected: PASS, 5 tests.

Run: `dotnet test Motiv.slnx`
Expected: all tests pass — in particular the existing async suites (`MixedSyncAsyncCompositionTests`) must be untouched by this change.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv src/Motiv.Tests
git commit -m "feat: instrument asynchronous evaluation with a single root span"
```

---

### Task 4: Errors and cancellation

The boundaries already call `scope.Fail`. This task proves the semantics the spec promises: status `Error`, an `error.type` dimension on both instruments, an exception event on the span, and the original exception rethrown **unwrapped**.

**Files:**
- Test: `src/Motiv.Tests/Diagnostics/EvaluationErrorTelemetryTests.cs`
- Modify (only if a test fails): `src/Motiv/Diagnostics/EvaluationScope.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–3. Adds no new API.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Tests/Diagnostics/EvaluationErrorTelemetryTests.cs`:

```csharp
using System.Diagnostics;

namespace Motiv.Tests.Diagnostics;

public class EvaluationErrorTelemetryTests
{
    [Fact]
    public void Should_mark_the_span_as_errored_and_rethrow_the_original_exception()
    {
        using var harness = new TelemetryHarness();

        var throws = Spec
            .Build((int _) => throw new InvalidOperationException("boom"))
            .Create("throws");

        var exception = Should.Throw<InvalidOperationException>(() => throws.Evaluate(1));
        exception.Message.ShouldBe("boom");

        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
        activity.GetTagItem("motiv.satisfied").ShouldBeNull();
    }

    [Fact]
    public void Should_record_the_error_type_on_both_instruments()
    {
        using var harness = new TelemetryHarness();

        var throws = Spec
            .Build((int _) => throw new InvalidOperationException("boom"))
            .Create("throws");

        Should.Throw<InvalidOperationException>(() => throws.Evaluate(1));

        harness.SingleMeasurement("motiv.evaluations")
            .Tags["error.type"].ShouldBe("System.InvalidOperationException");
        harness.SingleMeasurement("motiv.evaluation.duration")
            .Tags["error.type"].ShouldBe("System.InvalidOperationException");
    }

    [Fact]
    public async Task Should_record_cancellation_as_an_error()
    {
        using var harness = new TelemetryHarness();
        using var cancellation = new CancellationTokenSource();

        var slow = Spec
            .BuildAsync(async (int _, CancellationToken token) =>
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
                return true;
            })
            .Create("slow");

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await slow.EvaluateAsync(1, cancellation.Token));

        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.OperationCanceledException");
    }

    [Fact]
    public void Should_attribute_the_error_to_the_root_proposition_not_the_failing_operand()
    {
        using var harness = new TelemetryHarness();

        var throws = Spec
            .Build((int _) => throw new InvalidOperationException("boom"))
            .Create("throws");
        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var composed = isEven & throws;

        Should.Throw<InvalidOperationException>(() => composed.Evaluate(2));

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity()
            .GetTagItem("motiv.proposition")
            .ShouldBe(composed.Description.Statement);
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~EvaluationErrorTelemetryTests" -f net10.0`
Expected: PASS, 4 tests — Tasks 1–3 already implement this behaviour, and these tests exist to lock it down.

If `Should_record_cancellation_as_an_error` reports `error.type` as `System.Threading.Tasks.TaskCanceledException`, that is still correct behaviour (it derives from `OperationCanceledException`); relax the assertion to `activity.GetTagItem("error.type")!.ToString().ShouldContain("CanceledException")` rather than changing production code.

- [ ] **Step 3: Commit**

```bash
git add src/Motiv.Tests
git commit -m "test: lock down error and cancellation telemetry semantics"
```

---

### Task 5: Prove the unobserved path did not regress

The spec's central perf claim — an unobserved `Evaluate` costs one null check plus two `Enabled` flags — must be demonstrated, not asserted.

**Files:**
- Modify: `src/examples/Motiv.Benchmark/EvaluationBenchmarks.cs`
- Test: `src/Motiv.Tests/Diagnostics/TelemetryDisabledTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–3. Adds no new API.

- [ ] **Step 1: Write the failing test**

Create `src/Motiv.Tests/Diagnostics/TelemetryDisabledTests.cs`. This asserts the *observable* half of the claim: with no listener, nothing is emitted and no `Activity` is left on the ambient context.

```csharp
using System.Diagnostics;

namespace Motiv.Tests.Diagnostics;

public class TelemetryDisabledTests
{
    [Fact]
    public void Should_not_start_an_activity_when_nothing_is_listening()
    {
        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var isPositive = Spec.Build((int n) => n > 0).Create("is positive");

        (isEven & isPositive).Evaluate(2).Satisfied.ShouldBeTrue();

        Activity.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Should_not_start_an_activity_asynchronously_when_nothing_is_listening()
    {
        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");

        (await isEven.EvaluateAsync(2)).Satisfied.ShouldBeTrue();

        Activity.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_start_emitting_as_soon_as_a_listener_attaches_and_stop_when_it_detaches()
    {
        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

        using (var harness = new TelemetryHarness())
        {
            isEven.Evaluate(2);
            harness.Activities.Count.ShouldBe(1);
        }

        using var second = new TelemetryHarness();
        isEven.Evaluate(2);
        second.Activities.Count.ShouldBe(1);
    }
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~TelemetryDisabledTests" -f net10.0`
Expected: PASS, 3 tests.

These tests are order-sensitive if another test's listener leaks. If they fail only in a full-suite run, the cause is a `TelemetryHarness` that was not disposed — fix the leaking test, not these.

- [ ] **Step 3: Add the benchmark case**

Open `src/examples/Motiv.Benchmark/EvaluationBenchmarks.cs`, read the existing benchmark shape, and add a benchmark that evaluates a composed proposition with **no listener attached**, named `EvaluateComposed_NoListener`, following the file's existing conventions (`[Benchmark]`, existing `[GlobalSetup]` field construction). Do not add a listener-attached benchmark — the point is the unobserved cost.

- [ ] **Step 4: Run the benchmark against the pre-change baseline**

```bash
git stash
dotnet run -c Release --project src/examples/Motiv.Benchmark -- --filter "*Evaluate*"
git stash pop
dotnet run -c Release --project src/examples/Motiv.Benchmark -- --filter "*Evaluate*"
```

Expected: allocation column unchanged (0 B for the paths that were 0 B before), and mean within noise. A non-zero allocation delta means the `MotivTelemetry.IsEnabled` gate is being bypassed somewhere — find the boundary that starts a scope unconditionally.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Tests src/examples/Motiv.Benchmark
git commit -m "test: prove unobserved evaluation emits nothing and does not regress"
```

---

### Task 6: Documentation

Per CLAUDE.md: user-facing features go in `README.md` (brief) and `docs/` (detailed, with toc entries).

**Files:**
- Modify: `README.md`
- Create: `docs/observability/index.md`
- Create: `docs/observability/toc.yml`
- Modify: `docs/toc.yml`
- Modify: `docs/Overview.md`

**Interfaces:**
- Consumes: the final emitted names from Tasks 1–3. No code changes.

- [ ] **Step 1: Read the existing docs structure**

Read `docs/toc.yml` and one existing feature folder (e.g. `docs/` subfolder with `index.md` + `toc.yml`) so the new page matches the established structure exactly.

- [ ] **Step 2: Write the observability page**

Create `docs/observability/index.md` covering, with real code:

- Enabling: the user's own OTel setup calls `.AddSource("Motiv")` and `.AddMeter("Motiv")`. Show a complete `builder.Services.AddOpenTelemetry()...` snippet.
- What is emitted: one `motiv.evaluate` span per top-level `Evaluate`/`EvaluateAsync`, with the tag table (`motiv.proposition`, `motiv.satisfied`, `motiv.reason`, `motiv.assertions`, `error.type`).
- The two instruments and their tags.
- That `Matches` / `MatchesAsync` emit nothing, and why (they compute no explanation).
- That composed propositions emit **one** span, not one per operand.
- That assertion and reason text is model-derived, may carry sensitive data, and is stripped with a standard OTel processor if that is unwanted. Show the processor snippet.
- That `netstandard2.0` consumers pick up `System.Diagnostics.DiagnosticSource`; other targets gain no dependency.

- [ ] **Step 3: Wire up the table of contents**

Create `docs/observability/toc.yml` and add the corresponding entry to `docs/toc.yml` and a line to `docs/Overview.md`, following the existing pattern in those files.

- [ ] **Step 4: Add the README entry**

Under Core Features in `README.md`, add a short observability entry with the two-line opt-in snippet:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Motiv"))
    .WithMetrics(metrics => metrics.AddMeter("Motiv"));
```

- [ ] **Step 5: Verify the docs build**

Run: `dotnet build Motiv.slnx && dotnet test Motiv.slnx`
Expected: green — no code changed, but the example projects assert on justification strings and must remain unaffected.

- [ ] **Step 6: Commit**

```bash
git add README.md docs
git commit -m "docs: document OpenTelemetry integration"
```

---

### Task 7: Post-implementation review

Mandatory per CLAUDE.md — do not skip.

- [ ] **Step 1: Spawn the code-simplifier agent**

Dispatch a `code-simplifier` agent over the changed files (`git diff --stat main`), focusing on: duplication between the four boundary methods (sync spec, sync policy, async spec, async policy — they share a shape; judge whether consolidating is worth the generic indirection on a hot path, or whether the codebase's stated "avoid over-DRYing" preference wins), and on the `EvaluationScope` struct's responsibilities.

- [ ] **Step 2: Apply what it finds, re-run the affected tests**

Run: `dotnet test Motiv.slnx`
Expected: green.

- [ ] **Step 3: Commit any changes**

```bash
git add -A
git commit -m "refactor: simplify telemetry boundary per review"
```
