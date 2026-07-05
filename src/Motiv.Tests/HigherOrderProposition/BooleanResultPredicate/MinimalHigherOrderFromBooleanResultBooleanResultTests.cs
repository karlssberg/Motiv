using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

// Retargeted from the deleted HigherOrderBooleanResult<TMetadata, TUnderlyingMetadata> tests.
public class MinimalHigherOrderFromBooleanResultBooleanResultTests
{
    [Theory]
    [InlineData(true, "underlying true")]
    [InlineData(false, "underlying false")]
    public void StringMetadata_SurfacesUnderlyingAssertionsDirectly(bool satisfied, string expected)
    {
        var underlying = StringBoolResult(satisfied);
        var result = new MinimalHigherOrderFromBooleanResultBooleanResult<int, string>(
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
        var underlying = IntBoolResult(satisfied);
        var result = new MinimalHigherOrderFromBooleanResultBooleanResult<int, int>(
            satisfied,
            [underlying],
            Description,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.MetadataTier.Metadata.ShouldBe([expectedValue]);
        result.Explanation.Assertions.ShouldBe([ExpectedReason(satisfied)]);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Causes_AreComputedOnceAndCached(bool satisfied)
    {
        var callCount = 0;
        var result = new MinimalHigherOrderFromBooleanResultBooleanResult<int, string>(
            satisfied,
            [StringBoolResult(satisfied)],
            Description,
            (_, results) => { callCount++; return results; });

        _ = result.Causes;
        _ = result.CausesWithValues;
        _ = result.MetadataTier;

        callCount.ShouldBe(1);
    }
}
