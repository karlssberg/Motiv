using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromPolicyResultExplanationPolicyResultTests
{
    [Theory]
    [InlineData(true, "ho true")]
    [InlineData(false, "ho false")]
    public void ExposesResultSurface(bool satisfied, string expected)
    {
        var underlying = StringPolicyResult(satisfied);
        var result = new HigherOrderFromPolicyResultExplanationPolicyResult<int, string>(
            satisfied,
            [underlying],
            _ => "ho true",
            _ => "ho false",
            Description,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.Value.ShouldBe(expected);
        result.MetadataTier.Metadata.ShouldBe([expected]);
        result.Explanation.Assertions.ShouldBe([expected]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Description.ShouldBeOfType<HigherOrderResultDescription<string>>();
        result.Description.Reason.ShouldBe(expected);
    }
}
