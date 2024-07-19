namespace Motiv.Tests;

public class HigherOrderMetadataPredicateTests
{
    public enum MyMetadata
    {
        Unknown,
        IsTrue,
        IsFalse
    }

    [Theory]
    [InlineData(1, 3, 5, 7, "¬is a pair of even numbers")]
    [InlineData(1, 3, 5, 8, "¬is a pair of even numbers")]
    [InlineData(1, 3, 6, 8, "is a pair of even numbers")]
    [InlineData(1, 3, 5, 9, "¬is a pair of even numbers")]
    public void Should_supplant_metadata_from_a_higher_order_spec(int first, int second, int third, int fourth, string expected)
    {
        // Arrange
        var spec =
            Spec.Build((int i) => i % 2 == 0)
                .AsNSatisfied(2)
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("is a pair of even numbers");

        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Explanation.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Should_preserve_the_description_of_the_underlying_()
    {
        // Arrange
        var spec =
            Spec.Build((int i) => i % 2 == 0)
                .AsNSatisfied(2)
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("is a pair of even numbers");

        // Act
        var act = spec.Statement;

        // Assert
        act.Should().Be("is a pair of even numbers");
    }

    [Theory]
    [InlineData(true, true, true, "third all true")]
    [InlineData(true, true, false, "¬third all true")]
    [InlineData(true, false, true, "¬third all true")]
    [InlineData(true, false, false, "¬third all true")]
    [InlineData(false, true, true, "¬third all true")]
    [InlineData(false, true, false, "¬third all true")]
    [InlineData(false, false, true, "¬third all true")]
    [InlineData(false, false, false, "¬third all true")]
    public void Should_only_yield_the_most_recent_when_multiple_yields_are_chained(bool first, bool second, bool third, string expected)
    {
        // Arrange

        var firstSpec =
            Spec.Build((bool b) => b)
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("first all true");

        var secondSpec =
            Spec.Build(firstSpec)
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("second all true");

        var spec =
            Spec.Build(secondSpec)
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("third all true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Explanation.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(true, true, true, "first true")]
    [InlineData(true, true, false, "¬first true")]
    [InlineData(true, false, true, "¬first true")]
    [InlineData(true, false, false, "¬first true")]
    [InlineData(false, true, true, "¬first true")]
    [InlineData(false, true, false, "¬first true")]
    [InlineData(false, false, true, "¬first true")]
    [InlineData(false, false, false, "¬first true")]
    public void Should_yield_the_most_deeply_nested_reason_when_requested(bool first, bool second, bool third, string expected)
    {
        // Arrange
        var firstSpec =
            Spec.Build((bool b) => b)
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("first true");

        var secondSpec =
            Spec.Build(firstSpec)
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("second true");

        var spec =
            Spec.Build(secondSpec)
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("third true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.RootAssertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_harvest_propositionStatement_from_assertion(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy([model]);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsCallback =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsCallbackThatReturnsACollection =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalseYield(_ => [MyMetadata.IsFalse])
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollectionWithImpliedName =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalseYield(_ => [MyMetadata.IsFalse])
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsCallback &
                   withFalseAsCallbackThatReturnsACollection &
                   withFalseAsTwoParameterCallbackThatReturnsACollectionWithImpliedName;

        var result = spec.IsSatisfiedBy([model]);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool m) => m)
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalseYield(_ => [MyMetadata.IsFalse])
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;

        var result = spec.IsSatisfiedBy([model]);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }
}
