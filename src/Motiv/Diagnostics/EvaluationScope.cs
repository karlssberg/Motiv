using System.Diagnostics;
using Motiv.Traversal;

namespace Motiv.Diagnostics;

/// <summary>
/// Spans a single top-level evaluation. Created by <see cref="Start" />, then terminated by exactly one of
/// <see cref="Complete" />, <see cref="Fail" />, or <see cref="Cancel" />. A struct so that an unobserved
/// evaluation allocates nothing.
/// </summary>
/// <remarks>
/// <b>Nothing done here is part of the composition, and each of the four says so</b> through
/// <see cref="EvaluationBudget.Exclude" />. The bound on an evaluation's size is ambient, so any
/// re-entry into the fold from inside a node spends the enclosing composition's allowance unless it is
/// declared excluded — and every one of these methods can re-enter it. Rendering an explanation runs a
/// user's <c>WhenTrue</c>/<c>WhenFalse</c> delegate, and <c>Activity.Dispose()</c> synchronously runs
/// every <c>ActivityStopped</c> callback; either may evaluate a proposition of its own.
/// <para>
/// The argument is <c>Tap</c>'s, which is already excluded for it: observability hung off a decision is
/// not part of the decision. Charged, it would be worse than merely wrong — a scope belonging to an
/// evaluation nested inside a running one leaves its spending behind, because a nested fold's
/// <c>Ownership</c> deliberately does not release, so the refusal surfaces at the next node the
/// composition charges rather than here. That is
/// <see href="https://github.com/karlssberg/Motiv/issues/209">#209</see>: turning tracing on could turn
/// a satisfied evaluation into a <c>SpecException</c> at an unrelated node. The rule this restores is
/// the one <see cref="TrySetExplanationTags" /> already keeps against exceptions — <b>attaching a
/// listener must not change what an evaluation decides.</b>
/// </para>
/// <para>
/// The exclusion parks the count rather than discarding it, so a listener or a delegate whose own
/// evaluation is oversized is still refused on its own account; it simply costs the composition
/// nothing.
/// </para>
/// </remarks>
internal readonly struct EvaluationScope(Activity? activity, long startTimestamp, string proposition)
{
    /// <summary>Starts a scope, opening an activity if (and only if) something is listening.</summary>
    /// <param name="proposition">The propositional statement being evaluated.</param>
    /// <returns>
    /// A scope that must be terminated with exactly one of <see cref="Complete" />, <see cref="Fail" />, or
    /// <see cref="Cancel" />.
    /// </returns>
    internal static EvaluationScope Start(string proposition)
    {
        using var exclusion = EvaluationBudget.Exclude();

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
        using var exclusion = EvaluationBudget.Exclude();

        // Taken before any tagging so the duration measures the evaluation, not telemetry's own cost of
        // resolving Reason/Assertions or dispatching to listeners/exporters via activity.Dispose().
        var endTimestamp = Stopwatch.GetTimestamp();

        // Recorded before the activity is touched: activity.Dispose() below synchronously runs every
        // ActivityStopped listener, which can throw (a misbehaving listener or exporter). The metrics for an
        // evaluation that already succeeded must not be lost because of that — see the caller, which isolates
        // that same failure from the evaluation's own result.
        Record(result.Satisfied, errorType: null, endTimestamp);

        if (activity is not null)
        {
            activity.SetTag("motiv.satisfied", result.Satisfied);
            TrySetExplanationTags(activity, result);
            activity.Dispose();
        }
    }

    /// <summary>
    /// Tags the span with the result's own explanation, to the extent
    /// <see cref="MotivTelemetry.ExplanationDetail" /> allows — both tags under
    /// <see cref="ExplanationDetail.Full" />, <c>motiv.reason</c> only under
    /// <see cref="ExplanationDetail.ReasonOnly" />, and neither under <see cref="ExplanationDetail.None" />,
    /// which returns before any resolution. <see cref="BooleanResultBase.Reason" /> and
    /// <see cref="BooleanResultBase.Assertions" /> are lazily resolved, and that resolution can run a user's
    /// WhenTrue/WhenFalse delegate — which can throw. Since this only happens because a tracing listener is
    /// attached, that throw must never escape and turn an otherwise-succeeding evaluation into a failing one: if
    /// resolution throws, the span still carries the outcome (tagged by the caller beforehand) but no explanation
    /// text.
    /// </summary>
    private static void TrySetExplanationTags(Activity activity, BooleanResultBase result)
    {
        var detail = MotivTelemetry.ExplanationDetail;
        if (detail == ExplanationDetail.None)
            return;

        try
        {
            activity.SetTag("motiv.reason", result.Reason);

            if (detail == ExplanationDetail.Full)
                activity.SetTag("motiv.assertions", result.Assertions.ToArray());
        }
        catch
        {
            // Explanation resolution failing must not affect the evaluation's own outcome — see remarks above.
        }
    }

    /// <summary>Terminates the scope with a failed evaluation. The exception itself is rethrown by the caller.</summary>
    /// <param name="exception">The exception that escaped the evaluation.</param>
    internal void Fail(Exception exception)
    {
        using var exclusion = EvaluationBudget.Exclude();

        // Taken before any tagging so the duration measures the evaluation, not telemetry's own cost — see
        // the equivalent remark on Complete.
        var endTimestamp = Stopwatch.GetTimestamp();
        var errorType = exception.GetType().FullName;

        // Recorded before the activity is touched — see the equivalent remark in Complete.
        Record(satisfied: null, errorType, endTimestamp);

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
    }

    /// <summary>
    /// Terminates the scope for an evaluation that was cancelled via the caller's own <see cref="CancellationToken" />.
    /// Per OpenTelemetry semantic conventions, a cancellation the instrumentation can attribute to the caller's
    /// own intent is not an error: the span status is left unset and no <c>error.type</c> tag or exception event
    /// is added. The evaluation is still counted — via a distinguishing <c>motiv.cancelled</c> dimension on both
    /// instruments — so cancellations remain queryable without inflating the error rate.
    /// </summary>
    internal void Cancel()
    {
        using var exclusion = EvaluationBudget.Exclude();

        // Taken before any tagging — see the equivalent remark on Complete.
        var endTimestamp = Stopwatch.GetTimestamp();

        // Recorded before the activity is touched — see the equivalent remark in Complete.
        Record(satisfied: null, errorType: null, endTimestamp, cancelled: true);

        activity?.Dispose();
    }

    private void Record(bool? satisfied, string? errorType, long endTimestamp, bool cancelled = false)
    {
        var countEnabled = MotivTelemetry.Evaluations.Enabled;
        var durationEnabled = MotivTelemetry.Duration.Enabled;

        if (!countEnabled && !durationEnabled) return;

        var tags = new TagList { { "motiv.proposition", proposition } };

        if (satisfied.HasValue)
            tags.Add("motiv.satisfied", satisfied.Value);

        if (errorType is not null)
            tags.Add("error.type", errorType);

        if (cancelled)
            tags.Add("motiv.cancelled", true);

        if (countEnabled)
            MotivTelemetry.Evaluations.Add(1, tags);

        // startTimestamp is 0 when Start() ran before any listener attached (see Start()'s sentinel).
        // A duration measured from that point would span from the Stopwatch epoch, not the evaluation,
        // so only record when the scope was actually timed.
        if (durationEnabled && startTimestamp != 0)
            MotivTelemetry.Duration.Record(ElapsedSeconds(startTimestamp, endTimestamp), tags);
    }

    private static double ElapsedSeconds(long startTimestamp, long endTimestamp) =>
        (endTimestamp - startTimestamp) / (double)Stopwatch.Frequency;
}
