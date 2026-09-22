using Motiv.DecoratorProposition;
using Motiv.Shared;

namespace Motiv.Tests.DecoratorProposition;

public class MinimalPolicyDecoratorPolicyResultTests
{
    private static PolicyResultBase<string> CreateUnderlying(bool satisfied) =>
        Spec.Build((bool m) => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create()
            .Evaluate(satisfied);

    private static MinimalPolicyDecoratorPolicyResult<string> CreateDecorator(bool satisfied) =>
        new(CreateUnderlying(satisfied), new SpecDescription("decorated"));

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldMirrorUnderlyingResult(bool satisfied)
    {
        var result = CreateDecorator(satisfied);

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true, "underlying is true")]
    [InlineAutoData(false, "underlying is false")]
    public void Value_ShouldMirrorUnderlyingValue(bool satisfied, string expected)
    {
        var result = CreateDecorator(satisfied);

        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, "underlying is true")]
    [InlineAutoData(false, "underlying is false")]
    public void MetadataTier_ShouldExposeUnderlyingValue(bool satisfied, string expected)
    {
        var result = CreateDecorator(satisfied);

        result.MetadataTier.Metadata.ShouldBe([expected]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Underlying_ShouldContainTheUnderlyingResult(bool satisfied)
    {
        var underlying = CreateUnderlying(satisfied);
        var result = new MinimalPolicyDecoratorPolicyResult<string>(underlying, new SpecDescription("decorated"));

        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldContainTheUnderlyingResult(bool satisfied)
    {
        var underlying = CreateUnderlying(satisfied);
        var result = new MinimalPolicyDecoratorPolicyResult<string>(underlying, new SpecDescription("decorated"));

        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Causes_ShouldContainTheUnderlyingResult(bool satisfied)
    {
        var underlying = CreateUnderlying(satisfied);
        var result = new MinimalPolicyDecoratorPolicyResult<string>(underlying, new SpecDescription("decorated"));

        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldContainTheUnderlyingResult(bool satisfied)
    {
        var underlying = CreateUnderlying(satisfied);
        var result = new MinimalPolicyDecoratorPolicyResult<string>(underlying, new SpecDescription("decorated"));

        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Explanation_ShouldMirrorUnderlyingExplanation(bool satisfied)
    {
        var underlying = CreateUnderlying(satisfied);
        var result = new MinimalPolicyDecoratorPolicyResult<string>(underlying, new SpecDescription("decorated"));

        result.Explanation.ShouldBeSameAs(underlying.Explanation);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Description_ShouldReportTheDecoratingStatementAsReason(bool satisfied)
    {
        var result = CreateDecorator(satisfied);

        result.Description.Reason.ShouldBe($"decorated == {(satisfied ? "true" : "false")}");
    }
}
