namespace Motiv.Tests.Traversal;

/// <summary>
/// The predicate a caller supplies through <c>As(...)</c> decides the higher-order node; it is not
/// part of the composition the node sits in. The former public <c>HigherOrderResults.Materialize</c>
/// entry point excluded the element resolution and stopped there, so a predicate that evaluated a
/// proposition of its own spent the enclosing composition's budget —
/// <see href="https://github.com/karlssberg/Motiv/issues/208">#208</see>.
/// </summary>
/// <remarks>
/// The same argument as <c>Tap</c>'s and telemetry's, one delegate over: work a node does to reach
/// its own answer is inside the node. Charged, a quorum rule that read its threshold from another
/// proposition could make the composition around it refuse — at a node that has nothing to do with
/// the one that overspent.
/// </remarks>
[Collection(MotivLimitsTestCollection.Name)]
public class HigherOrderPredicateBudgetTests : IDisposable
{
    private readonly int _previous = MotivLimits.MaxEvaluationSize;

    /// <summary>
    /// Nineteen nodes — a chain of ten leaves — so that charging it to a composition of three against a
    /// limit of twenty is decisive rather than marginal.
    /// </summary>
    private static readonly SpecBase<int, string> Threshold =
        Enumerable.Range(0, 10)
            .Select(index => (SpecBase<int, string>)Spec.Build((int n) => n % 2 == 0).Create($"t{index} is even"))
            .Aggregate((left, right) => left.And(right));

    private static readonly int[] Models = [2, 4];

    public void Dispose() => MotivLimits.MaxEvaluationSize = _previous;

    /// <summary>
    /// Stated over all four higher-order families, because each writes its own <c>EvaluateModels</c>
    /// and there is nothing shared for a single case to stand for. The composition costs three nodes
    /// against a limit of twenty; the predicate's own proposition costs nineteen, so charging it
    /// refuses an evaluation that is nowhere near the bound.
    /// </summary>
    /// <remarks>
    /// Asserted as <see cref="Should.NotThrow(Action, string?)" /> rather than on the outcome, because
    /// the failure being guarded is a <see cref="SpecException" /> refusal and not a <c>false</c>
    /// answer. Stated as <c>Satisfied.ShouldBeTrue(because)</c> the case still fails — by throwing
    /// before the assertion, so the reason naming the family never reaches the output.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Families))]
    public void Should_not_charge_a_user_predicates_own_evaluation_to_the_composition(
        string family,
        Func<SpecBase<IEnumerable<int>, string>> higherOrder)
    {
        MotivLimits.MaxEvaluationSize = 20;

        var composed = higherOrder().And(NonEmpty());

        Should.NotThrow(
            () => composed.Evaluate(Models).Satisfied.ShouldBeTrue(),
            $"the {family} family's materializing funnel must not charge the predicate's own evaluation");
    }

    /// <summary>
    /// And on the funnel <c>Matches</c> takes. A custom predicate has no short-circuit form, so
    /// <c>Matches</c> falls back to the same <c>EvaluateModels</c> — which is worth pinning rather
    /// than assuming, since a later short-circuit for <c>As(...)</c> would move the seam.
    /// </summary>
    [Theory]
    [MemberData(nameof(Families))]
    public void Should_not_charge_a_user_predicates_own_evaluation_on_matches(
        string family,
        Func<SpecBase<IEnumerable<int>, string>> higherOrder)
    {
        MotivLimits.MaxEvaluationSize = 20;

        var composed = higherOrder().And(NonEmpty());

        Should.NotThrow(
            () => composed.Matches(Models).ShouldBeTrue(),
            $"the {family} family's allocation-free funnel must not charge the predicate's own evaluation");
    }

    /// <summary>
    /// A factory rather than the proposition itself, because the two theories share this data and a
    /// higher-order proposition caches nothing between calls only as long as nobody gives it reason to.
    /// A cold instance per case keeps the two funnels independent whatever it caches later.
    /// </summary>
    public static TheoryData<string, Func<SpecBase<IEnumerable<int>, string>>> Families() =>
        new()
        {
            {
                "boolean-predicate",
                () => Spec.Build((int n) => n % 2 == 0)
                    .As(results => results.All(result => result.Satisfied) && ThresholdHolds())
                    .Create("quorum over models")
            },
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

    private static bool ThresholdHolds() => Threshold.Evaluate(2).Satisfied;

    private static SpecBase<int, string> Element() =>
        Spec.Build((int n) => n % 2 == 0).Create("the element is even");

    private static PolicyBase<int, string> ElementPolicy() =>
        Spec.Build((int n) => n % 2 == 0).Create("the element is even");

    private static SpecBase<IEnumerable<int>, string> NonEmpty() =>
        Spec.Build((IEnumerable<int> models) => models.Any()).Create("the collection is not empty");
}
