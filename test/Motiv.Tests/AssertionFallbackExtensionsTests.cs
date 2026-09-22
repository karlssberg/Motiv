using Motiv.Shared;

namespace Motiv.Tests;

public class AssertionFallbackExtensionsTests
{
    [Theory]
    [InlineAutoData("meaningful", "meaningful")]
    [InlineAutoData("", "fallback")]
    [InlineAutoData("   ", "fallback")]
    [InlineAutoData(null, "fallback")]
    public void Should_use_single_assertion_unless_degenerate(string? assertion, string expected)
    {
        // Act
        var act = assertion.ElseFallback(() => "fallback");

        // Assert
        act.ShouldBe(expected);
    }

    [Fact]
    public void Should_filter_degenerate_entries_from_multiple_assertions()
    {
        // Arrange
        IEnumerable<string> assertions = ["good", "", "   ", "also good"];

        // Act
        var act = assertions.ElseFallback(() => "fallback");

        // Assert
        act.ShouldBe(["good", "also good"]);
    }

    [Fact]
    public void Should_fall_back_when_all_multiple_assertions_are_degenerate()
    {
        // Arrange
        IEnumerable<string> assertions = ["", "   "];

        // Act
        var act = assertions.ElseFallback(() => "fallback");

        // Assert
        act.ShouldBe(["fallback"]);
    }

    [Fact]
    public void Should_fall_back_to_collection_when_all_multiple_assertions_are_degenerate()
    {
        // Arrange
        IEnumerable<string> assertions = [" "];

        // Act
        var act = assertions.ElseFallback(() => (IEnumerable<string>)["a", "b"]);

        // Assert
        act.ShouldBe(["a", "b"]);
    }
}
