using FluentAssertions;

namespace Motiv.Tests;

public class IndentStringExtensionsTests
{
    [Fact]
    public void IndentLine_WithSingleIndentation_IndentsCorrectly()
    {
        var result = "Hello".IndentLine();
        result.Should().Be("    Hello");
    }

    [Fact]
    public void IndentLine_WithMultipleIndentations_IndentsCorrectly()
    {
        var result = "Hello".IndentLine(3);
        result.Should().Be("            Hello");
    }

    [Fact]
    public void IndentLines_IndentsAllLinesCorrectly()
    {
        string[] lines = ["Hello", "World"];
        var result = lines.IndentLines();
        result.Should().BeEquivalentTo( "    Hello", "    World");
    }

    [Fact]
    public void IndentFromLine_IndentsFromSpecifiedLine()
    {
        List<string> lines = ["Hello", "World", "!"];
        var result = lines.IndentFromLine(1);
        result.Should().BeEquivalentTo("Hello", "    World", "    !");
    }

    [Fact]
    public void IndentFromLine_WithStartLineGreaterThanLinesCount_DoesNotIndent()
    {
        List<string> lines = ["Hello", "World"];
        var result = lines.IndentFromLine(3);
        
        result.Should().BeEquivalentTo("Hello", "World" );
    }

    [Fact]
    public void IndentFromLine_WithStartLineZero_IndentsAllLines()
    {
        List<string> lines = ["Hello", "World"];
        var result = lines.IndentFromLine(0);
        result.Should().BeEquivalentTo("    Hello", "    World");
    }
}