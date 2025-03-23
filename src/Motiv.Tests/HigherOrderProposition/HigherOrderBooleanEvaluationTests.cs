using Motiv.HigherOrderProposition;
using Shouldly;

namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderBooleanEvaluationTests
{
    private static List<ModelResult<string>> CreateModelResults(params bool[] satisfiedValues)
    {
        return satisfiedValues.Select((s, i) => new ModelResult<string>($"Model{i}", s)).ToList();
    }

    [Fact]
    public void AllSatisfied_ReturnsTrue_WhenAllResultsAreSatisfied()
    {
        // Arrange
        var results = CreateModelResults(true, true, true);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);

        // Act
        bool allSatisfied = sut.AllSatisfied;

        // Assert
        allSatisfied.ShouldBeTrue();
    }

    [Fact]
    public void AllSatisfied_ReturnsFalse_WhenAnyResultIsNotSatisfied()
    {
        // Arrange
        var results = CreateModelResults(true, false, true);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);

        // Act
        bool allSatisfied = sut.AllSatisfied;

        // Assert
        allSatisfied.ShouldBeFalse();
    }

    [Fact]
    public void AnySatisfied_ReturnsTrue_WhenAtLeastOneResultIsSatisfied()
    {
        // Arrange
        var results = CreateModelResults(false, true, false);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);

        // Act
        bool anySatisfied = sut.AnySatisfied;

        // Assert
        anySatisfied.ShouldBeTrue();
    }

    [Fact]
    public void AnySatisfied_ReturnsFalse_WhenNoResultsAreSatisfied()
    {
        // Arrange
        var results = CreateModelResults(false, false, false);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);

        // Act
        bool anySatisfied = sut.AnySatisfied;

        // Assert
        anySatisfied.ShouldBeFalse();
    }

    [Fact]
    public void NoneSatisfied_ReturnsTrue_WhenNoResultsAreSatisfied()
    {
        // Arrange
        var results = CreateModelResults(false, false, false);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);

        // Act
        bool noneSatisfied = sut.NoneSatisfied;

        // Assert
        noneSatisfied.ShouldBeTrue();
    }

    [Fact]
    public void NoneSatisfied_ReturnsFalse_WhenAnyResultIsSatisfied()
    {
        // Arrange
        var results = CreateModelResults(false, true, false);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);

        // Act
        bool noneSatisfied = sut.NoneSatisfied;

        // Assert
        noneSatisfied.ShouldBeFalse();
    }

    [Fact]
    public void Models_ReturnsAllModels()
    {
        // Arrange
        var results = CreateModelResults(true, false, true);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);

        // Act
        var models = sut.Models;

        // Assert
        models.ShouldBe(results.Select(r => r.Model));
    }

    [Fact]
    public void TrueModels_ReturnsOnlySatisfiedModels()
    {
        // Arrange
        var results = CreateModelResults(true, false, true);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);
        var satisfiedModels = results.Where(r => r.Satisfied).Select(r => r.Model).ToList();

        // Act
        var trueModels = sut.TrueModels;

        // Assert
        trueModels.ShouldBe(satisfiedModels);
    }

    [Fact]
    public void FalseModels_ReturnsOnlyUnsatisfiedModels()
    {
        // Arrange
        var results = CreateModelResults(true, false, true);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);
        var unsatisfiedModels = results.Where(r => !r.Satisfied).Select(r => r.Model).ToList();

        // Act
        var falseModels = sut.FalseModels;

        // Assert
        falseModels.ShouldBe(unsatisfiedModels);
    }

    [Fact]
    public void CausalModels_ReturnsCausalModels()
    {
        // Arrange
        var allResults = CreateModelResults(true, false, true);
        var causalResults = CreateModelResults(true, true);
        var sut = new HigherOrderBooleanEvaluation<string>(allResults, causalResults);

        // Act
        var causalModels = sut.CausalModels;

        // Assert
        causalModels.ShouldBe(causalResults.Select(r => r.Model));
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        // Arrange
        var results = CreateModelResults(true, false, true);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);

        // Act
        int count = sut.Count;

        // Assert
        count.ShouldBe(results.Count);
    }

    [Fact]
    public void TrueCount_ReturnsCorrectCount()
    {
        // Arrange
        var results = CreateModelResults(true, false, true);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);
        int expectedTrueCount = results.Count(r => r.Satisfied);

        // Act
        int trueCount = sut.TrueCount;

        // Assert
        trueCount.ShouldBe(expectedTrueCount);
    }

    [Fact]
    public void FalseCount_ReturnsCorrectCount()
    {
        // Arrange
        var results = CreateModelResults(true, false, true);
        var sut = new HigherOrderBooleanEvaluation<string>(results, results);
        int expectedFalseCount = results.Count(r => !r.Satisfied);

        // Act
        int falseCount = sut.FalseCount;

        // Assert
        falseCount.ShouldBe(expectedFalseCount);
    }

    [Fact]
    public void CausalCount_ReturnsCorrectCount()
    {
        // Arrange
        var allResults = CreateModelResults(true, false, true);
        var causalResults = CreateModelResults(true, true);
        var sut = new HigherOrderBooleanEvaluation<string>(allResults, causalResults);

        // Act
        int causalCount = sut.CausalCount;

        // Assert
        causalCount.ShouldBe(causalResults.Count);
    }
}
