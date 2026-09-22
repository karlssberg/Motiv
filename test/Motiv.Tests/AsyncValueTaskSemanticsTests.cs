namespace Motiv.Tests;

/// <summary>
/// Verifies that the asynchronous evaluation pipeline is ValueTask-based, so that synchronously-completing
/// evaluations (e.g. sync specs lifted via ToAsyncSpec, or async predicates that complete synchronously)
/// resolve without allocating a Task per node.
/// </summary>
public class AsyncValueTaskSemanticsTests
{
    [Fact]
    public async Task Should_complete_synchronously_when_evaluating_a_sync_spec_lifted_into_the_async_hierarchy()
    {
        // Arrange
        var isPositive = Spec
            .Build((int n) => n > 0)
            .Create("is positive")
            .ToAsyncSpec();

        // Act
        ValueTask<PolicyResultBase<string>> pending = isPositive.EvaluateAsync(42);

        // Assert
        pending.IsCompletedSuccessfully.ShouldBeTrue();
        (await pending).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_complete_synchronously_when_matching_a_sync_spec_lifted_into_the_async_hierarchy()
    {
        // Arrange
        var isPositive = Spec
            .Build((int n) => n > 0)
            .Create("is positive")
            .ToAsyncSpec();

        // Act
        ValueTask<bool> pending = isPositive.MatchesAsync(-1);

        // Assert
        pending.IsCompletedSuccessfully.ShouldBeTrue();
        (await pending).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_complete_synchronously_when_composed_operands_all_complete_synchronously()
    {
        // Arrange
        var isPositive = Spec
            .BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .Create("is positive");

        var isEven = Spec
            .BuildAsync((int n) => new ValueTask<bool>(n % 2 == 0))
            .Create("is even");

        var isPositiveAndEven = isPositive & isEven;

        // Act
        ValueTask<BooleanResultBase<string>> pending = isPositiveAndEven.EvaluateAsync(42);

        // Assert
        pending.IsCompletedSuccessfully.ShouldBeTrue();
        (await pending).Reason.ShouldBe("(is positive == true) & (is even == true)");
    }

    [Fact]
    public async Task Should_complete_synchronously_when_evaluating_a_policy_whose_predicate_completes_synchronously()
    {
        // Arrange
        var isPositive = Spec
            .BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

        // Act
        ValueTask<PolicyResultBase<string>> pending = isPositive.EvaluateAsync(42);

        // Assert
        pending.IsCompletedSuccessfully.ShouldBeTrue();
        (await pending).Reason.ShouldBe("is positive");
    }

    [Fact]
    public async Task Should_still_complete_asynchronously_when_a_predicate_genuinely_yields()
    {
        // Arrange
        var isPositive = Spec
            .BuildAsync(async (int n, CancellationToken ct) =>
            {
                await Task.Yield();
                return n > 0;
            })
            .Create("is positive");

        // Act
        var result = await isPositive.EvaluateAsync(42);

        // Assert
        result.Satisfied.ShouldBeTrue();
    }
}
