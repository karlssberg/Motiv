namespace Motiv.Tests;

public class AsyncNotSpecTests
{
    [Theory]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    public async Task Should_negate_with_sync_parity(bool value, bool expected, object model)
    {
        // Arrange
        var syncSpec = !Spec.Build((object _) => value).Create("flag");
        var asyncSpec = !Spec.BuildAsync((object _) => Task.FromResult(value)).Create("flag");

        // Act
        var syncResult = syncSpec.Evaluate(model);
        var asyncResult = await asyncSpec.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(expected);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
        asyncSpec.Description.Statement.ShouldBe(syncSpec.Description.Statement);
    }

    [Fact]
    public void Should_negate_binary_compositions_with_parenthesised_statements()
    {
        // Arrange
        var syncSpec = !(Spec.Build((object _) => true).Create("a")
            & Spec.Build((object _) => true).Create("b"));
        var asyncSpec = !(Spec.BuildAsync((object _) => Task.FromResult(true)).Create("a")
            & Spec.BuildAsync((object _) => Task.FromResult(true)).Create("b"));

        // Act & Assert
        asyncSpec.Description.Statement.ShouldBe(syncSpec.Description.Statement); // "!(a & b)"
    }

    [Fact]
    public async Task Should_preserve_policy_ness_through_not()
    {
        // Arrange
        AsyncPolicyBase<object, string> policy =
            Spec.BuildAsync((object _) => Task.FromResult(true)).Create("flag");

        // Act
        AsyncPolicyBase<object, string> negated = !policy;   // must compile as a policy
        PolicyResultBase<string> result = await negated.EvaluateAsync(new object());

        // Assert
        result.Satisfied.ShouldBeFalse();
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public async Task Should_preserve_policy_ness_through_or_else(bool leftValue, bool rightValue, object model)
    {
        // Arrange
        AsyncPolicyBase<object, string> left =
            Spec.BuildAsync((object _) => Task.FromResult(leftValue)).Create("left");
        AsyncPolicyBase<object, string> right =
            Spec.BuildAsync((object _) => Task.FromResult(rightValue)).Create("right");

        var syncLeft = Spec.Build((object _) => leftValue).Create("left");
        var syncRight = Spec.Build((object _) => rightValue).Create("right");

        // Act
        AsyncPolicyBase<object, string> combined = left.OrElse(right);   // must compile as a policy
        PolicyResultBase<string> result = await combined.EvaluateAsync(model);
        var syncResult = syncLeft.OrElse(syncRight).Evaluate(model);

        // Assert
        result.Satisfied.ShouldBe(syncResult.Satisfied);
        result.Reason.ShouldBe(syncResult.Reason);
        result.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public async Task Should_short_circuit_or_else_policy()
    {
        // Arrange
        var rightCalls = 0;
        AsyncPolicyBase<object, string> left =
            Spec.BuildAsync((object _) => Task.FromResult(true)).Create("left");
        AsyncPolicyBase<object, string> right =
            Spec.BuildAsync((object _) =>
            {
                rightCalls++;
                return Task.FromResult(false);
            }).Create("right");

        // Act
        await left.OrElse(right).EvaluateAsync(new object());

        // Assert
        rightCalls.ShouldBe(0);
    }
}
