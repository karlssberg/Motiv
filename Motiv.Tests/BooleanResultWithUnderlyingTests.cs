using AutoFixture;
using Motiv.Tests.Customizations;
using NSubstitute;

namespace Motiv.Tests;

public class BooleanResultWithUnderlyingTests
{
    [Theory]
    [AutoData]
    public void Satisfied_ShouldReturnUnderlyingResultSatisfied(bool satisfied, IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestUnderlyingMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying<TestMetadata, TestUnderlyingMetadata>(underlyingResult);

        // Act
        var resultSatisfied = result.Satisfied;

        // Assert
        resultSatisfied.Should().Be(satisfied);
    }

    [Theory]
    [AutoData]
    public void Description_ShouldReturnDescriptionFromFunc(ResultDescriptionBase description, IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestUnderlyingMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying<TestMetadata, TestUnderlyingMetadata>(underlyingResult, descriptionFunc: () => description);

        // Act
        var resultDescription = result.Description;

        // Assert
        resultDescription.Should().Be(description);
    }

    [Theory]
    [AutoData]
    public void Explanation_ShouldReturnExplanationFromFunc(Explanation explanation, IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestUnderlyingMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying<TestMetadata, TestUnderlyingMetadata>(underlyingResult, explanationFunc: () => explanation);

        // Act
        var resultExplanation = result.Explanation;

        // Assert
        resultExplanation.Should().Be(explanation);
    }

    [Theory]
    [AutoData]
    public void MetadataTier_ShouldReturnMetadataTierFromFunc(MetadataNode<TestMetadata> metadataTier, IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestUnderlyingMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying(underlyingResult, metadataTierFunc: () => metadataTier);

        // Act
        var resultMetadataTier = result.MetadataTier;

        // Assert
        resultMetadataTier.Should().Be(metadataTier);
    }

    [Theory]
    [AutoData]
    public void Underlying_ShouldReturnUnderlyingResultAsEnumerable(IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestUnderlyingMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying<TestMetadata, TestUnderlyingMetadata>(underlyingResult);

        // Act
        var underlying = result.Underlying;

        // Assert
        underlying.Should().ContainSingle().Which.Should().Be(underlyingResult);
    }

    [Theory]
    [AutoData]
    public void UnderlyingWithValues_ShouldReturnEmptyWhenTypesDoNotMatch(IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestUnderlyingMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying<TestMetadata, TestUnderlyingMetadata>(underlyingResult);

        // Act
        var underlyingWithValues = result.UnderlyingWithValues;

        // Assert
        underlyingWithValues.Should().BeEmpty();
    }

    [Theory]
    [AutoData]
    public void UnderlyingWithValues_ShouldReturnUnderlyingResultWhenTypesMatch(IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying<TestMetadata, TestMetadata>(underlyingResult);

        // Act
        var underlyingWithValues = result.UnderlyingWithValues;

        // Assert
        underlyingWithValues.Should().ContainSingle().Which.Should().Be(underlyingResult);
    }

    [Theory]
    [AutoData]
    public void Causes_ShouldReturnUnderlyingResultAsEnumerable(IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestUnderlyingMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying<TestMetadata, TestUnderlyingMetadata>(underlyingResult);

        // Act
        var causes = result.Causes;

        // Assert
        causes.Should().ContainSingle().Which.Should().Be(underlyingResult);
    }

    [Theory]
    [AutoData]
    public void CausesWithValues_ShouldReturnEmptyWhenTypesDoNotMatch(IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestUnderlyingMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying<TestMetadata, TestUnderlyingMetadata>(underlyingResult);

        // Act
        var causesWithValues = result.CausesWithValues;

        // Assert
        causesWithValues.Should().BeEmpty();
    }

    [Theory]
    [AutoData]
    public void CausesWithValues_ShouldReturnUnderlyingResultWhenTypesMatch(IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult<TestMetadata>(fixture);
        var result = CreateBooleanResultWithUnderlying<TestMetadata, TestMetadata>(underlyingResult);

        // Act
        var causesWithValues = result.CausesWithValues;

        // Assert
        causesWithValues.Should().ContainSingle().Which.Should().Be(underlyingResult);
    }

    private static BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata> CreateBooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
        BooleanResultBase<TUnderlyingMetadata> underlyingResult,
        Func<MetadataNode<TMetadata>>? metadataTierFunc = null,
        Func<Explanation>? explanationFunc = null,
        Func<ResultDescriptionBase>? descriptionFunc = null)
    {
        var metadataTier = metadataTierFunc ?? CreateMetadataNode<TMetadata>;
        var explanation = explanationFunc ?? CreateExplanation;
        var description = descriptionFunc ?? CreateResultDescriptionBase;

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            underlyingResult,
            metadataTier,
            explanation,
            description);
    }

    private static ResultDescriptionBase CreateResultDescriptionBase() => Substitute.For<ResultDescriptionBase>();

    private static MetadataNode<TMetadata> CreateMetadataNode<TMetadata>() => new([], []);

    private static Explanation CreateExplanation() => new(Enumerable.Empty<string>(), []);

    public class TestMetadata
    {
    }

    private static PolicyResult<T> CreateSatisfiedPolicyResult<T>(IFixture fixture) =>
        new(
            true,
            fixture.Create<MetadataNode<T>>(),
            fixture.Create<Explanation>(),
            fixture.Create<ResultDescriptionBase>(),
            fixture.CreateMany<BooleanResultBase<T>>(),
            fixture.CreateMany<BooleanResultBase<T>>(),
            fixture.CreateMany<BooleanResultBase<T>>(),
            fixture.CreateMany<BooleanResultBase<T>>(),
            fixture.Create<T>());

    private static PolicyResult<T> CreateUnsatisfiedPolicyResult<T>(IFixture fixture) =>
        new(
            false,
            fixture.Create<MetadataNode<T>>(),
            fixture.Create<Explanation>(),
            fixture.Create<ResultDescriptionBase>(),
            fixture.CreateMany<BooleanResultBase<T>>(),
            fixture.CreateMany<BooleanResultBase<T>>(),
            fixture.CreateMany<BooleanResultBase<T>>(),
            fixture.CreateMany<BooleanResultBase<T>>(),
            fixture.Create<T>());

    public class TestUnderlyingMetadata
    {
    }
}
