namespace Motiv.Tests.BooleanPredicateProposition;

public class ExplanationPropositionPolicyResultTests
{
    private static PolicyResultBase<string> Evaluate(bool satisfied) =>
        Spec.Build((bool m) => m)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create()
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
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "is false")]
    public void Value_ShouldBeTheBecauseString(bool satisfied, string expected)
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
