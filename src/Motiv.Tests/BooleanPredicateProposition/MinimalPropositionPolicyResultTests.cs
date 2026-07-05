namespace Motiv.Tests.BooleanPredicateProposition;

public class MinimalPropositionPolicyResultTests
{
    private static PolicyResultBase<string> Evaluate(bool satisfied) =>
        Spec.Build((bool m) => m)
            .Create("is positive")
            .Evaluate(satisfied);

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldReflectPredicate(bool satisfied)
    {
        var result = Evaluate(satisfied);

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true, "is positive == true")]
    [InlineAutoData(false, "is positive == false")]
    public void Value_ShouldBeTheSuffixedAssertion(bool satisfied, string expected)
    {
        var result = Evaluate(satisfied);

        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Evaluate(satisfied);

        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Evaluate(satisfied);

        result.CausesWithValues.ShouldBeEmpty();
    }
}
