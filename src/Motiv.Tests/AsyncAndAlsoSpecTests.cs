namespace Motiv.Tests;

public class AsyncAndAlsoSpecTests
{
    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(false, false, false)]
    public async Task Should_perform_conditional_and_with_sync_parity(
        bool leftValue, bool rightValue, bool expected, object model)
    {
        // Arrange
        var syncSpec = Spec.Build((object _) => leftValue).Create("left")
            .AndAlso(Spec.Build((object _) => rightValue).Create("right"));
        var asyncSpec = Spec.BuildAsync((object _) => new ValueTask<bool>(leftValue)).Create("left")
            .AndAlso(Spec.BuildAsync((object _) => new ValueTask<bool>(rightValue)).Create("right"));

        // Act
        var syncResult = syncSpec.Evaluate(model);
        var asyncResult = await asyncSpec.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(expected);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public async Task Should_not_evaluate_the_right_operand_when_the_left_is_false()
    {
        // Arrange
        var rightCalls = 0;
        var left = Spec.BuildAsync((object _) => new ValueTask<bool>(false)).Create("left");
        var right = Spec.BuildAsync((object _) =>
        {
            rightCalls++;
            return new ValueTask<bool>(true);
        }).Create("right");

        // Act
        var result = await left.AndAlso(right).EvaluateAsync(new object());
        var matches = await left.AndAlso(right).MatchesAsync(new object());

        // Assert
        result.Satisfied.ShouldBeFalse();
        matches.ShouldBeFalse();
        rightCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Should_evaluate_the_right_operand_when_the_left_is_true()
    {
        // Arrange
        var rightCalls = 0;
        var left = Spec.BuildAsync((object _) => new ValueTask<bool>(true)).Create("left");
        var right = Spec.BuildAsync((object _) =>
        {
            rightCalls++;
            return new ValueTask<bool>(true);
        }).Create("right");

        // Act
        await left.AndAlso(right).EvaluateAsync(new object());

        // Assert
        rightCalls.ShouldBe(1);
    }
}
