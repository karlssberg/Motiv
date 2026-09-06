using System.Diagnostics;
using Motiv.Diagnostics;

namespace Motiv.Tests.Traversal;

/// <summary>
/// <b>Turning tracing on must not change what an evaluation decides.</b> Telemetry already holds that
/// line against exceptions — <c>EvaluationScope.TrySetExplanationTags</c> swallows a throw from
/// explanation resolution precisely so an otherwise-succeeding evaluation is not turned into a failing
/// one — and did not hold it against <see cref="MotivLimits.MaxEvaluationSize" />.
/// </summary>
/// <remarks>
/// Reported as <see href="https://github.com/karlssberg/Motiv/issues/209">#209</see>. Once
/// <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see> made the budget ambient, any
/// re-entry into the fold from inside a node spent the enclosing composition's allowance unless
/// something declared it excluded. Three seams declare it — higher-order element resolution,
/// <c>EnumerableExtensions.Where</c> and <c>Tap</c> — and telemetry did not, although it is the same
/// kind of work by the same argument: rendering an explanation is not part of the decision, and a
/// listener consuming a finished span is a side effect hung off the evaluation rather than part of it.
/// <para>
/// The failure has the worse of the two available shapes. The refusal is raised <i>inside</i>
/// telemetry's own <c>catch</c>, so it is swallowed — and the count it left behind is not released,
/// because a nested fold's <c>Ownership</c> deliberately does not release. The exception therefore
/// surfaces at the next node the composition charges, which is a node that did nothing wrong.
/// </para>
/// </remarks>
[Collection(MotivLimitsTestCollection.Name)]
public class TelemetryBudgetTests : IDisposable
{
    private readonly int _previousLimit = MotivLimits.MaxEvaluationSize;
    private readonly ExplanationDetail _previousDetail = MotivTelemetry.ExplanationDetail;

    public void Dispose()
    {
        MotivLimits.MaxEvaluationSize = _previousLimit;
        MotivTelemetry.ExplanationDetail = _previousDetail;
    }

    /// <summary>
    /// Stated as a difference rather than as an outcome: the same composition, the same model, the same
    /// limit, evaluated once with nothing listening and once with a listener attached. The first call is
    /// the control — without it this would pass on a limit that was simply generous.
    /// </summary>
    [Fact]
    public void Should_not_charge_explanation_rendering_to_the_composition()
    {
        MotivLimits.MaxEvaluationSize = Limit;

        ComposedOverATracedEvaluation().Evaluate(Model).Satisfied.ShouldBeTrue(
            "the control: with nothing listening the explanation is never rendered");

        using var listening = Listening();

        ComposedOverATracedEvaluation().Evaluate(Model).Satisfied.ShouldBeTrue(
            "attaching a listener must not change what the composition decides");
    }

    /// <summary>
    /// The same claim for the other half of what a span costs. <c>Activity.Dispose()</c> synchronously
    /// runs every <c>ActivityStopped</c> callback, so a listener that evaluates a proposition of its own
    /// — an exporter enriching a span, an audit hook — runs on the composition's thread while the
    /// composition is still in flight. <see cref="ExplanationDetail.None" /> is set so that
    /// <c>TrySetExplanationTags</c> returns before rendering anything, leaving the listener as the only
    /// thing that could be charged.
    /// </summary>
    [Fact]
    public void Should_not_charge_a_listeners_own_evaluation_to_the_composition()
    {
        MotivLimits.MaxEvaluationSize = Limit;
        MotivTelemetry.ExplanationDetail = ExplanationDetail.None;

        using var listening = Listening(onStopped: () => Audit().Evaluate(Model));

        ComposedOverATracedEvaluation().Evaluate(Model).Satisfied.ShouldBeTrue(
            "a listener consuming the span must not spend the composition's budget");
    }

    /// <summary>
    /// And again where the span is terminated by a throw rather than a result. A caller that catches an
    /// exception from a sub-evaluation and carries on is the shape #209 names as reachable from user
    /// code, and the span it leaves behind is disposed on the failure path — running the same listener
    /// callbacks, on a composition that is still in flight.
    /// </summary>
    [Fact]
    public void Should_not_charge_a_failed_spans_listener_to_the_composition()
    {
        MotivLimits.MaxEvaluationSize = Limit;
        MotivTelemetry.ExplanationDetail = ExplanationDetail.None;

        ComposedOverACaughtFailure().Evaluate(Model).Satisfied.ShouldBeTrue(
            "the control: with nothing listening the failed span costs nothing");

        using var listening = Listening(onStopped: () => Audit().Evaluate(Model));

        ComposedOverACaughtFailure().Evaluate(Model).Satisfied.ShouldBeTrue(
            "a listener consuming a failed span must not spend the budget of the composition that " +
            "caught it");
    }

