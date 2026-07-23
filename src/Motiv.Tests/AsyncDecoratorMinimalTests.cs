namespace Motiv.Tests;

public class AsyncDecoratorMinimalTests
{
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_rename_a_composed_async_spec_with_parity_to_sync(bool value, object model)
    {
        // Arrange — same composition shape, sync and async
        var syncSpec = Spec.Build((object _) => value).Create("left")
            .And(Spec.Build((object _) => true).Create("right"));
        var asyncSpec = Spec.BuildAsync((object _) => new ValueTask<bool>(value)).Create("left")
            .And(Spec.BuildAsync((object _) => new ValueTask<bool>(true)).Create("right"));

        var syncNamed = Spec.Build(syncSpec).Create("composite");
        var asyncNamed = Spec.Build(asyncSpec).Create("composite");

        // Act
        var syncResult = syncNamed.Evaluate(model);
        var asyncResult = await asyncNamed.EvaluateAsync(model);

        // Assert — full explanation parity
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public async Task Should_expose_the_underlying_spec_and_new_description()
    {
        // Arrange
        var inner = Spec.BuildAsync((int _) => new ValueTask<bool>(true)).Create("inner");

        // Act
        var named = Spec.Build(inner).Create("outer");

        // Assert — the statement is renamed, so Reason takes the new name, while the underlying
        // explanation passes through untouched (parity with the sync minimal decorator, whose
        // MinimalSpecDecoratorBooleanResult mirrors the underlying Explanation)
        named.Description.Statement.ShouldBe("outer");
        named.Underlying.ShouldBe([inner]);
        var result = await named.EvaluateAsync(0);
        result.Reason.ShouldBe("outer == true");
        result.Assertions.ShouldBe(["inner == true"]);
    }

    [Fact]
    public async Task Should_build_from_a_spec_factory_function_identically_to_the_direct_spec_form()
    {
        // Arrange
        var inner = Spec.BuildAsync((int _) => new ValueTask<bool>(true)).Create("inner");

        var fromSpec = Spec.Build(inner).Create("outer");
        var fromFactory = Spec.Build(() => inner).Create("outer");

        // Act
        var specResult = await fromSpec.EvaluateAsync(0);
        var factoryResult = await fromFactory.EvaluateAsync(0);

        // Assert
        factoryResult.Reason.ShouldBe(specResult.Reason);
        factoryResult.Assertions.ShouldBe(specResult.Assertions);
    }

    [Fact]
    public async Task Should_pass_the_cancellation_token_through_to_the_underlying_spec()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        CancellationToken observed = default;
        var inner = Spec.BuildAsync((int _, CancellationToken ct) =>
        {
            observed = ct;
            return new ValueTask<bool>(true);
        }).Create("inner");

        // Act
        await Spec.Build(inner).Create("outer").EvaluateAsync(0, cts.Token);

        // Assert
        observed.ShouldBe(cts.Token);
    }
}
