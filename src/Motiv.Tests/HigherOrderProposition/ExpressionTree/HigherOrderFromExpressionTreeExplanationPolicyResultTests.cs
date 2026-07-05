using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.ExpressionTree;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromExpressionTreeExplanationPolicyResultTests
{
    [Theory]
    [InlineData(true, "expr true")]
    [InlineData(false, "expr false")]
    public void ExposesResultSurface(bool satisfied, string expected)
    {
        var underlying = StringBoolResult(satisfied);
        var result = new HigherOrderFromExpressionTreeExplanationPolicyResult<int>(
            satisfied,
            [underlying],
            _ => "expr true",
            _ => "expr false",
            Description,
            Expression,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.Value.ShouldBe(expected);
        result.MetadataTier.Metadata.ShouldBe([expected]);
        result.Explanation.Assertions.ShouldBe([expected]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Description.ShouldBeOfType<HigherOrderExpressionTreeResultDescription<string>>();
        result.Description.Reason.ShouldBe(expected);
    }
}
