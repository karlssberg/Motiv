namespace Motiv.Tests;

/// <summary>
/// Covers the higher-order boolean-only <c>Matches</c> fast path (introduced alongside
/// <see cref="Motiv.HigherOrderProposition.HigherOrderShortCircuit"/>) across every builder-terminal variant
/// (Minimal, Explanation, Metadata, MultiAssertion/MultiMetadata) and every underlying-predicate family
/// (BooleanPredicate, BooleanResultPredicate, PolicyResultPredicate, ExpressionTree), plus the custom-predicate
/// fallback branch that cannot short-circuit.
/// </summary>
public class HigherOrderMatchesCoverageTests
{
    private enum Verdict { Positive, Negative }

    private static readonly int[][] Datasets = [[1, 2, 3], [1, -2, 3]];

    private static void AssertMatchesTracksEvaluate<TMetadata>(SpecBase<IEnumerable<int>, TMetadata> spec)
    {
        foreach (var data in Datasets)
            spec.Matches(data).ShouldBe(spec.Evaluate(data).Satisfied, $"[{string.Join(",", data)}]");
    }

    // ---------- BooleanPredicate family: Spec.Build((int n) => n > 0) ----------

    [Fact]
    public void BooleanPredicate_Minimal_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build((int n) => n > 0).AsAllSatisfied().Create("all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanPredicate_Explanation_unnamed_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("not all positive")
            .Create();
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanPredicate_Explanation_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("not all positive")
            .Create("named all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanPredicate_Metadata_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(Verdict.Positive)
            .WhenFalse(Verdict.Negative)
            .Create("metadata all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanPredicate_MultiAssertionExplanation_unnamed_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalseYield(_ => ["not all positive"])
            .Create();
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanPredicate_MultiMetadata_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrueYield(_ => new[] { Verdict.Positive })
            .WhenFalseYield(_ => new[] { Verdict.Negative })
            .Create("multi metadata all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanPredicate_CustomPredicate_fallback_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied))
            .Create("custom predicate all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    // ---------- BooleanResultPredicate family: Spec.Build(SpecBase<int,string> underlying) ----------

    private static SpecBase<int, string> BooleanResultUnderlying() =>
        Spec.Build((int n) => n > 0).Create("positive");

    [Fact]
    public void BooleanResultPredicate_Minimal_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(BooleanResultUnderlying()).AsAllSatisfied().Create("all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanResultPredicate_Explanation_unnamed_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(BooleanResultUnderlying())
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("not all positive")
            .Create();
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanResultPredicate_Explanation_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(BooleanResultUnderlying())
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("not all positive")
            .Create("named all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanResultPredicate_Metadata_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(BooleanResultUnderlying())
            .AsAllSatisfied()
            .WhenTrue(Verdict.Positive)
            .WhenFalse(Verdict.Negative)
            .Create("metadata all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanResultPredicate_MultiAssertionExplanation_unnamed_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(BooleanResultUnderlying())
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalseYield(_ => ["not all positive"])
            .Create();
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanResultPredicate_MultiMetadata_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(BooleanResultUnderlying())
            .AsAllSatisfied()
            .WhenTrueYield(_ => new[] { Verdict.Positive })
            .WhenFalseYield(_ => new[] { Verdict.Negative })
            .Create("multi metadata all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void BooleanResultPredicate_CustomPredicate_fallback_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(BooleanResultUnderlying())
            .As(results => results.All(r => r.Satisfied))
            .Create("custom predicate all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    // ---------- PolicyResultPredicate family: Spec.Build(PolicyBase<int,string> underlying) ----------

    private static PolicyBase<int, string> PolicyResultUnderlying() =>
        Spec.Build((int n) => n > 0).Create("positive");

    [Fact]
    public void PolicyResultPredicate_Minimal_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(PolicyResultUnderlying()).AsAllSatisfied().Create("all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void PolicyResultPredicate_Explanation_unnamed_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(PolicyResultUnderlying())
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("not all positive")
            .Create();
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void PolicyResultPredicate_Explanation_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(PolicyResultUnderlying())
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("not all positive")
            .Create("named all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void PolicyResultPredicate_Metadata_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(PolicyResultUnderlying())
            .AsAllSatisfied()
            .WhenTrue(Verdict.Positive)
            .WhenFalse(Verdict.Negative)
            .Create("metadata all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void PolicyResultPredicate_MultiAssertionExplanation_unnamed_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(PolicyResultUnderlying())
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalseYield(_ => ["not all positive"])
            .Create();
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void PolicyResultPredicate_MultiMetadata_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(PolicyResultUnderlying())
            .AsAllSatisfied()
            .WhenTrueYield(_ => new[] { Verdict.Positive })
            .WhenFalseYield(_ => new[] { Verdict.Negative })
            .Create("multi metadata all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void PolicyResultPredicate_CustomPredicate_fallback_Matches_tracks_Evaluate()
    {
        var spec = Spec.Build(PolicyResultUnderlying())
            .As(results => results.All(r => r.Satisfied))
            .Create("custom predicate all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    // ---------- ExpressionTree family: Spec.From((int n) => n > 0) ----------

    [Fact]
    public void ExpressionTree_Minimal_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0).AsAllSatisfied().Create("all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void ExpressionTree_Explanation_unnamed_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("not all positive")
            .Create();
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void ExpressionTree_Explanation_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("not all positive")
            .Create("named all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void ExpressionTree_Metadata_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(Verdict.Positive)
            .WhenFalse(Verdict.Negative)
            .Create("metadata all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void ExpressionTree_MultiAssertionExplanation_unnamed_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalseYield(_ => ["not all positive"])
            .Create();
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void ExpressionTree_MultiMetadata_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrueYield(_ => new[] { Verdict.Positive })
            .WhenFalseYield(_ => new[] { Verdict.Negative })
            .Create("multi metadata all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void ExpressionTree_CustomPredicate_fallback_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied))
            .Create("custom predicate all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    // ExpressionTree also exposes a delegate-form WhenTrue/WhenFalse ("evaluation => ...") in addition to the
    // literal-string form exercised above; it routes through a distinct factory/proposition pairing.

    [Fact]
    public void ExpressionTree_Explanation_delegate_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(_ => "all positive")
            .WhenFalse(_ => "not all positive")
            .Create("delegate all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void ExpressionTree_Metadata_unnamed_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(Verdict.Positive)
            .WhenFalse(Verdict.Negative)
            .Create();
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void ExpressionTree_MultiAssertionExplanation_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalseYield(_ => ["not all positive"])
            .Create("named multi assertion all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    [Fact]
    public void ExpressionTree_MultiAssertionExplanation_delegate_named_Matches_tracks_Evaluate()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrueYield(_ => new[] { "all positive" })
            .WhenFalseYield(_ => new[] { "not all positive" })
            .Create("delegate multi assertion all positive");
        AssertMatchesTracksEvaluate(spec);
    }

    // ---------- AsAtLeastNSatisfied / AsAtMostNSatisfied cause-selector coverage ----------
    //
    // The short-circuit fast path never touches the causeSelector lambda passed to each higher-order operation
    // (it only runs when the full Evaluate/justification path materializes causes), so it needs a dedicated
    // Evaluate(...).Justification read per family to be exercised.

    [Fact]
    public void BooleanPredicate_AtLeast_and_AtMost_CauseSelector_are_exercised_via_Values()
    {
        // Minimal propositions produce a PolicyResult whose Value getter forces the lazily-cached
        // HigherOrderBooleanEvaluation (and, in turn, the causeSelector lambda captured by AsAtLeastNSatisfied /
        // AsAtMostNSatisfied) to run; reading Values (which is backed by Value) triggers it without a cast.
        var atLeast = Spec.Build((int n) => n > 0).AsAtLeastNSatisfied(2).Create("at least two positive");
        var atMost = Spec.Build((int n) => n > 0).AsAtMostNSatisfied(1).Create("at most one positive");

        atLeast.Evaluate([1, 2, -3]).Values.ShouldNotBeEmpty();
        atMost.Evaluate([1, 2, -3]).Values.ShouldNotBeEmpty();
    }

    [Fact]
    public void BooleanResultPredicate_AtMost_CauseSelector_is_exercised_via_Justification()
    {
        var atMost = Spec.Build(BooleanResultUnderlying()).AsAtMostNSatisfied(1).Create("at most one positive");

        atMost.Evaluate([1, 2, -3]).Justification.ShouldNotBeNull();
    }
}
