namespace Motiv.Serialization.Tests.Rules;

public class RuleUpdateResultTests
{
    [Fact]
    public void Should_expose_outcome_specific_data()
    {
        // Arrange & Act
        var updated = RuleUpdateResult.Updated(3);
        var conflict = RuleUpdateResult.VersionConflict(5);
        var invalid = RuleUpdateResult.Invalid([new RuleError("$.rule", RuleErrorCode.UnknownSpec, "x")]);
        var notFound = RuleUpdateResult.NotFound();

        // Assert
        updated.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        updated.Version.ShouldBe(3);
        conflict.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        conflict.Version.ShouldBe(5);
        invalid.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        invalid.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
        notFound.Outcome.ShouldBe(RuleUpdateOutcome.NotFound);
    }
}
