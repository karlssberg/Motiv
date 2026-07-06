using Motiv.HigherOrderProposition.BooleanPredicate;
using Motiv.Shared;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromBooleanPredicateMultiAssertionExplanationBooleanResultTests
{
    [Theory]
    [InlineData(true, "bp true")]
    [InlineData(false, "bp false")]
    public void ExposesResultSurface(bool satisfied, string expected)
    {
        var result = new HigherOrderFromBooleanPredicateMultiAssertionExplanationBooleanResult<int>(
            satisfied,
            [Model(satisfied)],
            _ => ["bp true"],
            _ => ["bp false"],
            Description,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.MetadataTier.Metadata.ShouldBe([expected]);
        result.Explanation.Assertions.ShouldBe([expected]);
        result.Underlying.ShouldBeEmpty();
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.Causes.ShouldBeEmpty();
        result.CausesWithValues.ShouldBeEmpty();
        result.Description.ShouldBeOfType<BooleanResultDescription>();
        result.Description.Reason.ShouldBe(ExpectedReason(satisfied));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Explanation_FallsBackToStatementReason_WhenResolverReturnsNull(bool satisfied)
    {
        var result = new HigherOrderFromBooleanPredicateMultiAssertionExplanationBooleanResult<int>(
            satisfied,
            [Model(satisfied)],
            _ => null!,
            _ => null!,
            Description,
            (_, results) => results);

        result.Explanation.Assertions.ShouldBe([ExpectedReason(satisfied)]);
    }
}
