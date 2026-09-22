using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class MinimalHigherOrderFromPolicyResultBooleanResultTests
{
    [Theory]
    [InlineData(true, "underlying true")]
    [InlineData(false, "underlying false")]
    public void StringMetadata_SurfacesUnderlyingValuesDirectly(bool satisfied, string expected)
    {
        var underlying = StringPolicyResult(satisfied);
        var result = new MinimalHigherOrderFromPolicyResultBooleanResult<int, string>(
            satisfied,
            [underlying],
            Description,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.MetadataTier.Metadata.ShouldBe([expected]);
        result.Explanation.Assertions.ShouldBe([expected]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Description.ShouldBeOfType<HigherOrderResultDescription<string>>();
        result.Description.Reason.ShouldBe(ExpectedReason(satisfied));
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void NonStringMetadata_FallsBackToStatementAssertions(bool satisfied, int expectedValue)
    {
        var underlying = IntPolicyResult(satisfied);
        var result = new MinimalHigherOrderFromPolicyResultBooleanResult<int, int>(
            satisfied,
            [underlying],
            Description,
            (_, results) => results);

        result.MetadataTier.Metadata.ShouldBe([expectedValue]);
        result.Explanation.Assertions.ShouldBe([ExpectedReason(satisfied)]);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }
}
