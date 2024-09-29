using AutoFixture;
using Motiv.HigherOrderProposition;
using Motiv.Shared;
using Motiv.Tests.Customizations;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderBooleanResultEvaluationTests
{

    [Theory, AutoData]
    public void Constructor_ShouldInitializePropertiesCorrectly(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        List<BooleanResult<TestMetadata>> underlyingResults =
        [
            CreateSatisfiedBooleanResult(fixture),
            CreateUnsatisfiedBooleanResult(fixture),
            CreateSatisfiedBooleanResult(fixture)
        ];

        var results =  models.Zip(underlyingResults, (model, result) => (model, result))
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m.model, m.result))
            .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

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
        List<BooleanResult<TestMetadata>> underlyingResults =
        [
            CreateSatisfiedBooleanResult(fixture),
            CreateSatisfiedBooleanResult(fixture),
            CreateSatisfiedBooleanResult(fixture)
        ];

        var results =  models.Zip(underlyingResults, (model, result) => (model, result))
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m.model, m.result))
            .ToList();

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, results);

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
        List<BooleanResult<TestMetadata>> underlyingResults =
        [
            CreateSatisfiedBooleanResult(fixture),
            CreateUnsatisfiedBooleanResult(fixture),
            CreateSatisfiedBooleanResult(fixture)
        ];

        var results =  models.Zip(underlyingResults, (model, result) => (model, result))
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m.model, m.result))
            .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

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
        List<BooleanResult<TestMetadata>> underlyingResults =
        [
            CreateUnsatisfiedBooleanResult(fixture),
            CreateUnsatisfiedBooleanResult(fixture),
            CreateUnsatisfiedBooleanResult(fixture)
        ];

        var results =  models.Zip(underlyingResults, (model, result) => (model, result))
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m.model, m.result))
            .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

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
        List<BooleanResult<TestMetadata>> underlyingResults =
        [
            CreateSatisfiedBooleanResult(fixture),
            CreateUnsatisfiedBooleanResult(fixture),
            CreateSatisfiedBooleanResult(fixture)
        ];

        var results =  models.Zip(underlyingResults, (model, result) => (model, result))
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m.model, m.result))
            .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var evaluationModels = evaluation.Models;

        // Assert
        evaluationModels.Should().BeEquivalentTo(results.Select(r => r.Model));
    }

    [Theory, AutoData]
    public void TrueModels_ShouldReturnModelsWithTrueResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        List<BooleanResult<TestMetadata>> underlyingResults =
        [
            CreateSatisfiedBooleanResult(fixture),
            CreateUnsatisfiedBooleanResult(fixture),
            CreateSatisfiedBooleanResult(fixture)
        ];

        var results =  models.Zip(underlyingResults, (model, result) => (model, result))
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m.model, m.result))
            .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var trueModels = evaluation.TrueModels;

        // Assert
        trueModels.Should().BeEquivalentTo(results.Where(r => r.Satisfied).Select(r => r.Model));
    }

    [Theory, AutoData]
    public void FalseModels_ShouldReturnModelsWithFalseResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        List<BooleanResult<TestMetadata>> underlyingResults =
        [
            CreateSatisfiedBooleanResult(fixture),
            CreateUnsatisfiedBooleanResult(fixture),
            CreateSatisfiedBooleanResult(fixture)
        ];

        var results =  models.Zip(underlyingResults, (model, result) => (model, result))
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m.model, m.result))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results.Where(r => !r.Satisfied).Select(r => r.Model);

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

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
        var underlyingResult = CreateSatisfiedBooleanResult(fixture);


        var results =  models
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

        var expected = causes.Select(r => r.Model);

        // Act
        var causalModels = evaluation.CausalModels;

        // Assert
        causalModels.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void Values_ShouldReturnMetadataFromCausalResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedBooleanResult(fixture);

        var results =  models
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = causes
            .SelectMany(r => r.MetadataTier.Metadata)
            .DistinctWithOrderPreserved();

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var values = evaluation.Values;

        // Assert
        values.Should().BeEquivalentTo(expected);
    }

    [Theory, AutoData]
    public void Assertions_ShouldReturnAssertionsFromCausalResults(
        List<TestModel> models,
        IFixture fixture)
    {
        // Arrange
        var underlyingResult = CreateSatisfiedBooleanResult(fixture);

        var results =  models
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();

        var expected = causes.SelectMany(r => r.Assertions).Distinct();

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

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
        var underlyingResult = CreateSatisfiedBooleanResult(fixture);

        var results =  models
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();

        var expected = results.Where(r => r.Satisfied);

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

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
        var underlyingResult = CreateSatisfiedBooleanResult(fixture);

        var results =  models
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results.Where(r => !r.Satisfied);

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

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
        var underlyingResult = CreateSatisfiedBooleanResult(fixture);

        var results =  models
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results.Count(r => r.Satisfied);

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

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
        var underlyingResult = CreateUnsatisfiedBooleanResult(fixture);

        var results =  models
            .Select(m => new BooleanResult<TestModel, TestMetadata>(m, underlyingResult))
            .ToList();

        var causes = results.Take(2).ToList();
        var expected = results.Count(r => !r.Satisfied);

        var evaluation = new HigherOrderBooleanResultEvaluation<TestModel, TestMetadata>(results, causes);

        // Act
        var falseCount = evaluation.FalseCount;

        // Assert
        falseCount.Should().Be(expected);
    }


    private static BooleanResult<TestMetadata> CreateSatisfiedBooleanResult(IFixture fixture)
    {
        var underlying = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();

        return new BooleanResult<TestMetadata>(
            true,
            fixture.Create<MetadataNode<TestMetadata>>(),
            fixture.Create<Explanation>(),
            fixture.Create<ResultDescriptionBase>(),
            underlying,
            underlying);
    }

    private static BooleanResult<TestMetadata> CreateUnsatisfiedBooleanResult(IFixture fixture)
    {
        var underlying = fixture.CreateMany<BooleanResultBase<TestMetadata>>().ToList();

        return new BooleanResult<TestMetadata>(
            false,
            fixture.Create<MetadataNode<TestMetadata>>(),
            fixture.Create<Explanation>(),
            fixture.Create<ResultDescriptionBase>(),
            underlying,
            underlying);
    }
}

