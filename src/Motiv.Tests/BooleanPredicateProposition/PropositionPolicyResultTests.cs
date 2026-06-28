using AutoFixture;
using Motiv.BooleanPredicateProposition;
using Motiv.Shared;

namespace Motiv.Tests.BooleanPredicateProposition;

public class PropositionPolicyResultTests
{
    [Theory, AutoData]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly(
        bool satisfied,
        string value,
        MetadataNode<string> metadataTier,
        Explanation explanation,
        ResultDescriptionBase description)
    {
        var result = new PropositionPolicyResult<string>(satisfied, () => value, () => metadataTier, () => explanation, () => description);

        result.Satisfied.ShouldBe(satisfied);
        result.Value.ShouldBe(value);
        result.MetadataTier.ShouldBe(metadataTier);
        result.Explanation.ShouldBe(explanation);
        result.Description.ShouldBe(description);
    }

    [Theory, AutoData]
    public void Underlying_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionPolicyResult<string>>();
        result.Underlying.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void UnderlyingWithValues_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionPolicyResult<string>>();
        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void Causes_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionPolicyResult<string>>();
        result.Causes.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void CausesWithValues_AlwaysReturnsEmptyCollection(IFixture fixture)
    {
        var result = fixture.Create<PropositionPolicyResult<string>>();
        result.CausesWithValues.ShouldBeEmpty();
    }
}
