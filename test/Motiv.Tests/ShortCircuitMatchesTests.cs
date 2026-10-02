namespace Motiv.Tests;

/// <summary>
/// Pins <c>Matches</c> — the allocation-free outcome — for the operator variants no test matched through.
/// A short-circuiting operator combines its outcome as <c>second ?? first</c>: the right operand's
/// outcome when it was evaluated, the left's when it was skipped. Mutation testing (#294) showed that
/// replacing that with <c>first</c> alone survived on <c>AndAlsoSpec</c>, <c>AsyncAndAlsoPolicy</c>,
/// <c>ExpressionAndAlsoPolicy</c> and <c>ExpressionOrElsePolicy</c>, and that <c>NotSpec</c>'s
/// <c>!first</c> could lose its negation — only <c>Evaluate</c> had been asked of them.
/// </summary>
public class ShortCircuitMatchesTests
{
    private static SpecBase<int, string> AsSpec(bool value, string name) =>
        Spec.Build((int _) => value).Create(name);

    private static AsyncPolicyBase<int, string> AsyncPolicy(bool value, string name) =>
        Spec.BuildAsync((int _) => new ValueTask<bool>(value)).Create(name);

    private static ExpressionPolicyBase<int, string> IsPositive() =>
        Spec.From((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    private static ExpressionPolicyBase<int, string> IsSmall() =>
        Spec.From((int n) => n < 100).WhenTrue("is small").WhenFalse("is not small").Create();

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Should_match_an_and_also_spec(bool left, bool right, bool expected)
    {
        // Declared as specs, so AndAlso composes an AndAlsoSpec rather than an AndAlsoPolicy.
        var sut = AsSpec(left, "left").AndAlso(AsSpec(right, "right"));

        sut.Matches(0).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task Should_match_an_async_and_also_policy(bool left, bool right, bool expected)
    {
        var sut = AsyncPolicy(left, "left").AndAlso(AsyncPolicy(right, "right"));

        (await sut.MatchesAsync(0)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(200, false)] // positive, not small
    [InlineData(50, true)]   // positive and small
    [InlineData(-5, false)]  // not positive: the right operand is skipped
    public void Should_match_an_expression_and_also_policy(int model, bool expected)
    {
        var sut = IsPositive().AndAlso(IsSmall());

        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(-5, true)]   // not positive, but small
    [InlineData(50, true)]   // positive: the right operand is skipped
    public void Should_match_an_expression_or_else_policy(int model, bool expected)
    {
        var sut = IsPositive().OrElse(IsSmall());

        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Should_match_a_negated_spec(bool value, bool expected)
    {
        // Declared as a spec, so Not composes a NotSpec rather than a NotPolicy.
        var sut = AsSpec(value, "value").Not();

        sut.Matches(0).ShouldBe(expected);
    }
}
