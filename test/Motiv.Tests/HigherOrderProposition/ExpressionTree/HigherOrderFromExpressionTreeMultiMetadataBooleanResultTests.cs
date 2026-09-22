using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.ExpressionTree;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromExpressionTreeMultiMetadataBooleanResultTests
{
    [Theory]
    [InlineData(true, 7, "underlying true")]
    [InlineData(false, 9, "underlying false")]
    public void ExposesResultSurface(bool satisfied, int expectedMetadata, string expectedAssertion)
    {
        var underlying = StringBoolResult(satisfied);
        var result = new HigherOrderFromExpressionTreeMultiMetadataBooleanResult<int, int>(
            satisfied,
            [underlying],
            _ => [7],
            _ => [9],
            Description,
            Expression,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.MetadataTier.Metadata.ShouldBe([expectedMetadata]);
        result.Explanation.Assertions.ShouldBe([expectedAssertion]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldBeEmpty();
        result.Description.ShouldBeOfType<HigherOrderExpressionTreeResultDescription<string>>();
        result.Description.Reason.ShouldBe(ExpectedReason(satisfied));
    }
}
