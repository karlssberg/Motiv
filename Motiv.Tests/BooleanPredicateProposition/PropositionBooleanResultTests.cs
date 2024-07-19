using AutoFixture;
using Motiv.BooleanPredicateProposition;

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

        result.Satisfied.Should().Be(satisfied);
        result.MetadataTier.Should().Be(metadataTier.Value);
        result.Explanation.Should().Be(explanation.Value);
        result.Description.Should().Be(description.Value);
    }

    [Theory, AutoData]
    public void Underlying_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionBooleanResult<string>>();

        result.Underlying.Should().BeEmpty();
    }

    [Theory, AutoData]
    public void UnderlyingWithValues_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionBooleanResult<string>>();

        result.UnderlyingWithValues.Should().BeEmpty();
    }

    [Theory, AutoData]
    public void Causes_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionBooleanResult<string>>();

        result.Causes.Should().BeEmpty();
    }

    [Theory, AutoData]
    public void CausesWithValues_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionBooleanResult<string>>();

        result.CausesWithValues.Should().BeEmpty();
    }
}
