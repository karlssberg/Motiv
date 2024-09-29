using AutoFixture;
using AutoFixture.Xunit2;
using Motiv.HigherOrderProposition;
using Motiv.Shared;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderFromBooleanPredicatePolicyResultTests
{
    [Theory]
    [AutoData]
    public void Constructor_ShouldSetSatisfiedProperty([Frozen] bool isSatisfied, IFixture fixture)
    {
        // Arrange & Act
        var result = fixture.Create<HigherOrderFromBooleanPredicatePolicyResult<TestMetadata>>();

        // Assert
        result.Satisfied.Should().Be(isSatisfied);
    }

    [Theory]
    [AutoData]
    public void Value_ShouldReturnValueFromValueFunc(
        [Frozen] TestMetadata metadata,
        IFixture fixture)
    {
        // Arrange
        var result = fixture.Create<HigherOrderFromBooleanPredicatePolicyResult<TestMetadata>>();

        // Act
        var value = result.Value;

        // Assert
        value.Should().Be(metadata);
    }

    [Theory]
    [AutoData]
    public void MetadataTier_ShouldReturnMetadataFromMetadataFunc(
        [Frozen] MetadataNode<TestMetadata> metadata,
        IFixture fixture)
    {
        // Arrange
        var result = fixture.Create<HigherOrderFromBooleanPredicatePolicyResult<TestMetadata>>();
        // Act
        var metadataTier = result.MetadataTier;

        // Assert
        metadataTier.Should().Be(metadata);
    }

    [Theory]
    [AutoData]
    public void Underlying_ShouldReturnEmptyCollection(IFixture fixture)
    {
        // Arrange
        var result = fixture.Create<HigherOrderFromBooleanPredicatePolicyResult<TestMetadata>>();

        // Act
        var underlying = result.Underlying;

        // Assert
        underlying.Should().BeEmpty();
    }

    [Theory]
    [AutoData]
    public void UnderlyingWithValues_ShouldReturnEmptyCollection(IFixture fixture)
    {
        // Arrange
        var result = fixture.Create<HigherOrderFromBooleanPredicatePolicyResult<TestMetadata>>();

        // Act
        var underlyingWithValues = result.UnderlyingWithValues;

        // Assert
        underlyingWithValues.Should().BeEmpty();
    }

    [Theory]
    [AutoData]
    public void Causes_ShouldReturnEmptyCollection(IFixture fixture)
    {
        // Arrange
        var result = fixture.Create<HigherOrderFromBooleanPredicatePolicyResult<TestMetadata>>();

        // Act
        var causes = result.Causes;

        // Assert
        causes.Should().BeEmpty();
    }

    [Theory]
    [AutoData]
    public void CausesWithValues_ShouldReturnEmptyCollection(IFixture fixture)
    {
        // Arrange
        var result = fixture.Create<HigherOrderFromBooleanPredicatePolicyResult<TestMetadata>>();

        // Act
        var causesWithValues = result.CausesWithValues;

        // Assert
        causesWithValues.Should().BeEmpty();
    }

    [Theory]
    [AutoData]
    public void Description_ShouldReturnDescriptionFromDescriptionFunc(
        [Frozen] ResultDescriptionBase description,
        IFixture fixture)
    {
        // Arrange
        var result = fixture.Create<HigherOrderFromBooleanPredicatePolicyResult<TestMetadata>>();

        // Act
        var resultDescription = result.Description;

        // Assert
        resultDescription.Should().Be(description);
    }

    [Theory]
    [AutoData]
    public void Explanation_ShouldReturnExplanationFromExplanationFunc(
        [Frozen] Explanation explanation,
        IFixture fixture)
    {
        // Arrange
        var result = fixture.Create<HigherOrderFromBooleanPredicatePolicyResult<TestMetadata>>();

        // Act
        var resultExplanation = result.Explanation;

        // Assert
        resultExplanation.Should().Be(explanation);
    }
}
