namespace Motiv.Tests;

public class HigherOrderBooleanEvaluationTests
{
    public class Metadata(string text)
    {
        public string Text => text;
    }

    [Theory]
    [InlineData(true, true, "all true == true")]
    [InlineData(true, false, "all true == true")]
    [InlineData(false, true, "all true == true")]
    public void Should_yield_using_all_satisfied_property(bool modelA, bool modelB, params string[] expected)
    {
        // Arrange
        var higherOrder = Spec
            .Build((bool b) => b)
            .AsAnySatisfied()
            .WhenTrue(eval => eval.AllSatisfied ? "all true" : "some false")
            .WhenFalse("invalid")
            .Create("all true");

        var result = higherOrder.Evaluate([modelA, modelB]);

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, "all true == true")]
    [InlineData(false, true, "all true == true")]
    [InlineData(true, false, "all true == true")]
    public void Should_yield_using_any_satisfied_property(bool modelA, bool modelB, params string[] expected)
    {
        // Arrange
        var higherOrder = Spec
            .Build((bool b) => b)
            .AsAnySatisfied()
            .WhenTrue(eval => eval.AnySatisfied ? "any true" : "all false")
            .WhenFalse("invalid")
            .Create("all true");

        var result = higherOrder.Evaluate([modelA, modelB]);

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, false, "all true == true")]
    [InlineData(false, true, "all true == false")]
    [InlineData(true, false, "all true == false")]
    [InlineData(true, true, "all true == false")]
    public void Should_yield_using_none_satisfied_property(bool modelA, bool modelB, params string[] expected)
    {
        // Arrange
        var higherOrder = Spec
            .Build((bool b) => b)
            .AsNoneSatisfied()
            .WhenTrue(eval => eval.NoneSatisfied ? "none true" : "some true")
            .WhenFalse(eval => eval.NoneSatisfied ? "none true" : "some true")
            .Create("all true");

        var result = higherOrder.Evaluate([modelA, modelB]);

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, 2)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 2)]
    public void Should_yield_underlying_metadata_using_causal_count_property(
        bool modelA,
        bool modelB,
        params int[] expected)
    {
        // Arrange
        var higherOrder = Spec
            .Build((bool b) => b)
            .AsAnySatisfied()
            .WhenTrue(eval => eval.CausalCount)
            .WhenFalse(eval => eval.CausalCount)
            .Create("all true");

        var result = higherOrder.Evaluate([modelA, modelB]);

        // Act
        var act = result.Values;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false)]
    public void Should_yield_underlying_metadata_using_true_models_property(
        bool modelA,
        bool modelB,
        params bool[] expected)
    {
        // Arrange
        var higherOrder = Spec
            .Build((bool b) => b)
            .AsAllSatisfied()
            .WhenTrueYield(eval => eval.TrueModels)
            .WhenFalseYield(eval => eval.TrueModels)
            .Create("all true");

        var result = higherOrder.Evaluate([modelA, modelB]);

        // Act
        var act = result.Values;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Should_yield_underlying_metadata_using_false_models_property(
        bool modelA,
        bool modelB,
        params bool[] expected)
    {
        // Arrange
        var higherOrder = Spec
            .Build((bool b) => b)
            .AsAllSatisfied()
            .WhenTrueYield(eval => eval.FalseModels)
            .WhenFalseYield(eval => eval.FalseModels)
            .Create("all true");

        var result = higherOrder.Evaluate([modelA, modelB]);

        // Act
        var act = result.Values;

        // Assert
        act.ShouldBe(expected.AsEnumerable());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Should_yield_underlying_metadata_using_models_property(
        bool modelA,
        bool modelB)
    {
        // Arrange
        List<bool> models = [modelA, modelB];
        var expected = models.Distinct();

        var higherOrder = Spec
            .Build((bool b) => b)
            .AsAllSatisfied()
            .WhenTrueYield(eval => eval.Models)
            .WhenFalseYield(eval => eval.Models)
            .Create("all true");

        var result = higherOrder.Evaluate(models);

        // Act
        var act = result.Values;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Should_yield_underlying_metadata_using_count_property(
        bool modelA,
        bool modelB)
    {
        // Arrange
        List<bool> models = [modelA, modelB];

        var higherOrder = Spec
            .Build((bool b) => b)
            .AsAllSatisfied()
            .WhenTrue(eval => eval.Count)
            .WhenFalse(eval => eval.Count)
            .Create("all true");

        var result = higherOrder.Evaluate(models);

        // Act
        var act = result.Values;

        // Assert
        act.ShouldBe(2.ToEnumerable());
    }

    [Theory]
    [InlineData(true, true, 2)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 0)]
    public void Should_yield_underlying_metadata_using_true_count_property(
        bool modelA,
        bool modelB,
        int expectedCount)
    {
        // Arrange
        List<bool> models = [modelA, modelB];

        var higherOrder = Spec
            .Build((bool b) => b)
            .AsAllSatisfied()
            .WhenTrue(eval => eval.TrueCount)
            .WhenFalse(eval => eval.TrueCount)
            .Create("all true");

        var result = higherOrder.Evaluate(models);

        // Act
        var act = result.Values;

        // Assert
        act.ShouldBe(expectedCount.ToEnumerable());
    }

    [Theory]
    [InlineData(true, true, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 2)]
    public void Should_yield_underlying_metadata_using_false_count_property(
        bool modelA,
        bool modelB,
        int expectedCount)
    {
        // Arrange
        List<bool> models = [modelA, modelB];

        var higherOrder = Spec
            .Build((bool b) => b)
            .AsAllSatisfied()
            .WhenTrue(eval => eval.FalseCount)
            .WhenFalse(eval => eval.FalseCount)
            .Create("all true");

        var result = higherOrder.Evaluate(models);

        // Act
        var act = result.Values;

        // Assert
        act.ShouldBe(expectedCount.ToEnumerable());
    }
}
