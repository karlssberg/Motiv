namespace Motiv.Tests;

public class AsyncXOrSpecTests
{
    [Theory]
    [InlineAutoData(true, true, false)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public async Task Should_perform_logical_xor_with_sync_parity(
        bool leftValue, bool rightValue, bool expected, object model)
    {
        // Arrange
        var syncSpec = Spec.Build((object _) => leftValue).Create("left")
            ^ Spec.Build((object _) => rightValue).Create("right");
        var asyncSpec = Spec.BuildAsync((object _) => new ValueTask<bool>(leftValue)).Create("left")
            ^ Spec.BuildAsync((object _) => new ValueTask<bool>(rightValue)).Create("right");

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
    public async Task Should_always_surface_both_operand_assertions()
    {
        // Arrange
        var asyncSpec = Spec.BuildAsync((object _) => new ValueTask<bool>(true)).Create("left")
            ^ Spec.BuildAsync((object _) => new ValueTask<bool>(false)).Create("right");

        // Act
        var result = await asyncSpec.EvaluateAsync(new object());

        // Assert
        result.Assertions.ShouldBe(["left == true", "right == false"], ignoreOrder: true);
    }
}
