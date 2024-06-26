namespace Motiv.Tests;

public class MinimalPropositionTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Should_evaluate_a_minimal_proposition(bool model, bool expectedSatisfied)
    {
        // Arrange
        var spec =
            Spec.Build((bool b) => b)
                .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expectedSatisfied);
    }

    [Theory]
    [InlineData(true, "is true")]
    [InlineData(false, "¬is true")]
    public void Should_evaluate_reason_of_a_minimal_proposition(bool model, string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool b) => b)
                .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "is true", "is true")]
    [InlineData(false, "is true", "¬is true")]
    [InlineData(true, "is true (with brackets)", "(is true (with brackets))")]
    [InlineData(false, "is true (with brackets)", "¬(is true (with brackets))")]
    [InlineData(true, "is true!", "(is true!)")]
    [InlineData(false, "is true!", "¬(is true!)")]
    public void Should_escape_propositional_statement_when_evaluated(bool model, string propositionalStatement, string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool b) => b)
                .Create(propositionalStatement);

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }
}
