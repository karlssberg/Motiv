namespace Motiv.Tests;

public class ConcurrentOperatorTests
{
    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public async Task Should_produce_results_indistinguishable_from_sequential_and(
        bool leftValue, bool rightValue, object model)
    {
        // Arrange
        AsyncPolicyBase<object, string> L() =>
            Spec.BuildAsync((object _) => new ValueTask<bool>(leftValue)).Create("left");
        AsyncPolicyBase<object, string> R() =>
            Spec.BuildAsync((object _) => new ValueTask<bool>(rightValue)).Create("right");

        var sequential = L() & R();
        var concurrent = L().AndConcurrently(R());

        // Act
        var sequentialResult = await sequential.EvaluateAsync(model);
        var concurrentResult = await concurrent.EvaluateAsync(model);

        // Assert
        concurrent.Description.Statement.ShouldBe(sequential.Description.Statement);
        concurrentResult.Satisfied.ShouldBe(sequentialResult.Satisfied);
        concurrentResult.Reason.ShouldBe(sequentialResult.Reason);
        concurrentResult.Assertions.ShouldBe(sequentialResult.Assertions);
        concurrentResult.Justification.ShouldBe(sequentialResult.Justification);
    }

    [Fact]
    public async Task Should_evaluate_both_operands_concurrently()
    {
        // Arrange — right's completion unblocks left; deadlocks unless both start before either finishes
        var leftStarted = new TaskCompletionSource<bool>();
        var rightStarted = new TaskCompletionSource<bool>();

        var left = Spec.BuildAsync(async (object _) =>
        {
            leftStarted.SetResult(true);
            // Net472-safe fallback: use WhenAny instead of WaitAsync
            var completed = await Task.WhenAny(rightStarted.Task, Task.Delay(5000));
            if (completed == rightStarted.Task)
            {
                await rightStarted.Task.ConfigureAwait(false);
            }
            else
            {
                throw new TimeoutException("Right did not start within 5 seconds");
            }
            return true;
        }).Create("left");

        var right = Spec.BuildAsync(async (object _) =>
        {
            rightStarted.SetResult(true);
            // Net472-safe fallback: use WhenAny instead of WaitAsync
            var completed = await Task.WhenAny(leftStarted.Task, Task.Delay(5000));
            if (completed == leftStarted.Task)
            {
                await leftStarted.Task.ConfigureAwait(false);
            }
            else
            {
                throw new TimeoutException("Left did not start within 5 seconds");
            }
            return true;
        }).Create("right");

        // Act
        var result = await left.AndConcurrently(right).EvaluateAsync(new object());

        // Assert
        result.Satisfied.ShouldBeTrue();
    }

    [Theory]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    public async Task Should_produce_or_and_xor_parity_with_their_sequential_forms(
        bool leftValue, bool rightValue, object model)
    {
        // Arrange
        AsyncPolicyBase<object, string> L() =>
            Spec.BuildAsync((object _) => new ValueTask<bool>(leftValue)).Create("left");
        AsyncPolicyBase<object, string> R() =>
            Spec.BuildAsync((object _) => new ValueTask<bool>(rightValue)).Create("right");

        // Act
        var orSequential = await (L() | R()).EvaluateAsync(model);
        var orConcurrent = await L().OrConcurrently(R()).EvaluateAsync(model);
        var xorSequential = await (L() ^ R()).EvaluateAsync(model);
        var xorConcurrent = await L().XOrConcurrently(R()).EvaluateAsync(model);

        // Assert
        orConcurrent.Reason.ShouldBe(orSequential.Reason);
        orConcurrent.Justification.ShouldBe(orSequential.Justification);
        xorConcurrent.Reason.ShouldBe(xorSequential.Reason);
        xorConcurrent.Justification.ShouldBe(xorSequential.Justification);
    }

    [Fact]
    public async Task Should_propagate_the_first_exception_like_when_all()
    {
        // Arrange
        var left = Spec.BuildAsync((object _) =>
            new ValueTask<bool>(Task.FromException<bool>(new InvalidOperationException("left failed")))).Create("left");
        var right = Spec.BuildAsync((object _) => new ValueTask<bool>(true)).Create("right");

        // Act
        var act = async () => await left.AndConcurrently(right).EvaluateAsync(new object());

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe("left failed");
    }
}
