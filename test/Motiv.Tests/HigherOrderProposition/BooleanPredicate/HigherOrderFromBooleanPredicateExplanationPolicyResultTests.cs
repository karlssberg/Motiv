using Motiv.HigherOrderProposition.BooleanPredicate;
using Motiv.Shared;
using static Motiv.Tests.HigherOrderProposition.HigherOrderResultFixtures;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromBooleanPredicateExplanationPolicyResultTests
{
    [Theory]
    [InlineData(true, "bp true")]
    [InlineData(false, "bp false")]
    public void ExposesResultSurface(bool satisfied, string expected)
    {
        var result = new HigherOrderFromBooleanPredicateExplanationPolicyResult<int>(
            satisfied,
            [Model(satisfied)],
            _ => "bp true",
            _ => "bp false",
            Description,
            (_, results) => results);

        result.Satisfied.ShouldBe(satisfied);
        result.Value.ShouldBe(expected);
        result.MetadataTier.Metadata.ShouldBe([expected]);
        result.Explanation.Assertions.ShouldBe([expected]);
        result.Underlying.ShouldBeEmpty();
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.Causes.ShouldBeEmpty();
        result.CausesWithValues.ShouldBeEmpty();
        result.Description.ShouldBeOfType<BooleanResultDescription>();
        result.Description.Reason.ShouldBe(expected);
    }
}
