using AutoFixture;
using Motiv.HigherOrderProposition;
using Motiv.Shared;
using NSubstitute;

namespace Motiv.Tests.HigherOrderProposition;

public class PolicyResultTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenModelIsNull()
    {
        // Act & Assert
        var underlyingResult = Substitute.For<PolicyResultBase<TestMetadata>>();

        var act = () => new PolicyResult<TestModel, TestMetadata>(null!, underlyingResult);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [AutoData]
    public void Constructor_ShouldThrowArgumentNullException_WhenUnderlyingResultIsNull(
        TestModel model)
    {
        // Act & Assert
        var act = () => new PolicyResult<TestModel, TestMetadata>(model, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [AutoData]
    public void Constructor_ShouldSetPropertiesCorrectly(
        TestModel model,
        bool satisfied,
        TestMetadata value)
    {
        // Arrange
        var underlyingResult = Substitute.For<PolicyResultBase<TestMetadata>>();
        underlyingResult.Satisfied.Returns(satisfied);
        underlyingResult.Value.Returns(value);

        // Act
        var result = new PolicyResult<TestModel, TestMetadata>(model, underlyingResult);

        // Assert
        result.Model.Should().Be(model);
        result.Satisfied.Should().Be(satisfied);
        result.Value.Should().Be(value);
    }

    [Theory]
    [AutoData]
    public void Description_ShouldReturnUnderlyingResultDescription(
        TestModel model,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var result = new PolicyResult<TestModel, TestMetadata>(model, underlyingResult);

        // Act
        var resultDescription = result.Description;

        // Assert
        resultDescription.Should().Be(underlyingResult.Description);
    }

    [Theory]
    [AutoData]
    public void Explanation_ShouldReturnUnderlyingResultExplanation(
        TestModel model,
        Explanation explanation)
    {
        // Arrange
        var underlyingResult = Substitute.For<PolicyResultBase<TestMetadata>>();
        underlyingResult.Explanation.Returns(explanation);
        var result = new PolicyResult<TestModel, TestMetadata>(model, underlyingResult);

        // Act
        var resultExplanation = result.Explanation;

        // Assert
        resultExplanation.Should().Be(explanation);
    }

    [Theory]
    [AutoData]
    public void MetadataTier_ShouldReturnUnderlyingResultMetadataTier(
        TestModel model,
        MetadataNode<TestMetadata> metadataTier)
    {
        // Arrange
        var underlyingResult = Substitute.For<PolicyResultBase<TestMetadata>>();
        underlyingResult.MetadataTier.Returns(metadataTier);
        var result = new PolicyResult<TestModel, TestMetadata>(model, underlyingResult);

        // Act
        var resultMetadataTier = result.MetadataTier;

        // Assert
        resultMetadataTier.Should().Be(metadataTier);
    }

    [Theory]
    [AutoData]
    public void Underlying_ShouldReturnUnderlyingResultAsEnumerable(
        TestModel model)
    {
        // Arrange
        var underlyingResult = Substitute.For<PolicyResultBase<TestMetadata>>();
        var result = new PolicyResult<TestModel, TestMetadata>(model, underlyingResult);

        // Act
        var underlying = result.Underlying;

        // Assert
        underlying.Should().ContainSingle().Which.Should().Be(underlyingResult);
    }

    [Theory]
    [AutoData]
    public void UnderlyingWithValues_ShouldReturnUnderlyingResultAsEnumerable(
        TestModel model)
    {
        // Arrange
        var underlyingResult = Substitute.For<PolicyResultBase<TestMetadata>>();
        var result = new PolicyResult<TestModel, TestMetadata>(model, underlyingResult);

        // Act
        var underlyingWithValues = result.UnderlyingWithValues;

        // Assert
        underlyingWithValues.Should().ContainSingle().Which.Should().Be(underlyingResult);
    }

    [Theory]
    [AutoData]
    public void Causes_ShouldReturnUnderlyingResultAsEnumerable(
        TestModel model)
    {
        // Arrange
        var underlyingResult = Substitute.For<PolicyResultBase<TestMetadata>>();
        var result = new PolicyResult<TestModel, TestMetadata>(model, underlyingResult);

        // Act
        var causes = result.Causes;

        // Assert
        causes.Should().ContainSingle().Which.Should().Be(underlyingResult);
    }

    [Theory]
    [AutoData]
    public void CausesWithValues_ShouldReturnUnderlyingResultAsEnumerable(
        TestModel model)
    {
        // Arrange
        var underlyingResult = Substitute.For<PolicyResultBase<TestMetadata>>();
        var result = new PolicyResult<TestModel, TestMetadata>(model, underlyingResult);

        // Act
        var causesWithValues = result.CausesWithValues;

        // Assert
        causesWithValues.Should().ContainSingle().Which.Should().Be(underlyingResult);
    }


    private static Customizations.PolicyResult<TestMetadata> CreateSatisfiedPolicyResult(IFixture fixture) =>
        new(
            true,
            fixture.Create<MetadataNode<TestMetadata>>(),
            fixture.Create<Explanation>(),
            fixture.Create<ResultDescriptionBase>(),
            fixture.CreateMany<BooleanResultBase<TestMetadata>>(),
            fixture.CreateMany<BooleanResultBase<TestMetadata>>(),
            fixture.CreateMany<BooleanResultBase<TestMetadata>>(),
            fixture.CreateMany<BooleanResultBase<TestMetadata>>(),
            fixture.Create<TestMetadata>());
}

