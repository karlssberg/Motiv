using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromBooleanResultMultiMetadataBooleanResultTests
{
    [Theory]
    [InlineData(true, 7)]
    [InlineData(false, 9)]
    public void ExposesResultSurface(bool satisfied, int expectedMetadata)
    {
        var underlying = StringBoolResult(satisfied);
        var result = new HigherOrderFromBooleanResultMultiMetadataBooleanResult<int, int, string>(
            satisfied,
            [underlying],
            _ => [7],
            _ => [9],
            Description,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.MetadataTier.Metadata.ShouldBe([expectedMetadata]);
        result.Explanation.Assertions.ShouldBe([ExpectedReason(satisfied)]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldBeEmpty();
        result.Description.ShouldBeOfType<HigherOrderResultDescription<string>>();
        result.Description.Reason.ShouldBe(ExpectedReason(satisfied));
    }
}
