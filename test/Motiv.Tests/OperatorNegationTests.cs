using Motiv.Not;

namespace Motiv.Tests;

/// <summary>
/// Pins <see cref="OperatorNegation"/> directly: each operator heading maps to its negation and back, and
/// nothing but an exact, case-sensitive heading maps at all. The descriptions that call it fall back to
/// their own negation when it returns <see langword="null"/>.
/// </summary>
public class OperatorNegationTests
{
    [Theory]
    [InlineData("AND", "NAND")]
    [InlineData("AND ALSO", "NAND ALSO")]
    [InlineData("OR", "NOR")]
    [InlineData("OR ELSE", "NOR ELSE")]
    [InlineData("XOR", "XNOR")]
    [InlineData("NAND", "AND")]
    [InlineData("NAND ALSO", "AND ALSO")]
    [InlineData("NOR", "OR")]
    [InlineData("NOR ELSE", "OR ELSE")]
    [InlineData("XNOR", "XOR")]
    public void Should_map_each_operator_heading_to_its_negation(string operation, string expected)
    {
        var negated = OperatorNegation.Negate(operation);

        negated.ShouldNotBeNull();
        negated!.ShouldBe(expected);
    }

    [Theory]
    [InlineData("and")]
    [InlineData("Nand")]
    [InlineData("NOT")]
    [InlineData("AND ")]
    [InlineData("")]
    [InlineData("a == true")]
    public void Should_not_map_anything_but_an_exact_operator_heading(string operation)
    {
        OperatorNegation.Negate(operation).ShouldBeNull();
    }
}
