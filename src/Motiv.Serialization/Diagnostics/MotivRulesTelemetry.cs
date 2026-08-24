using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Motiv.Serialization;

/// <summary>
/// Owns the rules stack's OpenTelemetry primitives — the <c>motiv.rules.*</c> signals covering
/// authoring, storage, replication and the decision log. Nothing is emitted unless a listener
/// subscribes, so instrumentation is inert by default.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately separate from core <c>Motiv</c>'s <c>MotivTelemetry</c>, which owns
/// <c>motiv.evaluate</c> and the evaluation instruments. The two are on different version trains:
/// core is published and frozen as contract, the rules stack is 0.x and still churning, and sharing
/// a source would tie one's stability promise to the other's. An operator wanting both subscribes to
/// both.
/// </para>
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(tracing => tracing
///         .AddSource(MotivTelemetry.SourceName)
///         .AddSource(MotivRulesTelemetry.SourceName))
///     .WithMetrics(metrics => metrics
///         .AddMeter(MotivTelemetry.MeterName)
///         .AddMeter(MotivRulesTelemetry.MeterName));
/// </code>
/// <para>
/// Instrument and tag names here are the contract a dashboard is built against. A rename is silent —
/// a subscriber simply receives nothing — so once shipped they are additive-only, exactly as core's
/// are.
/// </para>
/// </remarks>
public static class MotivRulesTelemetry
{
    /// <summary>The name of the rules stack's activity source. Pass this to <c>AddSource</c>.</summary>
    public const string SourceName = "Motiv.Serialization";

    /// <summary>The name of the rules stack's meter. Pass this to <c>AddMeter</c>.</summary>
    public const string MeterName = "Motiv.Serialization";

    /// <summary>The span opened around one named rule's evaluation.</summary>
    internal const string EvaluateActivityName = "motiv.rules.evaluate";

    /// <summary>The span opened for one node of an audited rule's result tree, when node spans are on.</summary>
    internal const string NodeActivityName = "motiv.rules.node";

    // Tag names, so a call site cannot drift from the contract by mistyping a literal.
    internal const string NameTag = "motiv.rules.name";
    internal const string VersionTag = "motiv.rules.version";
    internal const string KindTag = "motiv.rules.kind";
    internal const string PhaseTag = "motiv.rules.phase";
    internal const string StoreTag = "motiv.rules.store";
    internal const string OperationTag = "motiv.rules.operation";
    internal const string OutcomeTag = "motiv.rules.outcome";

    internal const string RulesStore = "rules";
    internal const string PropositionsStore = "propositions";

    /// <summary>The <c>motiv.rules.kind</c> values, spelled as <c>NodeId.KindLabel</c> spells them.</summary>
    internal const string RuleKind = "rule";

    /// <inheritdoc cref="RuleKind"/>
    internal const string PropositionKind = "proposition";

    internal static readonly ActivitySource ActivitySource =
        new(SourceName, typeof(MotivRulesTelemetry).Assembly.GetName().Version?.ToString());

    private static readonly Meter Meter =
        new(MeterName, typeof(MotivRulesTelemetry).Assembly.GetName().Version?.ToString());

    /// <summary>The decision logs the queue and drop readings are taken from.</summary>
    internal static readonly TelemetrySubjects<DecisionLog> DecisionLogs = new();

    /// <summary>The binding scopes the generation, lag and catalog readings are taken from.</summary>
    internal static readonly TelemetrySubjects<BindingScope> Scopes = new();

    /// <summary>The break-glass registrations the active reading is taken from.</summary>
    internal static readonly TelemetrySubjects<BreakGlass> BreakGlasses = new();

    internal static readonly Counter<long> BindFailures =
        Meter.CreateCounter<long>(
            "motiv.rules.bind_failures",
            "{failure}",
            "Stored documents that would not bind, by kind and by the phase that found them.");

    internal static readonly Counter<long> PublishConflicts =
        Meter.CreateCounter<long>(
            "motiv.rules.publish_conflicts",
            "{conflict}",
            "Publishes refused because the head had already moved — the 409s.");

    internal static readonly Histogram<double> StoreDuration =
        Meter.CreateHistogram<double>(
            "motiv.rules.store.duration",
            "s",
            "How long a store call took, by kind and operation.");

    internal static readonly Counter<long> Refreshes =
        Meter.CreateCounter<long>(
            "motiv.rules.refreshes",
            "{refresh}",
            "Refresh attempts, by what each one did.");

    internal static readonly Histogram<double> RebuildDuration =
        Meter.CreateHistogram<double>(
            "motiv.rules.rebuild.duration",
            "s",
            "How long rebuilding a world took, whether or not it went on to be served.");

    internal static readonly Counter<long> PublishesUnderBreakGlass =
        Meter.CreateCounter<long>(
            "motiv.rules.publishes_under_break_glass",
            "{publish}",
            "Publishes that bypassed the approval gate because break-glass was active.");

    // ----------------------------------------------------------------------------------------
    // Readings. These have no call site to push from — they are the state of a live object — so
    // each is an observable instrument over the registry of those objects. Assigned to discards
    // because an observable instrument is driven by its callback, not by anything holding it; the
    // Meter keeps it alive, and naming it would only invite someone to try to record on it.
    // ----------------------------------------------------------------------------------------

    private static readonly ObservableUpDownCounter<long> CatalogSize =
        Meter.CreateObservableUpDownCounter(
            "motiv.rules.catalog.size",
            ObserveCatalogSize,
            "{document}",
            "How many rules and propositions this replica has registered.");

    private static readonly ObservableUpDownCounter<long> Generation =
        Meter.CreateObservableUpDownCounter(
            "motiv.rules.generation",
            ObserveGeneration,
            "{generation}",
            "The store generation this replica is serving, per store.");

    private static readonly ObservableUpDownCounter<long> ReplicaLag =
        Meter.CreateObservableUpDownCounter(
            "motiv.rules.replica_lag",
            ObserveReplicaLag,
            "{generation}",
            "How far behind the store this replica was at its last refresh, per store. Zero is converged.");

    private static readonly ObservableCounter<long> DecisionsDropped =
        Meter.CreateObservableCounter(
            "motiv.rules.decisions.dropped",
            () => DecisionLogs.Observe(log => [new Measurement<long>(log.DroppedCount)]),
            "{record}",
            "Audited decisions shed under the Drop posture. Equals the sum of every gap marker written.");

    private static readonly ObservableUpDownCounter<long> DecisionQueueDepth =
        Meter.CreateObservableUpDownCounter(
            "motiv.rules.decision_queue.depth",
            () => DecisionLogs.Observe(log => [new Measurement<long>(log.QueueDepth)]),
            "{record}",
            "Records waiting for the sink — the size of the crash-loss window currently at risk.");

    private static readonly ObservableCounter<long> DecisionBatchesFailed =
        Meter.CreateObservableCounter(
            "motiv.rules.decision_batches.failed",
            () => DecisionLogs.Observe(log => [new Measurement<long>(log.FailedBatchCount)]),
            "{batch}",
            "Batches the decision sink refused. A rising count is a sink that needs attention.");

    private static readonly ObservableUpDownCounter<long> BreakGlassActive =
        Meter.CreateObservableUpDownCounter(
            "motiv.rules.break_glass.active",
            ObserveBreakGlass,
            "{break_glass}",
            "1 while break-glass is bypassing the approval gate, 0 otherwise.");

    /// <summary>
    /// Whether one span per node of an audited rule's result tree is emitted. Off by default, and off
    /// by default <em>even for an audited rule</em>.
    /// </summary>
    /// <remarks>
    /// The structural tree's durable home is the decision log, not the trace waterfall: a decision
    /// record keeps it for the retention window and can be queried, where a trace is sampled, dropped
    /// under load, and gone within days. Node spans are for the case where an operator is already in a
    /// waterfall and wants to see which sub-proposition carried the outcome; they cost one span per
    /// causal node, which is why nothing turns them on for you. Applies process-wide, so set it once
    /// at startup. See <see cref="MaxNodeSpans"/> for the bound.
    /// </remarks>
    public static bool NodeSpans { get; set; }

    /// <summary>
    /// The most node spans one evaluation may emit. Defaults to 1,000; a tree larger than this is
    /// truncated and says so, via <c>motiv.rules.nodes.truncated</c> on the evaluation span.
    /// </summary>
    /// <remarks>
    /// A result tree has no small upper bound — a composition over a thousand propositions is an
    /// ordinary thing to write — so without a cap turning <see cref="NodeSpans"/> on would let one
    /// evaluation emit a span storm. The truncation is reported rather than silent, on principle: a
    /// waterfall that quietly stops short reads as a complete picture of a smaller tree.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public static int MaxNodeSpans
    {
        get => _maxNodeSpans;
        set => _maxNodeSpans = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxNodeSpans must be at least 1.");
    }

    private static int _maxNodeSpans = 1000;

    /// <summary>
    /// Forces the meter and its instruments into existence.
    /// </summary>
    /// <remarks>
    /// A <see cref="MeterListener"/> is offered an instrument when it is created, not when it is
    /// first recorded on, and the static initializer that creates them does not run merely because a
    /// caller named one of this class's compile-time constants. Tests subscribing before any rules
    /// work has happened call this so the listener sees the full published set.
    /// </remarks>
    internal static void EnsureInitialized() =>
        // Reading one static field is enough to force the type initializer, which builds them all.
        _ = BreakGlassActive;

    /// <summary>Whether anything is listening to the rules source or meter.</summary>
    internal static bool IsEnabled =>
        ActivitySource.HasListeners()
        || BindFailures.Enabled
        || PublishConflicts.Enabled
        || StoreDuration.Enabled
        || Refreshes.Enabled
        || RebuildDuration.Enabled;

    /// <summary>
    /// Opens the span that carries which named rule ran, and at which version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the pivot from a publish to the evaluations that ran what was published. Core
    /// <c>Motiv</c> stays version-agnostic on purpose — a <c>SpecBase</c> has no version, and giving
    /// it one to satisfy an operator's query would push a rules-stack concern into the published
    /// engine — so the tags live on a rules-stack span that <em>parents</em> core's
    /// <c>motiv.evaluate</c>. The evaluation is therefore tagged with name and version by containment
    /// rather than by attribute, which is the same answer to the operator's question and costs core
    /// nothing.
    /// </para>
    /// <para>
    /// Returns null when nothing is listening, so an unobserved evaluation neither allocates nor
    /// resolves anything.
    /// </para>
    /// </remarks>
    /// <param name="name">The rule's name.</param>
    /// <param name="version">The version of the binding being evaluated.</param>
    /// <returns>The span, or null when no listener is attached.</returns>
    internal static Activity? StartRuleEvaluation(string name, int version)
    {
        var activity = ActivitySource.StartActivity(EvaluateActivityName, ActivityKind.Internal);
        if (activity is null)
            return null;

        activity.SetTag(NameTag, name);
        activity.SetTag(VersionTag, version);
        return activity;
    }

    /// <summary>Counts a document that would not bind.</summary>
    /// <param name="kind">"rule" or "proposition", matching <c>NodeId.KindLabel</c>.</param>
    /// <param name="phase">Which pass found it — see <see cref="BindPhase"/>.</param>
    /// <param name="count">How many failed.</param>
    internal static void RecordBindFailures(string kind, string phase, int count)
    {
        if (count <= 0 || !BindFailures.Enabled) return;

        BindFailures.Add(count, new TagList { { KindTag, kind }, { PhaseTag, phase } });
    }

    /// <summary>Counts a publish refused because the head had already moved.</summary>
    /// <param name="kind">"rule" or "proposition".</param>
    internal static void RecordPublishConflict(string kind)
    {
        if (!PublishConflicts.Enabled) return;

        PublishConflicts.Add(1, new TagList { { KindTag, kind } });
    }

    /// <summary>Times a store call. Returns 0 when nothing is listening, which the caller reads as "do not record".</summary>
    internal static long StartStoreCall() => StoreDuration.Enabled ? Stopwatch.GetTimestamp() : 0L;

    /// <summary>Records a store call timed from <paramref name="startTimestamp"/>.</summary>
    /// <param name="startTimestamp">What <see cref="StartStoreCall"/> returned; 0 means it was not timed.</param>
    /// <param name="kind">"rule" or "proposition".</param>
    /// <param name="operation">Which store call — see <see cref="StoreOperation"/>.</param>
    internal static void RecordStoreCall(long startTimestamp, string kind, string operation)
    {
        if (startTimestamp == 0 || !StoreDuration.Enabled) return;

        StoreDuration.Record(
            ElapsedSeconds(startTimestamp, Stopwatch.GetTimestamp()),
            new TagList { { KindTag, kind }, { OperationTag, operation } });
    }

    /// <summary>Counts a refresh attempt by what it did, and times the rebuild it ran (if any).</summary>
    /// <param name="outcome">What the refresh did.</param>
    /// <param name="rebuildStartTimestamp">
    /// When the rebuild began, or 0 when this refresh did not rebuild — an
    /// <see cref="RefreshOutcome.Unchanged"/> tick builds nothing, and timing it would report a
    /// no-op as a fast rebuild rather than as no rebuild at all.
    /// </param>
    internal static void RecordRefresh(RefreshOutcome outcome, long rebuildStartTimestamp)
    {
        var end = Stopwatch.GetTimestamp();

        if (Refreshes.Enabled)
            Refreshes.Add(1, new TagList { { OutcomeTag, OutcomeLabel(outcome) } });

        if (rebuildStartTimestamp != 0 && RebuildDuration.Enabled)
            RebuildDuration.Record(
                ElapsedSeconds(rebuildStartTimestamp, end),
                new TagList { { OutcomeTag, OutcomeLabel(outcome) } });
    }

    /// <summary>Counts a publish that bypassed the gate because break-glass was active.</summary>
    /// <param name="kind">"rule" or "proposition".</param>
    internal static void RecordPublishUnderBreakGlass(string kind)
    {
        if (!PublishesUnderBreakGlass.Enabled) return;

        PublishesUnderBreakGlass.Add(1, new TagList { { KindTag, kind } });
    }

    private static IEnumerable<Measurement<long>> ObserveCatalogSize() =>
        Scopes.Observe(scope =>
        [
            new Measurement<long>(scope.RuleCount, new KeyValuePair<string, object?>(KindTag, RuleKind)),
            new Measurement<long>(scope.PropositionCount, new KeyValuePair<string, object?>(KindTag, PropositionKind))
        ]);

    private static IEnumerable<Measurement<long>> ObserveGeneration() =>
        Scopes.Observe(scope =>
        {
            var served = scope.Current.Sequence;
            return
            [
                new Measurement<long>(served.Rules, new KeyValuePair<string, object?>(StoreTag, RulesStore)),
                new Measurement<long>(served.Propositions, new KeyValuePair<string, object?>(StoreTag, PropositionsStore))
            ];
        });

    private static IEnumerable<Measurement<long>> ObserveReplicaLag() =>
        Scopes.Observe(scope =>
        {
            var (rules, propositions) = scope.Lag;
            return
            [
                new Measurement<long>(rules, new KeyValuePair<string, object?>(StoreTag, RulesStore)),
                new Measurement<long>(propositions, new KeyValuePair<string, object?>(StoreTag, PropositionsStore))
            ];
        });

    private static IEnumerable<Measurement<long>> ObserveBreakGlass()
    {
        var now = DateTimeOffset.UtcNow;
        return BreakGlasses.Observe(glass => [new Measurement<long>(glass.Active(now) ? 1 : 0)]);
    }

    private static string OutcomeLabel(RefreshOutcome outcome) =>
        outcome switch
        {
            RefreshOutcome.Unchanged => "unchanged",
            RefreshOutcome.Applied => "applied",
            RefreshOutcome.Aborted => "aborted",
            _ => "contended"
        };

    private static double ElapsedSeconds(long start, long end) => (end - start) / (double)Stopwatch.Frequency;
}

/// <summary>The pass that found a document would not bind — the <c>motiv.rules.phase</c> tag's values.</summary>
internal static class BindPhase
{
    /// <summary>The one-shot read of the store at startup.</summary>
    public const string Load = "load";

    /// <summary>A poller's whole-world rebuild.</summary>
    public const string Refresh = "refresh";

    /// <summary>An authoring write that was refused.</summary>
    public const string Publish = "publish";
}

/// <summary>Which store call was timed — the <c>motiv.rules.operation</c> tag's values.</summary>
internal static class StoreOperation
{
    /// <summary>Reading every head row.</summary>
    public const string Load = "load";

    /// <summary>Appending a version row.</summary>
    public const string Append = "append";

    /// <summary>Reading the store's generation.</summary>
    public const string Generation = "generation";
}
