namespace Motiv.Tests;

public class AsyncMinimalPropositionTests
{
    [Theory]
    [InlineAutoData(2, true)]
    [InlineAutoData(3, false)]
    public async Task Should_evaluate_an_async_minimal_proposition(int model, bool expected)
    {
        // Arrange
        var isEven = Spec
            .BuildAsync(async (int n) =>
            {
                await Task.Yield();
                return n % 2 == 0;
            })
            .Create("is even");

        // Act
        var act = (await isEven.EvaluateAsync(model)).Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(2, "is even == true")]
    [InlineAutoData(3, "is even == false")]
    public async Task Should_assert_using_the_statement_suffix_rule(int model, string expectedAssertion)
    {
        // Arrange
        var isEven = Spec
            .BuildAsync((int n) => Task.FromResult(n % 2 == 0))
            .Create("is even");

        // Act
        var act = (await isEven.EvaluateAsync(model)).Assertions;

        // Assert
        act.ShouldBe([expectedAssertion]);
    }

    [Theory]
    [InlineAutoData(2, true)]
    [InlineAutoData(3, false)]
    public async Task Should_match_using_the_boolean_only_path(int model, bool expected)
    {
        // Arrange
        var isEven = Spec
            .BuildAsync((int n) => Task.FromResult(n % 2 == 0))
            .Create("is even");

        // Act
        var act = await isEven.MatchesAsync(model);

        // Assert
        act.ShouldBe(expected);
    }

    [Fact]
    public async Task Should_pass_the_cancellation_token_to_the_predicate()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        CancellationToken observed = default;
        var spec = Spec
            .BuildAsync((int _, CancellationToken ct) =>
            {
                observed = ct;
                return Task.FromResult(true);
            })
            .Create("observes token");

        // Act
        await spec.EvaluateAsync(0, cts.Token);

        // Assert
        observed.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task Should_produce_a_parity_result_with_the_sync_minimal_proposition()
    {
        // Arrange
        var sync = Spec.Build((int n) => n > 0).Create("is positive");
        var async = Spec.BuildAsync((int n) => Task.FromResult(n > 0)).Create("is positive");

        foreach (var model in new[] { 1, -1 })
        {
            // Act
            var syncResult = sync.Evaluate(model);
            var asyncResult = await async.EvaluateAsync(model);

            // Assert
            asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
            asyncResult.Reason.ShouldBe(syncResult.Reason);
            asyncResult.Assertions.ShouldBe(syncResult.Assertions);
            asyncResult.Justification.ShouldBe(syncResult.Justification);
        }
    }
}
