namespace Motiv.Tests;

/// <summary>
/// Pins the human-friendly negated/plain serialization of decomposed expression-tree clauses in
/// justification leaves and assertions. Transformer-generated internal clause specs must keep their
/// because-strings (e.g. "n &lt;= 0") as explanation text rather than adopting the named
/// "statement == true/false" form that the v8 named-demotion rule applies to user-authored specs.
/// </summary>
public class ExpressionTreeNegatedClauseSerializationTests
{
    [Theory]
    [InlineData(-1,
        """
        is-positive == false
            (int n) => n > 0 == false
                n <= 0
        """)]
    [InlineData(1,
        """
        is-positive == true
            (int n) => n > 0 == true
                n > 0
        """)]
    public void Should_serialize_negated_greater_than_clause_in_justification_leaf(int model, string expected)
    {
        // Assemble
        var sut =
            Spec.From((int n) => n > 0)
                .Create("is-positive");

        // Act
        var act = sut.Evaluate(model);

        // Assert
        act.Justification.ShouldBe(expected);
    }

    [Theory]
    [InlineData(3,
        """
        is-five == false
            (int n) => n == 5 == false
                n != 5
        """)]
    [InlineData(5,
        """
        is-five == true
            (int n) => n == 5 == true
                n == 5
        """)]
    public void Should_serialize_negated_equality_clause_in_justification_leaf(int model, string expected)
    {
        // Assemble
        var sut =
            Spec.From((int n) => n == 5)
                .Create("is-five");

        // Act
        var act = sut.Evaluate(model);

        // Assert
        act.Justification.ShouldBe(expected);
    }

    [Theory]
    [InlineData(11,
        """
        within == false
            (int n) => n <= 10 == false
                n > 10
        """)]
    [InlineData(5,
        """
        within == true
            (int n) => n <= 10 == true
                n <= 10
        """)]
    public void Should_serialize_negated_less_than_or_equal_clause_in_justification_leaf(int model, string expected)
    {
        // Assemble
        var sut =
            Spec.From((int n) => n <= 10)
                .Create("within");

        // Act
        var act = sut.Evaluate(model);

        // Assert
        act.Justification.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", "txt.Length == 0", "txt == \"\"")]
    [InlineData("a", "txt.Length != 0", "tryParseInt(txt) == false")]
    public void Should_serialize_conditional_expression_clauses_without_double_suffix(
        string model,
        params string[] expectedAssertion)
    {
        // Assemble
        var tryParseInt = (string s) => int.TryParse(s, out _);
        var sut =
            Spec.From((string txt) => txt.Length == 0 ? txt == "" : tryParseInt(txt))
                .Create("assert-conditional");

        // Act
        var act = sut.Evaluate(model);

        // Assert
        act.Assertions.ShouldBe(expectedAssertion, true);
    }
}
