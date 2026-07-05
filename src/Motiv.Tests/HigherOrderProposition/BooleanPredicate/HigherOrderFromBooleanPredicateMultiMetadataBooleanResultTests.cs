using Motiv.HigherOrderProposition.BooleanPredicate;
using Motiv.Shared;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromBooleanPredicateMultiMetadataBooleanResultTests
{
    [Theory]
    [InlineData(true, 7)]
    [InlineData(false, 9)]
    public void ExposesResultSurface(bool satisfied, int expectedMetadata)
    {
        var result = new HigherOrderFromBooleanPredicateMultiMetadataBooleanResult<int, int>(
            satisfied,
            [Model(satisfied)],
            _ => [7],
            _ => [9],
            Description,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.MetadataTier.Metadata.ShouldBe([expectedMetadata]);
        result.Explanation.Assertions.ShouldBe([ExpectedReason(satisfied)]);
        result.Underlying.ShouldBeEmpty();
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.Causes.ShouldBeEmpty();
        result.CausesWithValues.ShouldBeEmpty();
        result.Description.ShouldBeOfType<BooleanResultDescription>();
        result.Description.Reason.ShouldBe(ExpectedReason(satisfied));
    }
}
