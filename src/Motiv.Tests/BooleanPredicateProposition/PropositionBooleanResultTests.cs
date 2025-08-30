using AutoFixture;
using Motiv.BooleanPredicateProposition;
using Motiv.Shared;

namespace Motiv.Tests.BooleanPredicateProposition;

public class PropositionBooleanResultTests
{

    [Theory, AutoData]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly(
        bool satisfied,
        Lazy<MetadataNode<string>> metadataTier,
        Lazy<Explanation> explanation,
        Lazy<ResultDescriptionBase> description)
    {
        var result = new PropositionBooleanResult<string>(satisfied, metadataTier, explanation, description);

        result.Satisfied.ShouldBe(satisfied);
        result.MetadataTier.ShouldBe(metadataTier.Value);
        result.Explanation.ShouldBe(explanation.Value);
        result.Description.ShouldBe(description.Value);
    }

    [Theory, AutoData]
    public void Underlying_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionBooleanResult<string>>();

        result.Underlying.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void UnderlyingWithValues_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionBooleanResult<string>>();

        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void Causes_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionBooleanResult<string>>();

        result.Causes.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void CausesWithValues_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionBooleanResult<string>>();

        result.CausesWithValues.ShouldBeEmpty();
    }
}
