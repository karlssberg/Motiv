namespace Motiv.Tests.Traversal;

/// <summary>
/// A higher-order <em>result</em> defers its user delegates to first property read — the cause selector
/// that builds its <c>Evaluation</c>, and the <c>WhenTrue</c>/<c>WhenFalse</c> that render its value. Read
/// while any evaluation is in flight, each of those ran on that evaluation's budget
/// (<see href="https://github.com/karlssberg/Motiv/issues/213">#213</see>).
/// </summary>
/// <remarks>
/// <see cref="HigherOrderPredicateBudgetTests" /> is the eager half of the same seam: #208 excluded the
/// decision — resolving the elements and applying the <c>As(...)</c> predicate to them. This is the half
/// it cut. The cause selector makes the omission plain rather than merely inconsistent, because the
/// default selector for <c>As(...)</c> is <c>Causes.Get(..., higherOrderPredicate)</c>, which re-invokes
/// <b>the very predicate #208 excluded</b> — so one delegate was excluded in one place and charged in
/// another.
/// <para>
/// <b>These delegates run after <c>Satisfied</c> is fixed.</b> Every one of these results takes its
/// outcome as a constructor argument; nothing read afterwards can change it. They describe a decision
/// rather than reach one, which is the same argument that keeps telemetry's own explanation rendering
/// excluded (<see href="https://github.com/karlssberg/Motiv/issues/209">#209</see>) — and the reason a
/// leaf predicate that evaluates a proposition per item stays counted, because that one <em>is</em> how
/// its node reaches an answer.
/// </para>
/// </remarks>
[Collection(MotivLimitsTestCollection.Name)]
public class HigherOrderLazyResultBudgetTests : IDisposable
{
    private readonly int _previous = MotivLimits.MaxEvaluationSize;

    /// <summary>
    /// Nineteen nodes — a chain of ten leaves — so that charging it to a composition of three against a
    /// limit of twenty is decisive rather than marginal. Shared with
    /// <see cref="HigherOrderPredicateBudgetTests" />' threshold, and for the same reason.
    /// </summary>
    private static readonly SpecBase<int, string> Threshold =
        Enumerable.Range(0, 10)
            .Select(index => (SpecBase<int, string>)Spec.Build((int n) => n % 2 == 0).Create($"t{index} is even"))
            .Aggregate((left, right) => left.And(right));

    private static readonly int[] Models = [2, 4];

    public void Dispose() => MotivLimits.MaxEvaluationSize = _previous;

    /// <summary>
    /// The cause selector, over all four families. The result is produced with no budget in force and
    /// left cold; the property is then read from inside an unrelated evaluation, which is where the
    /// selector — and with it the caller's <c>As(...)</c> predicate — actually runs.
    /// </summary>
    /// <remarks>
    /// The reading leaf is the vehicle and not the subject: it is merely the plainest way to have an
    /// evaluation in flight at the moment of the read. <see cref="Should_not_depend_on_who_read_first" />
    /// is the case that does not rest on how the vehicle is judged.
    /// </remarks>
    [Theory]
    [MemberData(nameof(CauseSelectorFamilies))]
    public void Should_not_charge_a_results_cause_selector_to_the_evaluation_that_reads_it(
        string family,
        Func<SpecBase<IEnumerable<int>, string>> higherOrder)
    {
        var cold = higherOrder().Evaluate(Models);

        MotivLimits.MaxEvaluationSize = 20;

        var reader = Reads(cold).And(NonEmpty());

        Should.NotThrow(
            () => reader.Evaluate(Models).Satisfied.ShouldBeTrue(),
            $"the {family} family's cause selector re-invokes the predicate #208 excluded");
    }

    /// <summary>
    /// The value delegates, over all four families, with a built-in quantifier so that the cause selector
    /// is <c>Causes.SatisfiedElseAll</c> and cannot be what leaks. Only <c>WhenTrue</c> evaluates a
    /// proposition here.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValueDelegateFamilies))]
    public void Should_not_charge_a_results_value_delegate_to_the_evaluation_that_reads_it(
        string family,
        Func<SpecBase<IEnumerable<int>, string>> higherOrder)
    {
        var cold = higherOrder().Evaluate(Models);

        MotivLimits.MaxEvaluationSize = 20;

        var reader = Reads(cold).And(NonEmpty());

        Should.NotThrow(
            () => reader.Evaluate(Models).Satisfied.ShouldBeTrue(),
            $"the {family} family's WhenTrue is rendering an explanation, not reaching a decision");
    }

