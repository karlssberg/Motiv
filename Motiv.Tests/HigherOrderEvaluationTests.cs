namespace Motiv.Tests;

public class HigherOrderBooleanResultEvaluationTests
{
    public class Metadata(string text)
    {
        public string Text => text;
    }

    [Fact]
    public void Should_yield_underlying_metadata()
    {
        // Arrange
        var underlying = Spec
            .Build((bool b) => b)
            .WhenTrue(new Metadata("is true"))
            .WhenFalse(new Metadata("is false"))
            .Create("is true");

        var higherOrder = Spec
            .Build(underlying)
            .AsAllSatisfied()
            .WhenTrueYield(eval => eval.Values)
            .WhenFalseYield(eval => eval.Metadata)
            .Create("all true");

        var result = higherOrder.IsSatisfiedBy([true]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([new Metadata("is true")]);
    }

    [Theory]
    [InlineData(true, true, "is true")]
    [InlineData(true, false, "is true")]
    [InlineData(false, true, "is true")]
    [InlineData(false, false)]
    public void Should_yield_underlying_metadata_from_true_results(bool modelA, bool modelB,
        params string[] expectedStrings)
    {
        // Arrange
        var expected = expectedStrings.Select(text => new Metadata(text)).ToArray();

        var underlying = Spec
            .Build((bool b) => b)
            .WhenTrue(new Metadata("is true"))
            .WhenFalse(new Metadata("is false"))
            .Create("is true");

        var higherOrder = Spec
            .Build(underlying)
            .AsAllSatisfied()
            .WhenTrueYield(eval => eval.TrueResults.GetValues())
            .WhenFalseYield(eval => eval.TrueResults.GetValues())
            .Create("all true");

        var result = higherOrder.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false, "is false")]
    [InlineData(false, true, "is false")]
    [InlineData(false, false, "is false")]
    public void Should_yield_underlying_metadata_from_false_results(bool modelA, bool modelB,
        params string[] expectedStrings)
    {
        // Arrange
        var expected = expectedStrings.Select(text => new Metadata(text)).ToArray();

        var underlying = Spec
            .Build((bool b) => b)
            .WhenTrue(new Metadata("is true"))
            .WhenFalse(new Metadata("is false"))
            .Create("is true");

        var higherOrder = Spec
            .Build(underlying)
            .AsAllSatisfied()
            .WhenTrueYield(eval => eval.FalseResults.GetValues())
            .WhenFalseYield(eval => eval.FalseResults.GetValues())
            .Create("all true");

        var result = higherOrder.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(true, true, "all true")]
    [InlineData(true, false, "some false")]
    [InlineData(false, true, "some false")]
    public void Should_yield_using_all_satisfied_property(bool modelA, bool modelB, params string[] expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool b) => b)
            .WhenTrue(new Metadata("is true"))
            .WhenFalse(new Metadata("is false"))
            .Create("is true");

        var higherOrder = Spec
            .Build(underlying)
            .AsAnySatisfied()
            .WhenTrue(eval => eval.AllSatisfied ? "all true" : "some false")
            .WhenFalse(eval => eval.NoneSatisfied ? "none true" : "some true")
            .Create("all true");

        var result = higherOrder.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }


    [Theory]
    [InlineData(true, true, "any true")]
    public void Should_yield_using_any_satisfied_property(bool modelA, bool modelB, params string[] expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool b) => b)
            .WhenTrue(new Metadata("is true"))
            .WhenFalse(new Metadata("is false"))
            .Create("is true");

        var higherOrder = Spec
            .Build(underlying)
            .AsAnySatisfied()
            .WhenTrue(eval => eval.AnySatisfied ? "any true" : "all false")
            .WhenFalse(eval => eval.NoneSatisfied ? "none true" : "some true")
            .Create("all true");

        var result = higherOrder.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(true, true, "is true")]
    [InlineData(true, false, "is true")]
    [InlineData(false, true, "is true")]
    [InlineData(false, false, "is false")]
    public void Should_yield_underlying_metadata_using_causal_results_property(
        bool modelA,
        bool modelB,
        params string[] expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool b) => b)
            .WhenTrue(new Metadata("is true"))
            .WhenFalse(new Metadata("is false"))
            .Create("is true");

        var higherOrder = Spec
            .Build(underlying)
            .AsAnySatisfied()
            .WhenTrueYield(eval => eval.CausalResults.GetValues().Select(m => m.Text))
            .WhenFalseYield(eval => eval.CausalResults.GetValues().Select(m => m.Text))
            .Create("all true");

        var result = higherOrder.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected);
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
        var underlying = Spec
            .Build((bool b) => b)
            .WhenTrue(new Metadata("is true"))
            .WhenFalse(new Metadata("is false"))
            .Create("is true");

        var higherOrder = Spec
            .Build(underlying)
            .AsAnySatisfied()
            .WhenTrue(eval => eval.CausalCount)
            .WhenFalse(eval => eval.CausalCount)
            .Create("all true");

        var result = higherOrder.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(true, true, "is true")]
    [InlineData(true, false, "is true", "is false")]
    [InlineData(false, true, "is true", "is false")]
    [InlineData(false, false, "is false")]
    public void Should_yield_underlying_metadata_using_results_property(
        bool modelA,
        bool modelB,
        params string[] expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool b) => b)
            .WhenTrue(new Metadata("is true"))
            .WhenFalse(new Metadata("is false"))
            .Create("is true");

        var higherOrder = Spec
            .Build(underlying)
            .AsAnySatisfied()
            .WhenTrueYield(eval => eval.Results.GetValues().Select(m => m.Text))
            .WhenFalseYield(eval => eval.Results.GetValues().Select(m => m.Text))
            .Create("all true");

        var result = higherOrder.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected);
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
        var underlying = Spec
            .Build((bool b) => b)
            .WhenTrue(new Metadata("is true"))
            .WhenFalse(new Metadata("is false"))
            .Create("is true");

        var higherOrder = Spec
            .Build(underlying)
            .AsAllSatisfied()
            .WhenTrueYield(eval => eval.TrueModels)
            .WhenFalseYield(eval => eval.TrueModels)
            .Create("all true");

        var result = higherOrder.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }
}
