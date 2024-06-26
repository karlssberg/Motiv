namespace Motiv.Tests;

public class SpecDecoratorExplanationPropositionTests
{
    [InlineData(true, "true - A", "true + model - B", "true - C", "true + model - D")]
    [InlineData(false, "false - A", "false - B", "false + model - C", "false + model - D")]
    [Theory]
    public void Should_replace_the_assertions_with_new_assertions(
        bool isSatisfied,
        params string[] expected)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue("true - A")
            .WhenFalse("false - A")
            .Create();

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true + {model} - B")
            .WhenFalse("false - B")
            .Create("is second true");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue("true - C")
            .WhenFalse(model => $"false + {model} - C")
            .Create();

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true + {model} - D")
            .WhenFalse(model => $"false + {model} - D")
            .Create("true + model - D");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [InlineData(true, "true - A", "true + model - B", "true - C", "true + model - D")]
    [InlineData(false, "false - A", "false - B", "false + model - C", "false + model - D")]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata(
        bool isSatisfied,
        params string[] expected)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue("true - A")
            .WhenFalse("false - A")
            .Create();

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true + {model} - B")
            .WhenFalse("false - B")
            .Create("is second true");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue("true - C")
            .WhenFalse(model => $"false + {model} - C")
            .Create();

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true + {model} - D")
            .WhenFalse(model => $"false + {model} - D")
            .Create("true + model - D");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [InlineAutoData(true, 1, 3, 5, 7)]
    [InlineAutoData(false, 2, 4, 6, 8)]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata_type(
        bool isSatisfied,
        int expectedA,
        int expectedB,
        int expectedC,
        int expectedD,
        string trueReason,
        string falseReason)
    {
        // Arrange
        int[] expectation = [expectedA, expectedB, expectedC, expectedD];
        var underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue(trueReason)
            .WhenFalse(falseReason)
            .Create();

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue(1)
            .WhenFalse(2)
            .Create("first spec");

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 3)
            .WhenFalse(4)
            .Create("second spec");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue(5)
            .WhenFalse(_ => 6)
            .Create("third spec");

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 7)
            .WhenFalse(_ => 8)
            .Create("fourth spec");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy("model");

        result.GetRootAssertions().Should().BeEquivalentTo(result.Satisfied
            ? trueReason
            : falseReason);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo(expectation);
    }

    [Fact]
    public void Should_accept_assertion_when_true_for_spec_decorators()
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo("true");
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_true_for_spec_decorators(string model)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(m => m)
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo(model);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_true_for_spec_decorators(string model)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrueYield((trueModel, result) => result.Metadata.Select(meta => $"{trueModel} - {meta}"))
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying true");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_true_for_spec_decorators(
            string model)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue((trueModel, result) => result.Metadata.Select(meta => $"{trueModel} - {meta}").First())
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying true");
    }


    [Fact]
    public void Should_accept_assertion_when_false_for_spec_decorators()
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo("false");
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_false_for_spec_decorators(string model)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse(m => m)
            .Create("is false");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo(model);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_false_for_spec_decorators(string model)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalseYield((falseModel, result) => result.Metadata.Select(meta => $"{falseModel} - {meta}"))
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_false_for_spec_decorators(
            string model)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse((falseResult, result) => result.Metadata.Select(meta => $"{falseResult} - {meta}").First())
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_evaluate_minimally_defined_spec(bool model)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(model);
    }

    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "¬is true")]
    public void Should_provide_a_reason_for_minimally_defined_spec(bool model, string expectedReason)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Fact]
    public void Should_generate_multi_metadata_description()
    {
        // Arrange
        var left = Spec
            .Build<string>(_ => true)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create("left");

        var right = Spec
            .Build<string>(_ => true)
            .WhenTrue("right true")
            .WhenFalse("right false")
            .Create("right");

        var underlying = left | right;

        var spec = Spec
            .Build(underlying)
            .Create("top-level proposition");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Description.Reason;

        // Assert
        act.Should().NotContainAny("left true", "left false", "right true", "right false");

    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_result(bool model)
    {
        // Arrange
        var left = Spec
            .Build((bool m) => m)
            .Create("left");

        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model);

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Underlying;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_with_metadata_result(bool model)
    {
        // Arrange
        var left = Spec
            .Build((bool m) => m)
            .Create("left");

        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model);

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.UnderlyingWithMetadata;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_have_a_description_that_has_a_causal_count_value_of_1(bool model)
    {
        // Arrange
        var left = Spec
            .Build((bool m) => m)
            .Create("left");

        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Description.CausalOperandCount;

        // Assert
        act.Should().Be(1);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_result(bool model)
    {
        // Arrange
        var left = Spec
            .Build((bool m) => m)
            .Create("left");

        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model).Causes;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Causes;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_with_metadata_result(bool model)
    {
        // Arrange
        var left = Spec
            .Build((bool m) => m)
            .Create("left");

        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model).CausesWithMetadata;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.CausesWithMetadata;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true, "True: left true", "True: right false")]
    [InlineAutoData(false, "False: left false", "False: right true")]
    public void Should_permit_metadata_generated_using_underlying_results(bool model, string expectedLeft, string expectedRight)
    {
        // Arrange
        var left = Spec
            .Build((bool m) => m)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create();

        var right = Spec
            .Build((bool m) => !m)
            .WhenTrue("right true")
            .WhenFalse("right false")
            .Create();

        var underlying = left ^ !right;

        var spec = Spec
            .Build(underlying)
            .WhenTrueYield((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .WhenFalseYield((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .Create("top-level proposition");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expectedLeft, expectedRight);
    }

    [Theory]
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_harvest_propositionStatement_from_assertion(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create();

        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create();

        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create();

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.IsSatisfiedBy(model);

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
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_using_multiple_assertions(
            bool model,
            string expectedReason)
    {
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
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
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrue(_ => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(_ => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void
        Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_when_using_multiple_assertions(
            bool model,
            string expectedReason)
    {
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build(underlying)
                .WhenTrue(_ => "true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
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
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void
        Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_when_using_multiple_assertions(
            bool model,
            string expectedReason)
    {
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build(underlying)
                .WhenTrue((_, _) => "true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
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
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }
}
