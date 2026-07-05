using Motiv.DecoratorProposition;
using Motiv.Shared;

namespace Motiv.Tests.DecoratorProposition;

public class PolicyDecoratorMultiAssertionExplanationBooleanResultTests
{
    private static PolicyResultBase<string> StringUnderlying(bool satisfied) =>
        Spec.Build((bool m) => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create()
            .Evaluate(satisfied);

    private static PolicyResultBase<int> IntUnderlying(bool satisfied) =>
        Spec.Build((bool m) => m)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("underlying integer")
            .Evaluate(satisfied);

    private static PolicyDecoratorMultiAssertionExplanationBooleanResult<bool, string> CompatibleDecorator(
        PolicyResultBase<string> underlying) =>
        new(underlying, true, (_, _) => new[] { "assertion a", "assertion b" }, new SpecDescription("decorated"));

    private static PolicyDecoratorMultiAssertionExplanationBooleanResult<bool, int> IncompatibleDecorator(
        PolicyResultBase<int> underlying) =>
        new(underlying, true, (_, _) => new[] { "assertion a", "assertion b" }, new SpecDescription("decorated"));

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldMirrorUnderlyingResult(bool satisfied)
    {
        var result = CompatibleDecorator(StringUnderlying(satisfied));

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void MetadataTier_ShouldExposeResolvedAssertions(bool satisfied)
    {
        var result = CompatibleDecorator(StringUnderlying(satisfied));

        result.MetadataTier.Metadata.ShouldBe(["assertion a", "assertion b"]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Underlying_ShouldContainTheUnderlyingResult(bool satisfied)
    {
        var underlying = StringUnderlying(satisfied);
        var result = CompatibleDecorator(underlying);

        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Causes_ShouldContainTheUnderlyingResult(bool satisfied)
    {
        var underlying = StringUnderlying(satisfied);
        var result = CompatibleDecorator(underlying);

        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldContainTheUnderlyingResult_WhenMetadataCompatible(bool satisfied)
    {
        var underlying = StringUnderlying(satisfied);
        var result = CompatibleDecorator(underlying);

        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldContainTheUnderlyingResult_WhenMetadataCompatible(bool satisfied)
    {
        var underlying = StringUnderlying(satisfied);
        var result = CompatibleDecorator(underlying);

        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldBeEmpty_WhenMetadataIncompatible(bool satisfied)
    {
        var result = IncompatibleDecorator(IntUnderlying(satisfied));

        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldBeEmpty_WhenMetadataIncompatible(bool satisfied)
    {
        var result = IncompatibleDecorator(IntUnderlying(satisfied));

        result.CausesWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Explanation_ShouldSurfaceResolvedAssertions(bool satisfied)
    {
        var result = CompatibleDecorator(StringUnderlying(satisfied));

        result.Explanation.Assertions.ShouldBe(["assertion a", "assertion b"]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Explanation_ShouldFallBackToStatementReason_WhenNoAssertionsResolved(bool satisfied)
    {
        var underlying = StringUnderlying(satisfied);
        var result = new PolicyDecoratorMultiAssertionExplanationBooleanResult<bool, string>(
            underlying, true, (_, _) => [], new SpecDescription("decorated"));

        result.Explanation.Assertions.ShouldBe([$"decorated == {(satisfied ? "true" : "false")}"]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Description_ShouldReportTheDecoratingStatementAsReason(bool satisfied)
    {
        var result = CompatibleDecorator(StringUnderlying(satisfied));

        result.Description.Reason.ShouldBe($"decorated == {(satisfied ? "true" : "false")}");
    }
}
