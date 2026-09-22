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

        var result = spec.Evaluate(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expectedSatisfied);
    }

    [Theory]
    [InlineData(true, "is true == true")]
    [InlineData(false, "is true == false")]
    public void Should_evaluate_reason_of_a_minimal_proposition(bool model, string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool b) => b)
                .Create("is true");

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "is true", "is true == true")]
    [InlineData(false, "is true", "is true == false")]
    [InlineData(true, "is true (with brackets)", "(is true (with brackets)) == true")]
    [InlineData(false, "is true (with brackets)", "(is true (with brackets)) == false")]
    [InlineData(true, "is true!", "(is true!) == true")]
    [InlineData(false, "is true!", "(is true!) == false")]
    public void Should_escape_propositional_statement_when_evaluated(bool model, string propositionalStatement, string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool b) => b)
                .Create(propositionalStatement);

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }
}
