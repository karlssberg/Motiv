using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.ExpressionTree;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromExpressionTreeMultiAssertionExplanationBooleanResultTests
{
    private static HigherOrderFromExpressionTreeMultiAssertionExplanationBooleanResult<int> Create(bool satisfied)
    {
        var underlying = StringBoolResult(satisfied);
        return new HigherOrderFromExpressionTreeMultiAssertionExplanationBooleanResult<int>(
            satisfied,
            [underlying],
            _ => ["expr true"],
            _ => ["expr false"],
            Description,
            Expression,
            (_, results) => results);
    }

    [Theory]
    [InlineData(true, "expr true")]
    [InlineData(false, "expr false")]
    public void ExposesResultSurface(bool satisfied, string expectedAssertion)
    {
        var underlying = StringBoolResult(satisfied);
        var result = new HigherOrderFromExpressionTreeMultiAssertionExplanationBooleanResult<int>(
            satisfied,
            [underlying],
            _ => ["expr true"],
            _ => ["expr false"],
            Description,
            Expression,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.MetadataTier.Metadata.ShouldBe([expectedAssertion]);
        result.Explanation.Assertions.ShouldBe([expectedAssertion]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Description.ShouldBeOfType<HigherOrderExpressionTreeResultDescription<string>>();
        result.Description.Reason.ShouldBe(ExpectedReason(satisfied));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Description_IsCachedAcrossReads(bool satisfied)
    {
        var result = Create(satisfied);

        result.Description.ShouldBeSameAs(result.Description);
    }
}
