using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromPolicyResultMetadataPolicyResultTests
{
    [Theory]
    [InlineData(true, 7)]
    [InlineData(false, 9)]
    public void ExposesResultSurface(bool satisfied, int expectedValue)
    {
        var underlying = StringPolicyResult(satisfied);
        var result = new HigherOrderFromPolicyResultMetadataPolicyResult<int, int, string>(
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
