namespace Motiv.Tests;

public class HigherOrderMatchesShortCircuitTests
{
    [Fact]
    public void AllSatisfied_Matches_short_circuits_on_first_false()
    {
        var calls = 0;
        var spec = Spec
            .Build((int n) => { calls++; return n > 0; })
            .AsAllSatisfied()
            .Create("all positive");

        spec.Matches([-1, 2, 3, 4, 5]).ShouldBeFalse();
        calls.ShouldBe(1); // stopped at the first (false) model
    }

    [Fact]
    public void AnySatisfied_Matches_short_circuits_on_first_true()
    {
        var calls = 0;
        var spec = Spec
            .Build((int n) => { calls++; return n > 0; })
            .AsAnySatisfied()
            .Create("any positive");

        spec.Matches([1, -2, -3, -4]).ShouldBeTrue();
        calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, true)]
    [InlineData(new[] { 1, -2, 3 }, false)]
    [InlineData(new int[0], true)]
    public void AllSatisfied_Matches_equals_Evaluate_Satisfied(int[] models, bool expected)
    {
        var spec = Spec.Build((int n) => n > 0).AsAllSatisfied().Create("all positive");

        spec.Matches(models).ShouldBe(expected);
        spec.Matches(models).ShouldBe(spec.Evaluate(models).Satisfied);
    }

    [Fact]
    public void SpecPredicate_AllSatisfied_Matches_short_circuits_on_first_false()
    {
        var calls = 0;
        SpecBase<int, string> underlying = Spec.Build((int n) => { calls++; return n > 0; }).Create("positive");
        var spec = Spec.Build(underlying).AsAllSatisfied().Create("all positive");

        spec.Matches([-1, 2, 3, 4]).ShouldBeFalse();
        calls.ShouldBe(1);
    }

    [Fact]
    public void SpecPredicate_AnySatisfied_Matches_short_circuits_on_first_true()
    {
        var calls = 0;
        SpecBase<int, string> underlying = Spec.Build((int n) => { calls++; return n > 0; }).Create("positive");
        var spec = Spec.Build(underlying).AsAnySatisfied().Create("any positive");

        spec.Matches([1, -2, -3, -4]).ShouldBeTrue();
        calls.ShouldBe(1);
    }

    [Fact]
    public void PolicyPredicate_AllSatisfied_Matches_short_circuits_on_first_false()
    {
        var calls = 0;
        PolicyBase<int, string> underlying = Spec.Build((int n) => { calls++; return n > 0; }).Create("positive");
        var spec = Spec.Build(underlying).AsAllSatisfied().Create("all positive");

        spec.Matches([-1, 2, 3, 4]).ShouldBeFalse();
        calls.ShouldBe(1);
    }

    [Fact]
    public void PolicyPredicate_AnySatisfied_Matches_short_circuits_on_first_true()
    {
        var calls = 0;
        PolicyBase<int, string> underlying = Spec.Build((int n) => { calls++; return n > 0; }).Create("positive");
        var spec = Spec.Build(underlying).AsAnySatisfied().Create("any positive");

        spec.Matches([1, -2, -3, -4]).ShouldBeTrue();
        calls.ShouldBe(1);
    }

    [Fact]
    public void ExpressionTree_AllSatisfied_Matches_equals_Evaluate_Satisfied()
    {
        var spec = Spec.From((int n) => n > 0).AsAllSatisfied().Create("all positive");

        foreach (var data in new int[][] { [], [1, 2, 3], [1, -2, 3] })
            spec.Matches(data).ShouldBe(spec.Evaluate(data).Satisfied);
    }

    [Theory]
    [InlineData("All")]
    [InlineData("Any")]
    [InlineData("None")]
    public void ExpressionTree_Matches_equals_Evaluate_Satisfied(string op)
    {
        SpecBase<IEnumerable<int>, string> spec = op switch
        {
            "All" => Spec.From((int n) => n > 0).AsAllSatisfied().Create("all positive"),
            "Any" => Spec.From((int n) => n > 0).AsAnySatisfied().Create("any positive"),
            "None" => Spec.From((int n) => n > 0).AsNoneSatisfied().Create("none positive"),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

        foreach (var data in new int[][] { [], [1, 2, 3], [-1, -2, -3], [1, -2, 3] })
            spec.Matches(data).ShouldBe(spec.Evaluate(data).Satisfied);
    }
}
