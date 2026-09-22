using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromBooleanResultExplanationPolicyResultTests
{
    [Theory]
    [InlineData(true, "ho true")]
    [InlineData(false, "ho false")]
    public void ExposesResultSurface(bool satisfied, string expected)
    {
        var underlying = StringBoolResult(satisfied);
        var result = new HigherOrderFromBooleanResultExplanationPolicyResult<int, string>(
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Value_IsResolvedOnceAndCached(bool satisfied)
    {
        var callCount = 0;
        var result = new HigherOrderFromBooleanResultExplanationPolicyResult<int, string>(
            satisfied,
            [StringBoolResult(satisfied)],
            _ => { callCount++; return "ho true"; },
            _ => { callCount++; return "ho false"; },
            Description,
            (_, results) => results);

        _ = result.Value;
        _ = result.Value;

        callCount.ShouldBe(1);
    }
}
