using Motiv.Shared;
using Shouldly;

namespace Motiv.Tests;

public class IndentStringExtensionsTests
{
    [Theory]
    [AutoData]
    public void Should_indent_string(string text)
    {
        // Arrange
        const string indent = "    ";

        // Act
        var act = text.Indent();

        // Assert
        act.ShouldBe(indent + text);
    }
}
