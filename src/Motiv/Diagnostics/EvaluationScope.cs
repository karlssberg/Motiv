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

        // startTimestamp is 0 when Start() ran before any listener attached (see Start()'s sentinel).
        // A duration measured from that point would span from the Stopwatch epoch, not the evaluation,
        // so only record when the scope was actually timed.
        if (durationEnabled && startTimestamp != 0)
            MotivTelemetry.Duration.Record(ElapsedSeconds(startTimestamp), tags);
    }

    private static double ElapsedSeconds(long startTimestamp) =>
        (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
}