    /// <summary>
    /// The half a suite of successes cannot see. <see cref="EvaluationBudget.Exclude" /> <em>parks</em>
    /// the composition's count and hands it back; a version that zeroed it and never restored would leak
    /// in the <em>permissive</em> direction, and every case above would stay green — they all assert an
    /// evaluation succeeds, and a discarded count makes more of them succeed, not fewer.
    /// </summary>
    /// <remarks>
    /// So this one asserts a refusal instead: a composition one node past the limit must be refused
    /// whether or not a listener is attached. Under a discarding exclusion the traced call resumes from
    /// zero after the inner span and is admitted.
    /// </remarks>
    [Fact]
    public void Should_resume_the_compositions_count_after_a_traced_evaluation()
    {
        MotivLimits.MaxEvaluationSize = Limit + 1;

        Should.Throw<SpecException>(() => OneNodePastTheLimit().Evaluate(Model),
            "the control: five nodes against a limit of four");

        using var listening = Listening();

        Should.Throw<SpecException>(() => OneNodePastTheLimit().Evaluate(Model),
            "and still five nodes when a listener is attached — telemetry's own work is set aside, " +
            "not the composition's");
    }

    /// <summary>
    /// Odd, so that the inner proposition below is <em>unsatisfied</em> and its explanation comes from
    /// the delegate rather than the constant. An unnamed explanation proposition is the only shape whose
    /// assertion text is produced by user code at all: name it and the text becomes
    /// <c>"{name} == false"</c>, with the delegate's value demoted to <c>Values</c>, which telemetry
    /// never reads.
    /// </summary>
    private const int Model = 3;

    /// <summary>
    /// The untraced composition's cost exactly, rather than a comfortable margin, so that a single node
    /// charged by telemetry is the difference between the two calls in each case above. Measured by
    /// bisection rather than derived: the chain arithmetic (<c>2n - 1</c>) gives the three, and the
    /// inner proposition's own fold adds nothing to it, because the fold charges a leaf before
    /// descending into it and the leaf's predicate is what re-enters.
    /// </summary>
    private const int Limit = 3;

    /// <summary>
    /// A composition containing a node that reaches the <em>public</em> evaluation surface, which is the
    /// only way a span is opened while a fold is already unwinding — composed propositions take
    /// <c>EvaluateInternal</c> precisely so a composition emits one span at its root rather than one per
    /// node.
    /// </summary>
    /// <remarks>
    /// Three nodes, as a chain of two propositions is: the operation at the fold's root and its two
    /// operands. The audit chain is nineteen more, and is rendered by the inner proposition's
    /// <c>WhenFalse</c> delegate — so it runs only when something reads the inner result's explanation,
    /// which nothing does until a listener is attached.
    /// </remarks>
    private static SpecBase<int, string> ComposedOverATracedEvaluation()
    {
        var inner = Spec
            .Build((int n) => n % 2 == 0)
            .WhenTrue("even")
            .WhenFalse(n => Audit().Evaluate(n).Satisfied ? "odd" : "odd, and the audit disagreed")
            .Create();

        return Spec
            .Build((int n) => !inner.Evaluate(n).Satisfied)
            .Create("the inner proposition does not hold")
            .And(Leaf(1));
    }

    /// <summary>
    /// The same composition with one more operand — five nodes rather than three, so that the traced
    /// node sits in the <em>middle</em> of a count that has to survive it.
    /// </summary>
    private static SpecBase<int, string> OneNodePastTheLimit() =>
        ComposedOverATracedEvaluation().And(Leaf(2));

    /// <summary>
    /// The same three nodes, reached by the other terminator: the inner proposition throws, the node
    /// holding it catches, and the composition carries on to its second operand.
    /// </summary>
    private static SpecBase<int, string> ComposedOverACaughtFailure()
    {
        var inner = Spec
            .Build((int _) => throw new InvalidOperationException("thrown from inside an evaluation"))
            .Create("throws");

        return Spec
            .Build((int n) =>
            {
                try { return inner.Evaluate(n).Satisfied; }
                catch (InvalidOperationException) { return true; }
            })
            .Create("the inner proposition was caught")
            .And(Leaf(1));
    }

    /// <summary>Nineteen nodes: within the limit on its own account, over it when added to a composition.</summary>
    private static SpecBase<int, string> Audit() => FlatChain(10);

    /// <summary>
    /// A listener of this suite's own rather than <c>TelemetryHarness</c>, which records spans this
    /// suite never asserts on and cannot run an evaluation when one stops.
    /// </summary>
    /// <remarks>
    /// The callback is guarded against its own re-entry: the evaluation it runs is itself traced, so an
    /// unguarded callback would stop a span from inside the handler for a stopped span, without end.
    /// </remarks>
    private static IDisposable Listening(Action? onStopped = null)
    {
        var running = false;

        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MotivTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _ =>
            {
                if (onStopped is null || running) return;

                running = true;
                try { onStopped(); }
                finally { running = false; }
            }
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    private static SpecBase<int, string> Leaf(int index) =>
        Spec.Build((int n) => n % 2 != 0).Create($"p{index} is odd");

    private static SpecBase<int, string> FlatChain(int operands) =>
        Enumerable.Range(0, operands).Select(Leaf).Aggregate((left, right) => left.And(right));
}
