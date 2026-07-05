namespace Motiv.Tests;

/// <summary>
/// Covers the fix for the double equality-assertion suffix bug, where a clause statement that
/// already ends in " == true"/" == false" (produced by ExpressionTreeTransformer.CreateSpecForBoolean
/// for non-comparison boolean sub-expressions) had a second suffix blindly appended by
/// AsSatisfied()/AsUnsatisfied(), yielding jarring text such as "tryParseInt(txt) == true == false".
/// </summary>
public class EqualityAssertionSuffixTests
{
    [Theory]
    [InlineData("x", "x == true")]
    [InlineData("x == true", "x == true")]
    [InlineData("x == false", "x == false")]
    public void Should_satisfy_text_without_double_suffixing(string text, string expected)
    {
        var act = text.AsSatisfied();

        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData("x", "x == false")]
    [InlineData("x == true", "x == false")]
    [InlineData("x == false", "x == true")]
    public void Should_unsatisfy_text_by_negating_a_trailing_equality_assertion(string text, string expected)
    {
        var act = text.AsUnsatisfied();

        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", "txt.Length == 0", "txt == \"\"")]
    [InlineData("a", "txt.Length != 0", "tryParseInt(txt) == false")]
    [InlineData("123", "txt.Length != 0", "tryParseInt(txt) == true")]
    public void Should_not_double_suffix_a_decomposed_boolean_clause_in_a_conditional_expression(
        string model, params string[] expectedAssertion)
    {
        // Assemble
        var tryParseInt = (string s) => int.TryParse(s, out _);
        var sut = Spec
            .From((string txt) => txt.Length == 0 ? txt == "" : tryParseInt(txt))
            .Create("assert-conditional");

        // Act
        var act = sut.Evaluate(model);

        // Assert
        act.Assertions.ShouldBe(expectedAssertion, true);
    }

    [Theory]
    [InlineData("hello world", "hello", "text.Contains(query) == true")]
    [InlineData("high-roller", "world", "text.Contains(query) == false")]
    public void Should_not_double_suffix_a_decomposed_boolean_clause_in_a_higher_order_proposition(
        string text, string model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((string query) => text.Contains(query))
            .AsAnySatisfied()
            .WhenTrueYield(eval => eval.CausalModels
                .Select(query => $"contains '{query}'"))
            .WhenFalseYield(eval => eval.CausalModels
                .Select(query => $"does not contain '{query}'"))
            .Create($"text-equals");

        // Act
        var act = sut.Evaluate([model]);

        // Assert
        act.Assertions.ShouldBe([expectedAssertion]);
    }
}
