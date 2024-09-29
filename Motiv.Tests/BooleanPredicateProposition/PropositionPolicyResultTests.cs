using AutoFixture;
using Motiv.BooleanPredicateProposition;
using Motiv.Shared;

namespace Motiv.Tests.BooleanPredicateProposition;

public class PropositionPolicyResultTests
{
    [Theory, AutoData]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly(
        bool satisfied,
        Lazy<string> lazyValue,
        Lazy<MetadataNode<string>> metadataTier,
        Lazy<Explanation> explanation,
        Lazy<ResultDescriptionBase> description)
    {
        var result = new PropositionPolicyResult<string>(satisfied, lazyValue, metadataTier, explanation, description);

        result.Satisfied.Should().Be(satisfied);
        result.Value.Should().Be(lazyValue.Value);
        result.MetadataTier.Should().Be(metadataTier.Value);
        result.Explanation.Should().Be(explanation.Value);
        result.Description.Should().Be(description.Value);
    }

    [Theory, AutoData]
    public void Underlying_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionPolicyResult<string>>();
        result.Underlying.Should().BeEmpty();
    }

    [Theory, AutoData]
    public void UnderlyingWithValues_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionPolicyResult<string>>();
        result.UnderlyingWithValues.Should().BeEmpty();
    }

    [Theory, AutoData]
    public void Causes_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionPolicyResult<string>>();
        result.Causes.Should().BeEmpty();
    }

    [Theory, AutoData]
    public void CausesWithValues_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionPolicyResult<string>>();
        result.CausesWithValues.Should().BeEmpty();
    }
}