    /// <summary>
    /// A <em>yielding</em> value delegate, which is the shape that separates the two value seams. An
    /// iterator block runs none of its body when it is invoked and all of it when it is enumerated, so a
    /// seam that excluded the invocation and handed back the sequence would leave the caller's code
    /// outside the scope while looking, from every angle but this one, as though it had covered it.
    /// </summary>
    /// <remarks>
    /// Written because routing a yielding delegate through the single-value seam <b>compiles</b> —
    /// <c>TValue</c> simply infers as <c>IEnumerable&lt;string&gt;</c> — and leaves the other cases in this
    /// file green. The structural gate refuses that miswrite too; this is the same claim stated as
    /// behaviour, so neither rests on the other.
    /// </remarks>
    [Fact]
    public void Should_not_charge_a_yielding_value_delegate_to_the_evaluation_that_reads_it()
    {
        var cold = Spec.Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrueYield(_ => ThresholdAssertions())
            .WhenFalseYield(_ => ThresholdAssertions())
            .Create("all models are even")
            .Evaluate(Models);

        MotivLimits.MaxEvaluationSize = 20;

        var reader = Reads(cold).And(NonEmpty());

        Should.NotThrow(
            () => reader.Evaluate(Models).Satisfied.ShouldBeTrue(),
            "a yielding WhenTrue evaluates when it is enumerated, so the enumeration is what must be excluded");
    }

    /// <summary>
    /// The seam's null contract, over the family that did <em>not</em> have one. A resolver returning null
    /// degrades to empty values rather than throwing.
    /// </summary>
    /// <remarks>
    /// The eight results this seam stands for disagreed here, and only one side was tested. The three
    /// <c>MultiAssertionExplanation</c> families used <c>?.ToArray()</c> and pin the degradation in
    /// <c>HigherOrderFrom*MultiAssertionExplanationBooleanResultTests</c> — <c>Values</c> empty,
    /// <c>Explanation</c> falling back to the statement's reason. The four metadata families used a bare
    /// <c>.ToArray()</c> and threw, with nothing covering it either way. One seam stands for both, so it
    /// stands for the tested contract; this case is the resulting change stated as behaviour rather than
    /// left to be discovered.
    /// </remarks>
    [Fact]
    public void Should_degrade_a_yielded_null_to_empty_values()
    {
        var result = Spec.Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrueYield(_ => (IEnumerable<string>)null!)
            .WhenFalseYield(_ => (IEnumerable<string>)null!)
            .Create("all models are even")
            .Evaluate(Models);

        Should.NotThrow(
            () => result.Values.ShouldBeEmpty(),
            "the assertion families' tested null contract is the one the shared seam carries");
    }

    /// <summary>
    /// The case that does not turn on where the line is drawn. Two results of the same proposition over
    /// the same models, read by the same composition against the same limit; the only difference is that
    /// one of them had its property read earlier, by someone else. The delegate is memoized, so the
    /// warm one has nothing left to run and the cold one pays — <b>whether a composition is refused
    /// depends on the order two unrelated reads happened in</b>, which is not a line any caller could
    /// reason about.
    /// </summary>
    /// <remarks>
    /// It is also #209 inverted, and reachable without writing a delegate at all: Motiv's own telemetry
    /// tags a span with the result's explanation, so <b>attaching a listener warms the memo</b> and makes
    /// a later refusal disappear. #209 established that subscribing must not change what an evaluation
    /// decides; this is the same statement with the sign flipped.
    /// </remarks>
    [Theory]
    [MemberData(nameof(CauseSelectorFamilies))]
    public void Should_not_depend_on_who_read_first(
        string family,
        Func<SpecBase<IEnumerable<int>, string>> higherOrder)
    {
        var cold = higherOrder().Evaluate(Models);
        var warm = higherOrder().Evaluate(Models);
        Warm(warm);

        MotivLimits.MaxEvaluationSize = 20;

        var readsWarm = Record(() => Reads(warm).And(NonEmpty()).Evaluate(Models));
        var readsCold = Record(() => Reads(cold).And(NonEmpty()).Evaluate(Models));

        readsCold.ShouldBe(
            readsWarm,
            $"the {family} family's outcome must not depend on whether someone else read the property first");
    }

    /// <summary>
    /// The leak canary. Excluding the read must <em>park</em> the enclosing composition's count and hand
    /// it back, not discard it: a scope that zeroed the count on the way out would leave every read a
    /// free refill of the whole budget, and the three cases above would pass on an implementation that
    /// had removed the bound rather than placed the work outside it.
    /// </summary>
    [Fact]
    public void Should_resume_the_readers_own_count_after_the_read()
    {
        var cold = Quorum().Evaluate(Models);

        MotivLimits.MaxEvaluationSize = 20;

        // Twenty-one nodes of ordinary composition, plus the reading leaf and the And joining it —
        // twenty-three against a bound of twenty, which is over it whether or not the read is charged.
        // Unless, that is, the read handed back a zero.
        var overspends = Reads(cold).And(Chain(11));

        Should.Throw<SpecException>(
            () => overspends.Evaluate(Models),
            "a read that reset the count rather than parking it would silently remove the bound");
    }

    /// <summary>
    /// A leaf whose predicate reads the result, forcing whichever delegates it has deferred.
    /// </summary>
    /// <remarks>
    /// <b>Both properties, because neither one alone reaches every family.</b> A named higher-order
    /// proposition's <c>Assertions</c> are <c>"{name} == true"</c>, rendered from the description without
    /// touching the value delegate at all — so a vehicle reading only <c>Assertions</c> leaves three of
    /// the four value-delegate cases green while the delegate sits unresolved, which is a test reporting
    /// a property it is not exercising. <c>ToArray</c> for the same reason in miniature: a yielding
    /// <c>WhenTrue</c> runs at enumeration rather than at invocation, and the enumeration is the part
    /// that evaluates.
    /// </remarks>
    private static SpecBase<IEnumerable<int>, string> Reads(BooleanResultBase<string> result) =>
        Spec.Build((IEnumerable<int> _) =>
                result.Values.ToArray().Length >= 0 && result.Assertions.ToArray().Length >= 0)
            .Create("the reader read the result");

