using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

// Retargeted from the deleted HigherOrderPolicyResult<TMetadata, TUnderlyingMetadata> tests, including its
// "empty when metadata types do not match" behaviour (int result metadata vs string underlying metadata).
public class HigherOrderFromBooleanResultPolicyResultTests
{
    [Theory]
    [InlineData(true, 7)]
    [InlineData(false, 9)]
    public void ExposesResultSurface(bool satisfied, int expectedValue)
    {
        var underlying = StringBoolResult(satisfied);
        var result = new HigherOrderFromBooleanResultPolicyResult<int, int, string>(
            satisfied,
            [underlying],
            _ => 7,
            _ => 9,
            Description,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.Value.ShouldBe(expectedValue);
        result.MetadataTier.Metadata.ShouldBe([expectedValue]);
        result.Explanation.Assertions.ShouldBe([ExpectedReason(satisfied)]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldBeEmpty();
        result.Description.ShouldBeOfType<HigherOrderResultDescription<string>>();
        result.Description.Reason.ShouldBe(ExpectedReason(satisfied));
    }
}
