using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromPolicyResultMultiAssertionExplanationBooleanResultTests
{
    [Theory]
    [InlineData(true, "ho true")]
    [InlineData(false, "ho false")]
    public void ExposesResultSurface(bool satisfied, string expected)
    {
        var underlying = StringPolicyResult(satisfied);
        var result = new HigherOrderFromPolicyResultMultiAssertionExplanationBooleanResult<int, string>(
            satisfied,
            [underlying],
            _ => ["ho true"],
            _ => ["ho false"],
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
    [InlineData(true)]
    [InlineData(false)]
    public void Explanation_FallsBackToStatementReason_WhenAssertionsAreDegenerate(bool satisfied)
    {
        var result = new HigherOrderFromPolicyResultMultiAssertionExplanationBooleanResult<int, string>(
            satisfied,
            [StringPolicyResult(satisfied)],
            _ => [" "],
            _ => [" "],
            Description,
            (_, results) => results);

        result.Explanation.Assertions.ShouldBe([ExpectedReason(satisfied)]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Explanation_FallsBackToStatementReason_WhenResolverReturnsNull(bool satisfied)
    {
        var result = new HigherOrderFromPolicyResultMultiAssertionExplanationBooleanResult<int, string>(
            satisfied,
            [StringPolicyResult(satisfied)],
            _ => null!,
            _ => null!,
            Description,
            (_, results) => results);

        result.Explanation.Assertions.ShouldBe([ExpectedReason(satisfied)]);
    }
}
