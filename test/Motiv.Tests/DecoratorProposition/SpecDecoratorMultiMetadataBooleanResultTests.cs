using Motiv.DecoratorProposition;
using Motiv.Shared;

namespace Motiv.Tests.DecoratorProposition;

public class SpecDecoratorMultiMetadataBooleanResultTests
{
    private static BooleanResultBase<int> IntUnderlying(bool satisfied) =>
        Spec.Build((bool m) => m)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("underlying integer")
            .Evaluate(satisfied);

    // TMetadata == TUnderlyingMetadata (int == int): the underlying-with-values cast succeeds.
    private static SpecDecoratorMultiMetadataBooleanResult<bool, int, int> CompatibleDecorator(
        BooleanResultBase<int> underlying) =>
        new(underlying, true, (_, _) => new[] { 10, 20 }, new SpecDescription("decorated"));

    // TMetadata (string) != TUnderlyingMetadata (int): the underlying-with-values cast fails and falls back to [].
    private static SpecDecoratorMultiMetadataBooleanResult<bool, string, int> IncompatibleDecorator(
        BooleanResultBase<int> underlying) =>
        new(underlying, true, (_, _) => new[] { "meta a", "meta b" }, new SpecDescription("decorated"));

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldMirrorUnderlyingResult(bool satisfied)
    {
        var result = CompatibleDecorator(IntUnderlying(satisfied));

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void MetadataTier_ShouldExposeResolvedMetadata(bool satisfied)
    {
        var result = CompatibleDecorator(IntUnderlying(satisfied));

        result.MetadataTier.Metadata.ShouldBe([10, 20]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Underlying_ShouldContainTheUnderlyingResult(bool satisfied)
    {
        var underlying = IntUnderlying(satisfied);
        var result = CompatibleDecorator(underlying);

        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Causes_ShouldContainTheUnderlyingResult(bool satisfied)
    {
        var underlying = IntUnderlying(satisfied);
        var result = CompatibleDecorator(underlying);

        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldContainTheUnderlyingResult_WhenMetadataCompatible(bool satisfied)
    {
        var underlying = IntUnderlying(satisfied);
        var result = CompatibleDecorator(underlying);

        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldContainTheUnderlyingResult_WhenMetadataCompatible(bool satisfied)
    {
        var underlying = IntUnderlying(satisfied);
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
