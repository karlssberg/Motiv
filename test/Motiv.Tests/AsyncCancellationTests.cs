namespace Motiv.Tests;

public class AsyncCancellationTests
{
    [Fact]
    public async Task Should_propagate_cancellation_from_a_leaf_predicate()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var spec = Spec
            .BuildAsync(async (object _, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                return true;
            })
            .Create("cancellable");

        // Act
        var act = async () => await spec.EvaluateAsync(new object(), cts.Token);

        // Assert
        await act.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_thread_the_token_through_every_operand_of_a_composition()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var observed = new List<CancellationToken>();

        AsyncPolicyBase<object, string> Leaf(string name) => Spec
            .BuildAsync((object _, CancellationToken ct) =>
            {
                observed.Add(ct);
                return new ValueTask<bool>(true);
            })
            .Create(name);

        var spec = (Leaf("a") & Leaf("b")).AndAlso(!Leaf("c"));

        // Act
        await spec.EvaluateAsync(new object(), cts.Token);

        // Assert
        observed.Count.ShouldBe(3);
        observed.ShouldAllBe(ct => ct == cts.Token);
    }

    [Fact]
    public async Task Should_stop_issuing_predicate_calls_after_cancellation_mid_sequential_evaluation()
    {
        // Arrange — left cancels the source; right observes ThrowIfCancellationRequested
        using var cts = new CancellationTokenSource();
        var rightCalls = 0;

        var left = Spec.BuildAsync((object _, CancellationToken _) =>
        {
            cts.Cancel();
            return new ValueTask<bool>(true);
        }).Create("left");

        var right = Spec.BuildAsync((object _, CancellationToken ct) =>
        {
            rightCalls++;
            ct.ThrowIfCancellationRequested();
            return new ValueTask<bool>(true);
        }).Create("right");

        // Act
        var act = async () => await (left & right).EvaluateAsync(new object(), cts.Token);

        // Assert — the right predicate is entered but must observe the cancelled token and throw
        await act.ShouldThrowAsync<OperationCanceledException>();
        rightCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Should_use_default_token_when_none_supplied()
    {
        // Arrange
        CancellationToken observed = new(canceled: true);
        var spec = Spec
            .BuildAsync((object _, CancellationToken ct) =>
            {
                observed = ct;
                return new ValueTask<bool>(true);
            })
            .Create("default token");

        // Act
        await spec.EvaluateAsync(new object());
        await spec.MatchesAsync(new object());

        // Assert
        observed.ShouldBe(CancellationToken.None);
    }
}
