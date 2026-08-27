namespace Motiv.Tests.Traversal;

/// <summary>
/// The evaluation size bound — ticket 19's replacement for the stack overflow that used to cap how much
/// a single composition could cost, and which this slice removes.
/// </summary>
[Collection(MotivLimitsTestCollection.Name)]
public class MotivLimitsTests : IDisposable
{
    private readonly int _previous = MotivLimits.MaxEvaluationSize;

    public void Dispose() => MotivLimits.MaxEvaluationSize = _previous;

    [Fact]
    public void Should_abandon_an_evaluation_that_exceeds_the_size_limit()
    {
        MotivLimits.MaxEvaluationSize = 10;

        var act = () => Chain(50).Evaluate(2);

        act.ShouldThrow<SpecException>()
            .Message.ShouldContain("10");
    }

    /// <summary>
    /// The bound applies to the allocation-free path too, so a composition one entry point accepts is
    /// never one the other refuses.
    /// </summary>
    [Fact]
    public void Should_abandon_a_match_that_exceeds_the_size_limit()
    {
        MotivLimits.MaxEvaluationSize = 10;

        var act = () => { _ = Chain(50).Matches(2); };

        act.ShouldThrow<SpecException>();
    }

    [Fact]
    public void Should_name_the_setting_to_raise()
    {
        MotivLimits.MaxEvaluationSize = 4;

        var act = () => Chain(50).Evaluate(2);

        act.ShouldThrow<SpecException>()
            .Message.ShouldContain(nameof(MotivLimits.MaxEvaluationSize));
    }

    /// <summary>
    /// A chain of <c>n</c> propositions composes <c>2n - 1</c> nodes — one per proposition and one per
    /// operation joining them — so the limit is expressed in nodes rather than in propositions.
    /// </summary>
    [Fact]
    public void Should_admit_a_composition_of_exactly_the_limit()
    {
        MotivLimits.MaxEvaluationSize = 19;

        Chain(10).Evaluate(2).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_abandon_a_composition_one_node_past_the_limit()
    {
        MotivLimits.MaxEvaluationSize = 19;

        var act = () => Chain(11).Evaluate(2);

        act.ShouldThrow<SpecException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_reject_a_limit_below_one(int value)
    {
        var act = () => { MotivLimits.MaxEvaluationSize = value; };

        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Should_default_to_a_limit_no_composition_in_this_suite_approaches() =>
        MotivLimits.MaxEvaluationSize.ShouldBe(MotivLimits.DefaultMaxEvaluationSize);

    private static SpecBase<int, string> Chain(int operands) =>
        Enumerable
            .Range(0, operands)
            .Select(i => (SpecBase<int, string>)Spec.Build((int n) => n % 2 == 0).Create($"p{i} is even"))
            .Aggregate((left, right) => left.And(right));
}
