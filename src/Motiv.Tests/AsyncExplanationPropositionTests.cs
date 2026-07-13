namespace Motiv.Tests;

public class AsyncExplanationPropositionTests
{
    [Theory]
    [InlineAutoData(true, "user is active")]
    [InlineAutoData(false, "user is not active")]
    public async Task Should_use_because_strings_as_assertions_when_unnamed(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue("user is active")
            .WhenFalse("user is not active")
            .Create();

        // Act
        var act = (await spec.EvaluateAsync(model)).Assertions;

        // Assert
        act.ShouldBe([expected]);
    }

    [Theory]
    [InlineAutoData(true, "is active == true", "user is active")]
    [InlineAutoData(false, "is active == false", "user is not active")]
    public async Task Should_demote_because_strings_to_values_when_named(
        bool model, string expectedAssertion, string expectedValue)
    {
        // Arrange
        var spec = Spec
            .BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue("user is active")
            .WhenFalse("user is not active")
            .Create("is active");

        // Act
        var result = await spec.EvaluateAsync(model);

        // Assert
        result.Assertions.ShouldBe([expectedAssertion]);
        result.Values.ShouldBe([expectedValue]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_have_parity_with_the_sync_explanation_proposition(bool model)
    {
        // Arrange
        var sync = Spec.Build((bool b) => b)
            .WhenTrue("yes").WhenFalse("no").Create();
        var async = Spec.BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue("yes").WhenFalse("no").Create();

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public void Should_guard_the_unnamed_create_against_whitespace_when_true()
    {
        // Act
        var act = () => Spec
            .BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue("  ")
            .WhenFalse("no")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }
}