    /// <summary>
    /// Reads exactly what <see cref="Reads" /> reads, so that the warm result has nothing left deferred.
    /// Warming through a narrower read leaves the delegate cold and the two arms agreeing for the wrong
    /// reason — which is how the boolean-predicate family passed this case while leaking.
    /// </summary>
    private static void Warm(BooleanResultBase<string> result)
    {
        _ = result.Values.ToArray();
        _ = result.Assertions.ToArray();
    }

    /// <summary>Whether the evaluation was refused, as a comparable value rather than an exception.</summary>
    private static string Record(Func<BooleanResultBase<string>> evaluate)
    {
        try
        {
            evaluate();
            return "accepted";
        }
        catch (SpecException)
        {
            return "refused";
        }
    }

    public static TheoryData<string, Func<SpecBase<IEnumerable<int>, string>>> CauseSelectorFamilies() =>
        new()
        {
            { "boolean-predicate", Quorum },
            {
                "boolean-result-predicate",
                () => Spec.Build(Element())
                    .As(results => results.All(result => result.Satisfied) && ThresholdHolds())
                    .Create("quorum over spec results")
            },
            {
                "policy-result-predicate",
                () => Spec.Build(ElementPolicy())
                    .As(results => results.All(result => result.Satisfied) && ThresholdHolds())
                    .Create("quorum over policy results")
            },
            {
                "expression-tree",
                () => Spec.From((int n) => n % 2 == 0)
                    .As(results => results.All(result => result.Satisfied) && ThresholdHolds())
                    .Create("quorum over expression results")
            }
        };

    public static TheoryData<string, Func<SpecBase<IEnumerable<int>, string>>> ValueDelegateFamilies() =>
        new()
        {
            {
                "boolean-predicate",
                () => Spec.Build((int n) => n % 2 == 0)
                    .AsAllSatisfied()
                    .WhenTrue(_ => ThresholdHolds() ? "all even" : "all even, barely")
                    .WhenFalse(_ => "not all even")
                    .Create("all models are even")
            },
            {
                "boolean-result-predicate",
                () => Spec.Build(Element())
                    .AsAllSatisfied()
                    .WhenTrue(_ => ThresholdHolds() ? "all even" : "all even, barely")
                    .WhenFalse(_ => "not all even")
                    .Create("all spec results are even")
            },
            {
                "policy-result-predicate",
                () => Spec.Build(ElementPolicy())
                    .AsAllSatisfied()
                    .WhenTrue(_ => ThresholdHolds() ? "all even" : "all even, barely")
                    .WhenFalse(_ => "not all even")
                    .Create("all policy results are even")
            },
            {
                "expression-tree",
                () => Spec.From((int n) => n % 2 == 0)
                    .AsAllSatisfied()
                    .WhenTrue(_ => ThresholdHolds() ? "all even" : "all even, barely")
                    .WhenFalse(_ => "not all even")
                    .Create("all expression results are even")
            }
        };

    private static SpecBase<IEnumerable<int>, string> Quorum() =>
        Spec.Build((int n) => n % 2 == 0)
            .As(results => results.All(result => result.Satisfied) && ThresholdHolds())
            .Create("quorum over models");

    private static bool ThresholdHolds() => Threshold.Evaluate(2).Satisfied;

    /// <summary>
    /// An iterator block, deliberately: the <c>yield</c> is what defers the evaluation below it to
    /// enumeration time, which is the whole subject of
    /// <see cref="Should_not_charge_a_yielding_value_delegate_to_the_evaluation_that_reads_it" />.
    /// </summary>
    private static IEnumerable<string> ThresholdAssertions()
    {
        yield return ThresholdHolds() ? "the threshold holds" : "the threshold does not hold";
    }

    private static SpecBase<int, string> Element() =>
        Spec.Build((int n) => n % 2 == 0).Create("the element is even");

    private static PolicyBase<int, string> ElementPolicy() =>
        Spec.Build((int n) => n % 2 == 0).Create("the element is even");

    private static SpecBase<IEnumerable<int>, string> NonEmpty() =>
        Spec.Build((IEnumerable<int> models) => models.Any()).Create("the collection is not empty");

    /// <summary>A left-deep chain of <paramref name="leaves" /> propositions — <c>2n - 1</c> nodes.</summary>
    private static SpecBase<IEnumerable<int>, string> Chain(int leaves) =>
        Enumerable.Range(0, leaves)
            .Select(index => (SpecBase<IEnumerable<int>, string>)
                Spec.Build((IEnumerable<int> models) => models.Any()).Create($"c{index} is not empty"))
            .Aggregate((left, right) => left.And(right));
}
