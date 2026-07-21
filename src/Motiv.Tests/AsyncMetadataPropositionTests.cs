namespace Motiv.Tests;

public class AsyncMetadataPropositionTests
{
    public record Message(string Text)
    {
        public string Text { get; } = Text;
    }

    [Theory]
    [InlineAutoData(true, "granted")]
    [InlineAutoData(false, "denied")]
    public async Task Should_yield_metadata_from_an_async_metadata_proposition(bool model, string expectedText)
    {
        // Arrange
        var spec = Spec
            .BuildAsync((bool b) => new ValueTask<bool>(b))
            .WhenTrue(new Message("granted"))
            .WhenFalse(new Message("denied"))
            .Create("is granted");

        // Act
        var result = await spec.EvaluateAsync(model);

        // Assert
        result.Values.ShouldBe([new Message(expectedText)]);
        result.Assertions.ShouldBe([model ? "is granted == true" : "is granted == false"]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_yield_multiple_assertions_from_when_true_yield(bool model)
    {
        // Arrange
        var sync = Spec.Build((bool b) => b)
            .WhenTrueYield(_ => ["a1", "a2"])
            .WhenFalse("f")
            .Create("multi");
        var async = Spec.BuildAsync((bool b) => new ValueTask<bool>(b))
            .WhenTrueYield(_ => ["a1", "a2"])
            .WhenFalse("f")
            .Create("multi");

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Values.ShouldBe(syncResult.Values);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_yield_multiple_metadata_with_parity(bool model)
    {
        // Arrange
        var sync = Spec.Build((bool b) => b)
            .WhenTrueYield(_ => new[] {1, 2})
            .WhenFalseYield(_ => new[] {3})
            .Create("numbers");
        var async = Spec.BuildAsync((bool b) => new ValueTask<bool>(b))
            .WhenTrueYield(_ => new[] {1, 2})
            .WhenFalseYield(_ => new[] {3})
            .Create("numbers");

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Values.ShouldBe(syncResult.Values);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_support_unnamed_multi_assertion_explanations(bool model)
    {
        // Arrange
        var sync = Spec.Build((bool b) => b)
            .WhenTrue("all good")
            .WhenFalseYield(_ => ["bad one", "bad two"])
            .Create();
        var async = Spec.BuildAsync((bool b) => new ValueTask<bool>(b))
            .WhenTrue("all good")
            .WhenFalseYield(_ => ["bad one", "bad two"])
            .Create();

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
    }
}
