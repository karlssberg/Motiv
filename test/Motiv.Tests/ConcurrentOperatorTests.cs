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

    /// <summary>
    /// The same claim for a <em>nest</em>, which is folded rather than nested since
    /// <see href="https://github.com/karlssberg/Motiv/issues/145">#145</see>: an unbroken run of
    /// concurrent operations is absorbed into one region and every operand at its boundary is started
    /// together. The composition it produces has to be the one the nesting produced, node for node.
    /// </summary>
    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(false, false, false)]
    public async Task Should_produce_nested_results_indistinguishable_from_sequential_ones(
        bool first, bool second, bool third, object model)
    {
        // Arrange
        AsyncPolicyBase<object, string> Operand(string name, bool value) =>
            Spec.BuildAsync((object _) => new ValueTask<bool>(value)).Create(name);

        var sequential = Operand("first", first) & Operand("second", second) | Operand("third", third);
        var concurrent = Operand("first", first)
            .AndConcurrently(Operand("second", second))
            .OrConcurrently(Operand("third", third));

        // Act
        var sequentialResult = await sequential.EvaluateAsync(model);
        var concurrentResult = await concurrent.EvaluateAsync(model);

        // Assert
        concurrentResult.Satisfied.ShouldBe(sequentialResult.Satisfied);
        concurrentResult.Reason.ShouldBe(sequentialResult.Reason);
        concurrentResult.Assertions.ShouldBe(sequentialResult.Assertions);
        concurrentResult.Justification.ShouldBe(sequentialResult.Justification);
    }

    /// <summary>
    /// Flattening a nest must not serialize it. Each of the three operands announces itself and then
    /// waits for the other two, so the composition completes only if all three were in flight at once
    /// — and times out rather than hanging if the region were ever walked in order.
    /// </summary>
    [Fact]
    public async Task Should_start_every_operand_of_a_nest_at_once()
    {
        // Arrange
        var started = new[]
        {
            new TaskCompletionSource<bool>(),
            new TaskCompletionSource<bool>(),
            new TaskCompletionSource<bool>()
        };

        AsyncPolicyBase<object, string> Operand(int index) =>
            Spec.BuildAsync(async (object _) =>
            {
                started[index].SetResult(true);

                // Net472-safe fallback: WhenAny rather than WaitAsync, as the pairwise case above uses.
                var all = Task.WhenAll(started.Select(source => source.Task));
                if (await Task.WhenAny(all, Task.Delay(5000)).ConfigureAwait(false) != all)
                    throw new TimeoutException("The nest's operands did not all start.");

                return true;
            }).Create($"operand{index}");

        var nest = Operand(0).AndConcurrently(Operand(1)).AndConcurrently(Operand(2));

        // Act
        var result = await nest.EvaluateAsync(new object());

        // Assert
        result.Satisfied.ShouldBeTrue();
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
