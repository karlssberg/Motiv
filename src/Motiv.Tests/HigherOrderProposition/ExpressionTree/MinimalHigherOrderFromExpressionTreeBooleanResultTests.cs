using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.ExpressionTree;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class MinimalHigherOrderFromExpressionTreeBooleanResultTests
{
    [Theory]
    [InlineData(true, "underlying true")]
    [InlineData(false, "underlying false")]
    public void ExposesResultSurface(bool satisfied, string expected)
    {
        var underlying = StringBoolResult(satisfied);
        var result = new MinimalHigherOrderFromExpressionTreeBooleanResult<int>(
            satisfied,
            [underlying],
            Description,
            Expression,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.MetadataTier.Metadata.ShouldBe([expected]);
        result.Explanation.Assertions.ShouldBe([expected]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Description.ShouldBeOfType<HigherOrderExpressionTreeResultDescription<string>>();
        result.Description.Reason.ShouldBe(ExpectedReason(satisfied));
    }
}
