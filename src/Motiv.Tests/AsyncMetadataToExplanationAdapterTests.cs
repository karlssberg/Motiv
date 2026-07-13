namespace Motiv.Tests;

public class AsyncMetadataToExplanationAdapterTests
{
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_adapt_async_metadata_specs_to_explanation_specs_with_sync_parity(bool model)
    {
        // Arrange
        var sync = Spec.Build((bool b) => b)
            .WhenTrue(1).WhenFalse(0).Create("has value");
        var async = Spec.BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue(1).WhenFalse(0).Create("has value");

        // Act — untyped EvaluateAsync goes through ToAsyncExplanationSpec
        var syncResult = ((SpecBase<bool>)sync).Evaluate(model);
        var asyncResult = await ((AsyncSpecBase<bool>)async).EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public async Task Should_cache_the_explanation_spec_adapter()
    {
        // Arrange
        var spec = Spec.BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue(1).WhenFalse(0).Create("has value");

        // Act
        var first = spec.ToAsyncExplanationSpec();
        var second = spec.ToAsyncExplanationSpec();

        // Assert
        first.ShouldBeSameAs(second);
        (await first.EvaluateAsync(true)).Satisfied.ShouldBeTrue();
    }
}
