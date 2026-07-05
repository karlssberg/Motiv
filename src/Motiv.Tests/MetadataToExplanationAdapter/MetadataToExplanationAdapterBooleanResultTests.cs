using Motiv.MetadataToExplanationAdapter;
using Motiv.Shared;

namespace Motiv.Tests.MetadataToExplanationAdapter;

public class MetadataToExplanationAdapterBooleanResultTests
{
    private static BooleanResultBase<int> CreateIncompatibleUnderlying(bool satisfied) =>
        Spec.Build((bool m) => m)
            .WhenTrue(1)
            .WhenFalse(-1)
            .Create("is one")
            .Evaluate(satisfied);

    private static BooleanResultBase<string> CreateCompatibleUnderlying(bool satisfied) =>
        Spec.Build((bool m) => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create()
            .Evaluate(satisfied);

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldMirrorUnderlyingResult(bool satisfied)
    {
        var result = new MetadataToExplanationAdapterBooleanResult<int>(
            CreateIncompatibleUnderlying(satisfied), new SpecDescription("adapted"));

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Underlying_ShouldContainTheUnderlyingResult_WhenMetadataIsIncompatible(bool satisfied)
    {
        var underlying = CreateIncompatibleUnderlying(satisfied);
        var result = new MetadataToExplanationAdapterBooleanResult<int>(underlying, new SpecDescription("adapted"));

        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldBeEmpty_WhenMetadataIsIncompatible(bool satisfied)
    {
        var result = new MetadataToExplanationAdapterBooleanResult<int>(
            CreateIncompatibleUnderlying(satisfied), new SpecDescription("adapted"));

        result.UnderlyingWithValues.ShouldBeEmpty();
        result.CausesWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldContainTheUnderlyingResult_WhenMetadataIsCompatible(bool satisfied)
    {
        var underlying = CreateCompatibleUnderlying(satisfied);
        var result = new MetadataToExplanationAdapterBooleanResult<string>(underlying, new SpecDescription("adapted"));

        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void MetadataTier_ShouldExposeUnderlyingAssertions(bool satisfied)
    {
        var underlying = CreateIncompatibleUnderlying(satisfied);
        var result = new MetadataToExplanationAdapterBooleanResult<int>(underlying, new SpecDescription("adapted"));

        result.MetadataTier.Metadata.ShouldBe(underlying.Assertions);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Explanation_ShouldMirrorUnderlyingExplanation(bool satisfied)
    {
        var underlying = CreateIncompatibleUnderlying(satisfied);
        var result = new MetadataToExplanationAdapterBooleanResult<int>(underlying, new SpecDescription("adapted"));

        result.Explanation.ShouldBeSameAs(underlying.Explanation);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Description_ShouldReportTheAdaptedStatementAsReason(bool satisfied)
    {
        var result = new MetadataToExplanationAdapterBooleanResult<int>(
            CreateIncompatibleUnderlying(satisfied), new SpecDescription("adapted"));

        result.Description.Reason.ShouldBe($"adapted == {(satisfied ? "true" : "false")}");
    }
}
