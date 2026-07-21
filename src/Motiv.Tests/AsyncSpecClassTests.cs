namespace Motiv.Tests;

public class AsyncSpecClassTests
{
    private sealed class IsPositiveAsync() : AsyncSpec<int>(
        Spec.BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create());

    private sealed class GradeAsync() : AsyncSpec<int, char>(() =>
        Spec.BuildAsync((int n) => new ValueTask<bool>(n >= 50))
            .WhenTrue('P')
            .WhenFalse('F')
            .Create("passing grade"));

    [Theory]
    [InlineAutoData(1, true, "is positive")]
    [InlineAutoData(-1, false, "is not positive")]
    public async Task Should_support_class_based_async_specs(int model, bool expected, string assertion)
    {
        // Arrange
        var spec = new IsPositiveAsync();

        // Act
        var result = await spec.EvaluateAsync(model);

        // Assert
        result.Satisfied.ShouldBe(expected);
        result.Assertions.ShouldBe([assertion]);
    }

    [Theory]
    [InlineAutoData(80, 'P')]
    [InlineAutoData(20, 'F')]
    public async Task Should_support_factory_constructed_metadata_async_specs(int model, char expected)
    {
        // Arrange
        var spec = new GradeAsync();

        // Act
        var result = await spec.EvaluateAsync(model);

        // Assert
        result.Values.ShouldBe([expected]);
    }

    [Fact]
    public async Task Should_compose_class_based_async_specs()
    {
        // Arrange
        var spec = new IsPositiveAsync() & new IsPositiveAsync().Not();

        // Act & Assert
        (await spec.EvaluateAsync(1)).Satisfied.ShouldBeFalse();
    }
}
