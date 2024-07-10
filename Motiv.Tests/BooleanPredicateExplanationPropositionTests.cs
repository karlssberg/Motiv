namespace Motiv.Tests;

public class BooleanPredicateExplanationPropositionTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_be_satisfied_boolean_predicate(
        bool model,
        bool other,
        bool expected)
    {
        // Arrange
        var firstSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_be_satisfied_boolean_predicate_when_yielding_multiple_assertions(
        bool model,
        bool other,
        bool expected)
    {
        // Arrange
        var firstSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrueYield(m => ["first is true"])
            .WhenFalse("first is false")
            .Create("first true");

        var secondSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue("second is true")
            .WhenFalseYield(m => ["second is false"])
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrueYield(m => ["first is true"])
            .WhenFalseYield(m => ["second is false"])
            .Create("is third true");

        var spec = firstSpec | secondSpec | thirdSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineData(true,  "first is true", "second is true", "third is true", "fourth is true")]
    [InlineData(false,  "first is false", "second is false", "third is false", "fourth is false")]
    public void Should_replace_the_assertion_with_new_assertion(
        bool model,
        params string[] expectedAssertions)
    {
        // Arrange
        var firstSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expectedAssertions);
    }

    [Theory]
    [InlineData(true,  "first is true", "second is true", "third is true")]
    [InlineData(false, "first is false", "second is false", "third is false")]
    public void Should_replace_the_assertion_with_new_assertion_when_yielding_multiple_assertions(
        bool model,
        params string[] expectedAssertions)
    {
        // Arrange
        var firstSpec = Spec
            .Build((bool m) => m)
            .WhenTrueYield(m => ["first is true"])
            .WhenFalse("first is false")
            .Create("first true");

        var secondSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("second is true")
            .WhenFalseYield(m => ["second is false"])
            .Create("second true");

        var thirdSpec = Spec
            .Build((bool m) => m)
            .WhenTrueYield(m => ["third is true"])
            .WhenFalseYield(m => ["third is false"])
            .Create("third true");

        var spec = firstSpec | secondSpec | thirdSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expectedAssertions);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Should_a_reason_from_assertions(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue("true")
                .WhenFalse("false")
                .Create();

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue("true")
                .WhenFalse(_ => "false")
                .Create();

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_use_the_propositional_statement_in_the_reason(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_more_than_one_assertion_possible(
        bool model,
        string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .WhenTrue("true assertion")
                .WhenFalseYield(m => ["false assertion"])
                .Create("propositional statement");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "true assertion")]
    [InlineData(false, "¬true assertion")]
    public void Should_use_the_implicit_propositional_statement_in_the_reason_when_more_than_one_assertion_possible(
        bool model,
        string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .WhenTrue("true assertion")
                .WhenFalseYield(m => ["false assertion"])
                .Create();

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_when_multiple_assertion_possible(
        bool model,
        string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => "true assertion")
                .WhenFalseYield(m => ["false assertion"])
                .Create("propositional statement");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(m => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(m => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_when_multiple_assertion_possible(
        bool model,
        string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .WhenTrue(m => "true assertion")
                .WhenFalseYield(m => ["false assertion"])
                .Create("propositional statement");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrueYield(m => ["true assertion"])
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrueYield(m => ["true assertion"])
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);

    }

    [Fact]
    public void Should_describe_a_boolean_result_spec()
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create("is model true");

        // Act
        var act = spec.Description.Statement;

        // Assert
        act.Should().Be("is model true");
    }
}
