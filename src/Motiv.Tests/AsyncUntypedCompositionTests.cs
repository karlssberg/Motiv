namespace Motiv.Tests;

public class AsyncUntypedCompositionTests
{
    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(false, false, false)]
    public async Task Should_compose_specs_with_differing_metadata_types(
        bool leftValue, bool rightValue, bool expected, object model)
    {
        // Arrange — int metadata composed with string metadata forces the untyped level
        AsyncSpecBase<object> left = Spec.BuildAsync((object _) => new ValueTask<bool>(leftValue))
            .WhenTrue(1).WhenFalse(0).Create("left");
        AsyncSpecBase<object> right = Spec.BuildAsync((object _) => new ValueTask<bool>(rightValue))
            .WhenTrue("yes").WhenFalse("no").Create("right");

        // Act
        var result = await (left & right).EvaluateAsync(model);

        // Assert
        result.Satisfied.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(false, true)]
    public async Task Should_have_parity_with_sync_untyped_composition(
        bool leftValue, bool rightValue, object model)
    {
        // Arrange
        SpecBase<object> syncLeft = Spec.Build((object _) => leftValue)
            .WhenTrue(1).WhenFalse(0).Create("left");
        SpecBase<object> syncRight = Spec.Build((object _) => rightValue)
            .WhenTrue("yes").WhenFalse("no").Create("right");
        AsyncSpecBase<object> asyncLeft = Spec.BuildAsync((object _) => new ValueTask<bool>(leftValue))
            .WhenTrue(1).WhenFalse(0).Create("left");
        AsyncSpecBase<object> asyncRight = Spec.BuildAsync((object _) => new ValueTask<bool>(rightValue))
            .WhenTrue("yes").WhenFalse("no").Create("right");

        // Act
        var syncResult = (syncLeft & syncRight).Evaluate(model);
        var asyncResult = await (asyncLeft & asyncRight).EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public async Task Should_support_mixed_sync_async_at_the_untyped_level()
    {
        // Arrange
        SpecBase<object> sync = Spec.Build((object _) => true)
            .WhenTrue(1).WhenFalse(0).Create("sync");
        AsyncSpecBase<object> async = Spec.BuildAsync((object _) => new ValueTask<bool>(true))
            .WhenTrue("yes").WhenFalse("no").Create("async");

        // Act & Assert
        (await (sync & async).EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await (async & sync).EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await (!async).EvaluateAsync(new object())).Satisfied.ShouldBeFalse();
        (await async.OrElse(sync).EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
    }
}
