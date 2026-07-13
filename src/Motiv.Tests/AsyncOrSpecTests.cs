namespace Motiv.Tests;

public class AsyncOrSpecTests
{
    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public async Task Should_perform_logical_or_with_sync_parity(
        bool leftValue, bool rightValue, bool expected, object model)
    {
        // Arrange
        var syncSpec = Spec.Build((object _) => leftValue).Create("left")
            | Spec.Build((object _) => rightValue).Create("right");
        var asyncSpec = Spec.BuildAsync((object _) => Task.FromResult(leftValue)).Create("left")
            | Spec.BuildAsync((object _) => Task.FromResult(rightValue)).Create("right");

        // Act
        var syncResult = syncSpec.Evaluate(model);
        var asyncResult = await asyncSpec.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(expected);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public async Task Should_perform_conditional_or_with_sync_parity(
        bool leftValue, bool rightValue, bool expected, object model)
    {
        // Arrange
        var syncSpec = Spec.Build((object _) => leftValue).Create("left")
            .OrElse(Spec.Build((object _) => rightValue).Create("right"));
        var asyncSpec = Spec.BuildAsync((object _) => Task.FromResult(leftValue)).Create("left")
            .OrElse(Spec.BuildAsync((object _) => Task.FromResult(rightValue)).Create("right"));

        // Act
        var syncResult = syncSpec.Evaluate(model);
        var asyncResult = await asyncSpec.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(expected);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public async Task Should_not_evaluate_the_right_operand_of_or_else_when_the_left_is_true()
    {
        // Arrange
        var rightCalls = 0;
        var left = Spec.BuildAsync((object _) => Task.FromResult(true)).Create("left");
        var right = Spec.BuildAsync((object _) =>
        {
            rightCalls++;
            return Task.FromResult(false);
        }).Create("right");

        // Act
        var result = await left.OrElse(right).EvaluateAsync(new object());

        // Assert
        result.Satisfied.ShouldBeTrue();
        rightCalls.ShouldBe(0);
    }

    [Fact]
    public void Should_collapse_nested_or_statements_like_sync()
    {
        // Arrange
        PolicyBase<object, string> S(string name) => Spec.Build((object _) => true).Create(name);
        AsyncPolicyBase<object, string> A(string name) =>
            Spec.BuildAsync((object _) => Task.FromResult(true)).Create(name);

        var syncSpec = (S("a") | S("b")) | S("c");
        var asyncSpec = (A("a") | A("b")) | A("c");

        // Act & Assert
        asyncSpec.Description.Statement.ShouldBe(syncSpec.Description.Statement);
        asyncSpec.Expression.ShouldBe(syncSpec.Expression);
    }
}
