using Motiv.HigherOrderProposition;

namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The evaluation a higher-order proposition hands to its <c>WhenTrue</c>/<c>WhenFalse</c> delegates filters
/// and projects its results once, however many of its members the caller reads, and in whatever order (#294).
/// <para>
/// <c>TrueModels</c> and <c>TrueCount</c> filter through the same memo as <c>TrueResults</c> (and likewise for
/// false), so each test reads the results first, then the members that share their memo, then the results
/// again: a member that refiltered instead of reusing the memo would replace the list the first read returned.
/// </para>
/// </summary>
public class HigherOrderEvaluationMemoIdentityTests
{
    private static readonly int[] Models = [2, 4, 5];

    [Fact]
    public void A_boolean_predicate_evaluation_projects_its_models_once()
    {
        HigherOrderBooleanEvaluation<int>? evaluation = null;
        Spec.Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue(e => Capture(e, out evaluation))
            .WhenFalse(e => Capture(e, out evaluation))
            .Create("all even")
            .Evaluate(Models)
            .Value.ShouldNotBeNull();

        evaluation.ShouldNotBeNull();
        evaluation.TrueModels.ShouldNotBeEmpty();
        evaluation.FalseModels.ShouldNotBeEmpty();
        ReadTwice(evaluation,
            ("Models", e => e.Models),
            ("TrueModels", e => e.TrueModels),
            ("FalseModels", e => e.FalseModels),
            ("CausalModels", e => e.CausalModels));
    }

    [Fact]
    public void A_spec_evaluation_filters_and_projects_its_results_once()
    {
        HigherOrderBooleanResultEvaluation<int, string>? evaluation = null;
        SpecBase<int, string> isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        Spec.Build(isEven)
            .AsAllSatisfied()
            .WhenTrue(e => Capture(e, out evaluation))
            .WhenFalse(e => Capture(e, out evaluation))
            .Create("all even")
            .Evaluate(Models)
            .Value.ShouldNotBeNull();

        evaluation.ShouldNotBeNull();
        var trueResults = evaluation.TrueResults;
        var falseResults = evaluation.FalseResults;
        trueResults.ShouldNotBeEmpty();
        falseResults.ShouldNotBeEmpty();
        _ = evaluation.TrueModels;
        _ = evaluation.FalseModels;
        _ = evaluation.TrueCount;
        _ = evaluation.FalseCount;
        evaluation.TrueResults.ShouldBeSameAs(trueResults);
        evaluation.FalseResults.ShouldBeSameAs(falseResults);

        ReadTwice(evaluation,
            ("Models", e => e.Models),
            ("TrueModels", e => e.TrueModels),
            ("FalseModels", e => e.FalseModels),
            ("CausalModels", e => e.CausalModels),
            ("Values", e => e.Values),
            ("Assertions", e => e.Assertions));
    }

    [Fact]
    public void A_policy_evaluation_filters_and_projects_its_results_once()
    {
        HigherOrderPolicyResultEvaluation<int, string>? evaluation = null;
        Spec.Build(Spec.Build((int n) => n % 2 == 0).Create("is even"))
            .AsAllSatisfied()
            .WhenTrue(e => Capture(e, out evaluation))
            .WhenFalse(e => Capture(e, out evaluation))
            .Create("all even")
            .Evaluate(Models)
            .Value.ShouldNotBeNull();

        evaluation.ShouldNotBeNull();
        var trueResults = evaluation.TrueResults;
        var falseResults = evaluation.FalseResults;
        trueResults.ShouldNotBeEmpty();
        falseResults.ShouldNotBeEmpty();
        _ = evaluation.TrueModels;
        _ = evaluation.FalseModels;
        _ = evaluation.TrueCount;
        _ = evaluation.FalseCount;
        evaluation.TrueResults.ShouldBeSameAs(trueResults);
        evaluation.FalseResults.ShouldBeSameAs(falseResults);

        ReadTwice(evaluation,
            ("Models", e => e.Models),
            ("TrueModels", e => e.TrueModels),
            ("FalseModels", e => e.FalseModels),
            ("CausalModels", e => e.CausalModels),
            ("Metadata", e => e.Metadata),
            ("Values", e => e.Values),
            ("Assertions", e => e.Assertions));
    }

    private static string Capture<TEvaluation>(TEvaluation captured, out TEvaluation? evaluation)
    {
        evaluation = captured;
        return "evaluated";
    }

    private static void ReadTwice<TEvaluation>(
        TEvaluation evaluation,
        params (string Member, Func<TEvaluation, object> Read)[] readers) =>
        evaluation.ShouldRebuildNothingOnASecondRead(readers, typeof(TEvaluation).NameWithoutArity());
}
