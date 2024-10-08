namespace Motiv.Tests;

public class SpecDecoratorExplanationPropositionTests
{
    [InlineData(true, "true - A", "true + model - B", "true - C", "true + model - D", "true + model - E", "true + model - F", "true + model - G")]
    [InlineData(false, "false - A", "false - B", "false + model - C", "false + model - D", "false + model - E", "false + model - F", "false + model - G")]
    [Theory]
    public void Should_replace_the_assertions_with_new_assertions_for_policies(
        bool isSatisfied,
        params string[] expected)
    {
        // Arrange
        PolicyBase<string, string> underlying = Spec
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

        var fifthSpec = Spec
            .Build(underlying)
            .WhenTrue((model, _) => $"true + {model} - E")
            .WhenFalse(model => $"false + {model} - E")
            .Create("true + model - E");

        var sixthSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true + {model} - F")
            .WhenFalse((model, _) => $"false + {model} - F")
            .Create("true + model - F");

        var seventhSpec = Spec
            .Build(underlying)
            .WhenTrue((model, _) => $"true + {model} - G")
            .WhenFalse((model, _) => $"false + {model} - G")
            .Create("true + model -G");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [InlineData(true, "true - A", "true + model - B", "true - C", "true + model - D", "true + model - E", "true + model - F", "true + model - G")]
    [InlineData(false, "false - A", "false - B", "false + model - C", "false + model - D", "false + model - E", "false + model - F", "false + model - G")]
    [Theory]
    public void Should_replace_the_assertions_with_new_assertions_for_specs(
        bool isSatisfied,
        params string[] expected)
    {
        // Arrange
        SpecBase<string, string> underlying = Spec
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

        var fifthSpec = Spec
            .Build(underlying)
            .WhenTrue((model, _) => $"true + {model} - E")
            .WhenFalse(model => $"false + {model} - E")
            .Create("true + model - E");

        var sixthSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true + {model} - F")
            .WhenFalse((model, _) => $"false + {model} - F")
            .Create("true + model - F");

        var seventhSpec = Spec
            .Build(underlying)
            .WhenTrue((model, _) => $"true + {model} - G")
            .WhenFalse((model, _) => $"false + {model} - G")
            .Create("true + model -G");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [InlineAutoData(true, 1, 3, 5, 7, 9, 11, 13)]
    [InlineAutoData(false, 2, 4, 6, 8, 10, 12, 14)]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata_type_for_policies(
        bool isSatisfied,
        int expectedA,
        int expectedB,
        int expectedC,
        int expectedD,
        int expectedE,
        int expectedF,
        int expectedG,
        string trueReason,
        string falseReason)
    {
        // Arrange
        int[] expectation = [expectedA, expectedB, expectedC, expectedD, expectedE, expectedF, expectedG];
        PolicyBase<string, string> underlying = Spec
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

        var fifthSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 9)
            .WhenFalse(_ => 10)
            .Create("fifth spec");

        var sixthSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 11)
            .WhenFalse((_, _) => 12)
            .Create("sixth spec");

        var seventhSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 13)
            .WhenFalse((_, _) => 14)
            .Create("seventh spec");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.IsSatisfiedBy("model");

