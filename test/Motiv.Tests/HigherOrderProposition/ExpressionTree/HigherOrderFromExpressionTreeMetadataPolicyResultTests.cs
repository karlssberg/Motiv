using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.ExpressionTree;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromExpressionTreeMetadataPolicyResultTests
{
    [Theory]
    [InlineData(true, 7, "underlying true")]
    [InlineData(false, 9, "underlying false")]
    public void ExposesResultSurface(bool satisfied, int expectedValue, string expectedAssertion)
    {
        var underlying = StringBoolResult(satisfied);
        var result = new HigherOrderFromExpressionTreeMetadataPolicyResult<int, int>(
            satisfied,
            [underlying],
            _ => 7,
            _ => 9,
            Description,
            Expression,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.Value.ShouldBe(expectedValue);
        result.MetadataTier.Metadata.ShouldBe([expectedValue]);
        result.Explanation.Assertions.ShouldBe([expectedAssertion]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldBeEmpty();
        result.Description.ShouldBeOfType<HigherOrderExpressionTreeResultDescription<string>>();
        result.Description.Reason.ShouldBe(ExpectedReason(satisfied));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Value_IsCachedAcrossReads(bool satisfied)
    {
        var callCount = 0;
        var result = new HigherOrderFromExpressionTreeMetadataPolicyResult<int, int>(
            satisfied,
            [StringBoolResult(satisfied)],
            _ => { callCount++; return 7; },
            _ => { callCount++; return 9; },
            Description,
            Expression,
            (_, results) => results);

        _ = result.Value;
        _ = result.Value;

        callCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(true, "true reasons")]
    [InlineData(false, "false reasons")]
    public void Explanation_SurfacesMetadataDirectly_WhenMetadataIsStringCollection(bool satisfied, string expected)
    {
        var result = new HigherOrderFromExpressionTreeMetadataPolicyResult<int, IEnumerable<string>>(
            satisfied,
            [StringBoolResult(satisfied)],
            _ => ["true reasons"],
            _ => ["false reasons"],
            Description,
            Expression,
            (_, results) => results);

        result.Explanation.Assertions.ShouldBe([expected]);
    }
}
