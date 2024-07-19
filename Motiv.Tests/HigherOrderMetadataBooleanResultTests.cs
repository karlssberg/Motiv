namespace Motiv.Tests;

public class HigherOrderMetadataBooleanResultTests
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
        SpecBase<int, string> underlying =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue(i => $"{i} is even")
                .WhenFalse(i => $"{i} is odd")
                .Create("is even spec");

        var spec =
            Spec.Build((int m) => underlying.IsSatisfiedBy(m))
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
        SpecBase<int, string> underlying =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue("is even")
                .WhenFalse("is odd")
                .Create("is even spec");

        var spec =
            Spec.Build((int m) => underlying.IsSatisfiedBy(m))
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
        SpecBase<bool, string> underlying =
            Spec.Build((bool b) => b)
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create();

        var firstSpec =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
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
    [InlineData(true, true, true, "is true")]
    [InlineData(true, true, false, "is false")]
    [InlineData(true, false, true, "is false")]
    [InlineData(true, false, false, "is false")]
    [InlineData(false, true, true, "is false")]
    [InlineData(false, true, false, "is false")]
    [InlineData(false, false, true, "is false")]
    [InlineData(false, false, false, "is false")]
    public void Should_yield_the_most_deeply_nested_reason_when_requested(bool first, bool second, bool third, string expected)
    {
        // Arrange
        SpecBase<bool, string> underlying =
            Spec.Build<bool>(b => b)
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create();

        var firstSpec =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("all even");

        var secondSpec =
            Spec.Build(firstSpec)
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("all even");

        var spec =
            Spec.Build(secondSpec)
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("all even");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.GetRootAssertions();

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(2, 4, 6, 8, true)]
    [InlineData(2, 4, 6, 9, false)]
    [InlineData(1, 4, 6, 9, false)]
    [InlineData(1, 3, 6, 9, false)]
    [InlineData(1, 3, 5, 9, false)]
    public void Should_satisfy_regular_true_assertion_yield_to_be_used_with_a_higher_order_yield_of_false_assertions(
        int first,
        int second,
        int third,
        int fourth,
        bool expected)
    {
        // Arrange
        SpecBase<int, MyMetadata> underlying =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("is even spec");

        var spec =
            Spec.Build((int m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("all even");

        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }


    [Theory]
    [InlineData(true, MyMetadata.IsTrue)]
    [InlineData(false, MyMetadata.IsFalse)]
    public void Should_harvest_propositionStatement_from_assertion(
        bool model,
        MyMetadata expected)
    {
        // Arrange

        SpecBase<bool, string> underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("true assertion");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("all true");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy([model]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineData(true, MyMetadata.IsTrue)]
    [InlineData(false, MyMetadata.IsFalse)]
    public void Should_use_the_propositional_statement_in_the_reason(
        bool model,
        MyMetadata expected)
    {
        // Arrange
        SpecBase<bool, string> underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsCallback =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsCallbackThatReturnsACollection =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalseYield(_ => MyMetadata.IsFalse.ToEnumerable())
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollectionWithImpliedName =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(MyMetadata.IsTrue)
                .WhenFalseYield(_ => MyMetadata.IsFalse.ToEnumerable())
                .Create("true assertion");

        var spec = withFalseAsScalar &
                   withFalseAsCallback &
                   withFalseAsCallbackThatReturnsACollection &
                   withFalseAsTwoParameterCallbackThatReturnsACollectionWithImpliedName;

        var result = spec.IsSatisfiedBy([model]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineData(true, MyMetadata.IsTrue)]
    [InlineData(false, MyMetadata.IsFalse)]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        MyMetadata expected)
    {
        // Arrange
        SpecBase<bool, string> underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalseYield(_ => MyMetadata.IsFalse.ToEnumerable())
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;

        var result = spec.IsSatisfiedBy([model]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineData(true, MyMetadata.IsTrue)]
    [InlineData(false, MyMetadata.IsFalse)]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        MyMetadata expected)
    {
        // Arrange
        SpecBase<bool, string> underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalse(MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrue(_ => MyMetadata.IsTrue)
                .WhenFalseYield(_ => MyMetadata.IsFalse.ToEnumerable())
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;

        var result = spec.IsSatisfiedBy([model]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineData(true, MyMetadata.IsTrue)]
    [InlineData(false, MyMetadata.IsFalse)]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection(
        bool model,
        MyMetadata expected)
    {
        // Arrange
        SpecBase<bool, string> underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrueYield(_ => MyMetadata.IsTrue.ToEnumerable())
                .WhenFalse(MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsCallback =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrueYield(_ => MyMetadata.IsTrue.ToEnumerable())
                .WhenFalse(_ => MyMetadata.IsFalse)
                .Create("propositional statement");

        var withFalseAsCallbackThatReturnsACollection =
            Spec.Build((bool m) => underlying.IsSatisfiedBy(m))
                .AsAllSatisfied()
                .WhenTrueYield(_ => MyMetadata.IsTrue.ToEnumerable())
                .WhenFalseYield(_ => MyMetadata.IsFalse.ToEnumerable())
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsCallback &
                   withFalseAsCallbackThatReturnsACollection;

        var result = spec.IsSatisfiedBy([model]);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }
}