        result.GetRootAssertions().Should().BeEquivalentTo(result.Satisfied
            ? trueReason
            : falseReason);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expectation);
    }

    [InlineAutoData(true, 1, 3, 5, 7, 9, 11, 13)]
    [InlineAutoData(false, 2, 4, 6, 8, 10, 12, 14)]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata_type_for_specs(
        bool isSatisfied,
        int expectedA,
        int expectedB,
        int expectedC,
        int expectedD,
        int expectedE,
        int expectedF,
        int expectedG,
        string trueReason,
        string falseReason)
    {
        // Arrange
        int[] expectation = [expectedA, expectedB, expectedC, expectedD, expectedE, expectedF, expectedG];
        SpecBase<string, string> underlying = Spec
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

        var fifthSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 9)
            .WhenFalse(_ => 10)
            .Create("fifth spec");

        var sixthSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 11)
            .WhenFalse((_, _) => 12)
            .Create("sixth spec");

        var seventhSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 13)
            .WhenFalse((_, _) => 14)
            .Create("seventh spec");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.IsSatisfiedBy("model");

        result.GetRootAssertions().Should().BeEquivalentTo(result.Satisfied
            ? trueReason
            : falseReason);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expectation);
    }

    [Fact]
    public void Should_accept_assertion_when_true_for_policies()
    {
        // Arrange
        PolicyBase<string, string> underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo("true");
    }

    [Fact]
    public void Should_accept_assertion_when_true_for_specs()
    {
        // Arrange
        SpecBase<string, string> underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo("true");
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_true_for_policies(string model)
    {
        // Arrange
        PolicyBase<string, string> underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(m => m)
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(model);
    }



    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_true_for_specs(string model)
    {
        // Arrange
        SpecBase<string, string> underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(m => m)
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(model);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_true_for_policies(string model)
    {
        // Arrange
        PolicyBase<string, string> underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrueYield((trueModel, result) => result.Values.Select(meta => $"{trueModel} - {meta}"))
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying true");
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_true_for_specs(string model)
    {
        // Arrange
        SpecBase<string, string> underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrueYield((trueModel, result) => result.Values.Select(meta => $"{trueModel} - {meta}"))
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying true");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_true_for_policies(
            string model)
    {
        // Arrange
        PolicyBase<string, string> underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue((trueModel, result) => result.Values.Select(meta => $"{trueModel} - {meta}").First())
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying true");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_true_for_spes(
            string model)
    {
        // Arrange
        SpecBase<string, string> underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue((trueModel, result) => result.Values.Select(meta => $"{trueModel} - {meta}").First())
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying true");
    }

    [Fact]
    public void Should_accept_assertion_when_false_for_policies()
    {
        // Arrange
        PolicyBase<string, string> underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo("false");
    }

    [Fact]
    public void Should_accept_assertion_when_false_for_spes()
    {
        // Arrange
        SpecBase<string, string> underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo("false");
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_false_for_policies(string model)
    {
        // Arrange
        PolicyBase<string, string> underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse(m => m)
            .Create("is false");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(model);
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_false_for_specs(string model)
    {
        // Arrange
        SpecBase<string, string> underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse(m => m)
            .Create("is false");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(model);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_false_for_policies(string model)
    {
        // Arrange
        PolicyBase<string, string> underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalseYield((falseModel, result) => result.Values.Select(meta => $"{falseModel} - {meta}"))
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_false_for_specs(string model)
    {
        // Arrange
        SpecBase<string, string> underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalseYield((falseModel, result) => result.Values.Select(meta => $"{falseModel} - {meta}"))
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_false_for_policies(
            string model)
    {
        // Arrange
        PolicyBase<string, string> underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse((falseResult, result) => result.Values.Select(meta => $"{falseResult} - {meta}").First())
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_false_for_specs(
            string model)
    {
        // Arrange
        SpecBase<string, string> underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse((falseResult, result) => result.Values.Select(meta => $"{falseResult} - {meta}").First())
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_evaluate_minimally_defined_policy(bool model)
    {
        // Arrange
        PolicyBase<bool, string> underlying = Spec
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
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_evaluate_minimally_defined_spec(bool model)
    {
        // Arrange
        SpecBase<bool, string> underlying = Spec
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
    public void Should_generate_multi_metadata_description_for_policies()
    {
        // Arrange
        PolicyBase<string, string> left = Spec
            .Build<string>(_ => true)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create("left");

        PolicyBase<string, string> right = Spec
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

    [Fact]
    public void Should_generate_multi_metadata_description_for_specs()
    {
        // Arrange
        SpecBase<string,string> left = Spec
            .Build<string>(_ => true)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create("left");

        SpecBase<string,string> right = Spec
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
    public void Should_create_a_boolean_result_that_contains_the_underlying_result_for_policies(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
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
    public void Should_create_a_boolean_result_that_contains_the_underlying_result_for_specs(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
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
    public void Should_create_a_boolean_result_that_contains_the_underlying_with_metadata_result_for_polcies(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model);

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.UnderlyingWithValues;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }


    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_with_metadata_result_for_specs(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model);

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.UnderlyingWithValues;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_have_a_description_that_has_a_causal_count_value_of_1_for_policies(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
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
    public void Should_have_a_description_that_has_a_causal_count_value_of_1_for_specs(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
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
    public void Should_create_a_boolean_result_that_contains_the_causal_result_for_policies(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
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
    public void Should_create_a_boolean_result_that_contains_the_causal_result_for_specs(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
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
    public void Should_create_a_boolean_result_that_contains_the_causal_with_metadata_result_for_policies(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model).CausesWithValues;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.CausesWithValues;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_with_metadata_result_for_specs(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model).CausesWithValues;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.CausesWithValues;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true, "True: left true", "True: right false")]
    [InlineAutoData(false, "False: left false", "False: right true")]
    public void Should_permit_metadata_generated_using_underlying_results_for_policies(bool model, string expectedLeft, string expectedRight)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create();

        PolicyBase<bool,string> right = Spec
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
    [InlineAutoData(true, "True: left true", "True: right false")]
    [InlineAutoData(false, "False: left false", "False: right true")]
    public void Should_permit_metadata_generated_using_underlying_results_for_specs(bool model, string expectedLeft, string expectedRight)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create();

        SpecBase<bool,string> right = Spec
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
    public void Should_harvest_propositionStatement_from_assertion_for_policies(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        PolicyBase<bool,string> underlying =
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
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_harvest_propositionStatement_from_assertion_for_specs(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        SpecBase<bool,string> underlying =
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
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_not_use_the_propositional_statement_in_the_reason_for_policies(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        PolicyBase<bool,string> underlying =
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
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_use_assertions_in_the_reason_when_propositional_statements_are_used(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        SpecBase<bool,string> underlying =
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
    public void Should_use_the_propositional_statement_in_the_reason_when_using_multiple_assertions_for_policies(
            bool model,
            string expectedReason)
    {
        PolicyBase<bool, string> underlying =
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
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_using_multiple_assertions_for_specs(
        bool model,
        string expectedReason)
    {
        SpecBase<bool,string> underlying =
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
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_for_policies(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        PolicyBase<bool,string> underlying =
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
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_for_specs(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        SpecBase<bool,string> underlying =
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
        Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_when_using_multiple_assertions_for_policies(
            bool model,
            string expectedReason)
    {
        PolicyBase<bool,string> underlying =
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
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void
        Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_when_using_multiple_assertions_for_specs(
            bool model,
            string expectedReason)
    {
        SpecBase<bool,string> underlying =
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
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_for_policies(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        PolicyBase<bool,string> underlying =
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
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_for_specs(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));

        SpecBase<bool,string> underlying =
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
        Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_when_using_multiple_assertions_for_policies(
            bool model,
            string expectedReason)
    {
        PolicyBase<bool, string> underlying =
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
    public void
        Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_when_using_multiple_assertions_for_specs(
            bool model,
            string expectedReason)
    {
        SpecBase<bool,string> underlying =
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
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection_for_polcies(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        PolicyBase<bool,string> underlying =
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

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection_for_specs(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        SpecBase<bool,string> underlying =
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
