namespace Motiv.Tests.BooleanPredicateProposition;

public class MultiAssertionExplanationPropositionBooleanResultTests
{
    private static BooleanResultBase<string> Evaluate(bool satisfied) =>
        Spec.Build((bool m) => m)
            .WhenTrue("is true assertion")
            .WhenFalseYield(_ => ["is false assertion"])
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
    [InlineAutoData(true, "is true assertion")]
    [InlineAutoData(false, "is false assertion")]
    public void MetadataTier_ShouldExposeResolvedAssertions(bool satisfied, string expected)
    {
        var result = Evaluate(satisfied);

        result.MetadataTier.Metadata.ShouldBe([expected]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Underlying_ShouldBeEmpty(bool satisfied)
    {
        var result = Evaluate(satisfied);

        result.Underlying.ShouldBeEmpty();
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
    public void Causes_ShouldBeEmpty(bool satisfied)
    {
        var result = Evaluate(satisfied);

        result.Causes.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Evaluate(satisfied);

        result.CausesWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true, "is true assertion")]
    [InlineAutoData(false, "is false assertion")]
    public void Explanation_ShouldSurfaceTheAssertion(bool satisfied, string expected)
    {
        var result = Evaluate(satisfied);

        result.Explanation.Assertions.ShouldBe([expected]);
    }
}
