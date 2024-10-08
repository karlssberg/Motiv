using AutoFixture;
using Motiv.HigherOrderProposition;
using Motiv.Shared;
using Motiv.Tests.Customizations;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderPolicyResultEvaluationTests
{
    [Theory, AutoData]
    public void Constructor_ShouldInitializePropertiesCorrectly(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Assert
        evaluation.Results.Should().BeEquivalentTo(results);
        evaluation.CausalResults.Should().BeEquivalentTo(causes);
        evaluation.Count.Should().Be(results.Count);
        evaluation.CausalCount.Should().Be(causes.Count);
    }

    [Theory, AutoData]
    public void AllSatisfied_ShouldReturnTrue_WhenAllResultsAreSatisfied(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
                  .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
                  .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var allSatisfied = evaluation.AllSatisfied;

        // Assert
        allSatisfied.Should().BeTrue();
    }

    [Theory, AutoData]
    public void AnySatisfied_ShouldReturnTrue_WhenAtLeastOneResultIsSatisfied(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var anySatisfied = evaluation.AnySatisfied;

        // Assert
        anySatisfied.Should().BeTrue();
    }

    [Theory, AutoData]
    public void NoneSatisfied_ShouldReturnTrue_WhenNoResultsAreSatisfied(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateUnsatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var noneSatisfied = evaluation.NoneSatisfied;

        // Assert
        noneSatisfied.Should().BeTrue();
    }

    [Theory, AutoData]
    public void Models_ShouldReturnAllModels(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results.Select(r => r.Model);

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var evaluationModels = evaluation.Models;

        // Assert
        evaluationModels.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void TrueModels_ShouldReturnModelsWithTrueResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results
            .Where(r => r.Satisfied)
            .Select(r => r.Model);

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var trueModels = evaluation.TrueModels;

        // Assert
        trueModels.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void FalseModels_ShouldReturnModelsWithFalseResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results
            .Where(r => !r.Satisfied)
            .Select(r => r.Model);

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var falseModels = evaluation.FalseModels;

        // Assert
        falseModels.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void CausalModels_ShouldReturnModelsFromCausalResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        List<PolicyResult<TestMetadata>> underlyingResult =
        [
            CreateSatisfiedPolicyResult(fixture),
            CreateUnsatisfiedPolicyResult(fixture),
            CreateSatisfiedPolicyResult(fixture)
        ];

        var results =  models
            .Zip(underlyingResult, (model, result) => (model, result))
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m.model, m.result))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = causes.Select(r => r.Model);

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var causalModels = evaluation.CausalModels;

        // Assert
        causalModels.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void Values_ShouldReturnValuesFromResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = causes.Select(r => r.Value);

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);
        // Act
        var values = evaluation.Values;

        // Assert
        values.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void Metadata_ShouldReturnMetadataFromCausalResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingWithValues = CreateSatisfiedPolicyResult(fixture);
        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingWithValues))
            .ToList();
        var causesWithValues = results.Take(2).ToArray();
        var metadataTier = new MetadataNode<TestMetadata>(causesWithValues.SelectMany(r => r.Values), causesWithValues);

        var expected = metadataTier.Metadata;

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var metadata = evaluation.Metadata;

        // Assert
        metadata.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void Assertions_ShouldReturnAssertionsFromCausalResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = causes.SelectMany(r => r.Assertions).DistinctWithOrderPreserved();

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var assertions = evaluation.Assertions;

        // Assert
        assertions.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void TrueResults_ShouldReturnOnlyTrueResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results.Where(r => r.Satisfied);

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var trueResults = evaluation.TrueResults;

        // Assert
        trueResults.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void FalseResults_ShouldReturnOnlyFalseResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results.Where(r => !r.Satisfied);

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var falseResults = evaluation.FalseResults;

        // Assert
        falseResults.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void TrueCount_ShouldReturnCorrectCount(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results.Count(r => r.Satisfied);

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);
        // Act
        var trueCount = evaluation.TrueCount;

        // Assert
        trueCount.Should().Be(expected);
    }

    [Theory, AutoData]
    public void FalseCount_ShouldReturnCorrectCount(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedPolicyResult(fixture);

        var results =  models
            .Select(m => new PolicyResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results.Count(r => !r.Satisfied);

        var evaluation = new HigherOrderPolicyResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var falseCount = evaluation.FalseCount;

        // Assert
        falseCount.Should().Be(expected);
    }

    private static PolicyResult<TestMetadata> CreateSatisfiedPolicyResult(IFixture fixture) =>
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

    private static PolicyResult<TestMetadata> CreateUnsatisfiedPolicyResult(IFixture fixture) =>
        new(
            false,
            fixture.Create<MetadataNode<TestMetadata>>(),
            fixture.Create<Explanation>(),
            fixture.Create<ResultDescriptionBase>(),
            fixture.CreateMany<BooleanResultBase<TestMetadata>>(),
            fixture.CreateMany<BooleanResultBase<TestMetadata>>(),
            fixture.CreateMany<BooleanResultBase<TestMetadata>>(),
            fixture.CreateMany<BooleanResultBase<TestMetadata>>(),
            fixture.Create<TestMetadata>());
}
