using Motiv.HigherOrderProposition.BooleanPredicate;
using Motiv.Shared;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

// Retargeted from the deleted HigherOrderFromBooleanPredicatePolicyResult<TMetadata> tests.
public class HigherOrderFromBooleanPredicateMetadataPolicyResultTests
{
    [Theory]
    [InlineData(true, 7)]
    [InlineData(false, 9)]
    public void ExposesResultSurface(bool satisfied, int expectedValue)
    {
        var result = new HigherOrderFromBooleanPredicateMetadataPolicyResult<int, int>(
            satisfied,
            [Model(satisfied)],
            _ => 7,
            _ => 9,
            Description,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.Value.ShouldBe(expectedValue);
        result.MetadataTier.Metadata.ShouldBe([expectedValue]);
        result.Explanation.Assertions.ShouldBe([ExpectedReason(satisfied)]);
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
    public void Value_IsResolvedOnceAndCached(bool satisfied)
    {
        var callCount = 0;
        var result = new HigherOrderFromBooleanPredicateMetadataPolicyResult<int, int>(
            satisfied,
            [Model(satisfied)],
            _ => { callCount++; return 7; },
            _ => { callCount++; return 9; },
            Description,
            (_, results) => results);

        _ = result.Value;
        _ = result.Value;

        callCount.ShouldBe(1);
    }
}
