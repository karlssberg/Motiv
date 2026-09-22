using Motiv.DecoratorProposition;
using Motiv.Shared;

namespace Motiv.Tests.DecoratorProposition;

public class PolicyDecoratorWithSingleTrueAssertionPolicyResultTests
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

    // TUnderlyingMetadata == string: the underlying-with-values cast succeeds.
    private static PolicyDecoratorWithSingleTrueAssertionPolicyResult<bool, string> CompatibleDecorator(
        PolicyResultBase<string> underlying) =>
        new(underlying, true, "is true", (_, _) => "is false", new SpecDescription("decorated"));

    // TUnderlyingMetadata (int) != string: the underlying-with-values cast fails and falls back to [].
    private static PolicyDecoratorWithSingleTrueAssertionPolicyResult<bool, int> IncompatibleDecorator(
        PolicyResultBase<int> underlying) =>
        new(underlying, true, "is true", (_, _) => "is false", new SpecDescription("decorated"));

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldMirrorUnderlyingResult(bool satisfied)
    {
        var result = CompatibleDecorator(StringUnderlying(satisfied));

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "is false")]
    public void Value_ShouldSelectAssertionByOutcome(bool satisfied, string expected)
    {
        var result = CompatibleDecorator(StringUnderlying(satisfied));

        result.Value.ShouldBe(expected);
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
}
