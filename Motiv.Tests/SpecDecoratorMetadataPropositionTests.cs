namespace Motiv.Tests;

public class SpecDecoratorMetadataPropositionTests
{
    public enum Metadata
    {
        True,
        False
    }
    
    [Theory]
    [InlineData(true, "is first true", "is second true", "is third true", "is fourth true")]
    [InlineData(false, "!is first true", "!is second true", "!is third true", "!is fourth true")]
    public void Should_replace_the_assertion_with_new_assertion(
        bool isSatisfied,
        params string[] expected)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue(100)
            .WhenFalse(-100)
            .Create("is underlying true");

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is first true");

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 300)
            .WhenFalse(-300)
            .Create("is second true");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue(400)
            .WhenFalse(_ => -400)
            .Create("is third true");

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 500)
            .WhenFalse(_ => -500)
            .Create("is fourth true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [InlineData(true, 200, 300, 400, 500)]
    [InlineData(false, -200, -300, -400, -500)]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata(
        bool isSatisfied,
        int expectedA,
        int expectedB,
        int expectedC,
        int expectedD)
    {
        // Arrange
        int[] expectation = [expectedA, expectedB, expectedC, expectedD];
        var underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue(100)
            .WhenFalse(-100)
            .Create("is underlying true");

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is first true");

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 300)
            .WhenFalse(-300)
            .Create("is second true");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue(400)
            .WhenFalse(_ => -400)
            .Create("is third true");

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 500)
            .WhenFalse(_ => -500)
            .Create("is fourth true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo(expectation);
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
            .WhenTrue((_, _) => 7)
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
            .WhenTrue(Metadata.True)
            .WhenFalse(Metadata.False)
            .Create("is on");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([Metadata.True]);
    }

    [Theory]
    [InlineAutoData(true,  Metadata.True)]
    [InlineAutoData(false, Metadata.False)]
    public void Should_accept_single_parameter_assertion_factory_when_true_for_spec_decorators(bool model, Metadata expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(_ => Metadata.True)
            .WhenFalse(_ => Metadata.False)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo(expected.ToEnumerable());
    }

    [Theory]
    [InlineAutoData(true,  Metadata.True)]
    [InlineAutoData(false, Metadata.False)]
    public void Should__double_parameter_assertion_factory_when_true_for_spec_decorators(bool model, Metadata expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue(Metadata.True)
            .WhenFalse(Metadata.False)
            .Create("is on");

        var spec = Spec
            .Build(underlying)
            .WhenTrue((m, r) => (Model: m, r.Metadata))
            .WhenFalse((m, r) => (Model: m, r.Metadata))
            .Create("is outer on");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([(model, expected.ToEnumerable())]);
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

    [Theory]
    [AutoData]
    public void Should_accept_metadata_when_true_for_spec_decorators(
        Guid isTrueMetadata,
        Guid isFalseMetadata)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(isTrueMetadata)
            .WhenFalse(isFalseMetadata)
            .Create("is true");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([isTrueMetadata]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_metadata_factory_when_true_for_spec_decorators(
        Guid guidModel,
        Guid isFalseMetadata)
    {
        // Arrange
        var underlying = Spec
            .Build<Guid>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(model => model)
            .WhenFalse(isFalseMetadata)
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([guidModel]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_metadata_factory_when_true_for_spec_decorators(
        Guid underlyingTrueGuid,
        Guid underlyingFalseGuid,
        Guid guidModel,
        Guid isFalseMetadata)
    {
        // Arrange
        var underlying = Spec
            .Build<Guid>(_ => true)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue((model, result) => result.Metadata.Select(meta => (model, meta)).First())
            .WhenFalse(model => (model, isFalseMetadata))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingTrueGuid)]);
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_metadata_when_true_for_spec_decorators(
            Guid underlyingTrueGuid,
            Guid underlyingFalseGuid,
            Guid guidModel,
            Guid isFalseMetadata)
    {
        // Arrange
        var underlying = Spec
            .Build<Guid>(_ => true)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue((model, result) => result.Metadata.Select(meta => (model, meta)))
            .WhenFalse(model => (model, isFalseMetadata))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingTrueGuid)]);
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
            .WhenFalse((falseModel, result) => result.Metadata.Select(meta => $"{falseModel} - {meta}"))
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
    [AutoData]
    public void Should_accept_metadata_when_false_for_spec_decorators(
        Guid isTrueMetadata,
        Guid isFalseMetadata)
    {
        // Arrange
        var underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(isTrueMetadata)
            .WhenFalse(isFalseMetadata)
            .Create("is true");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([isFalseMetadata]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_metadata_factory_when_false_for_spec_decorators(
        Guid guidModel,
        Guid isTrueMetadata)
    {
        // Arrange
        var underlying = Spec
            .Build<Guid>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(isTrueMetadata)
            .WhenFalse(model => model)
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([guidModel]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_metadata_factory_when_false_for_spec_decorators(
        Guid underlyingTrueGuid,
        Guid underlyingFalseGuid,
        Guid guidModel,
        Guid isTrueMetadata)
    {
        // Arrange
        var underlying = Spec
            .Build<Guid>(_ => false)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(model => (model, isTrueMetadata))
            .WhenFalse((model, result) => result.Metadata.Select(meta => (model, meta)).First())
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingFalseGuid)]);
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_metadata_when_false_for_spec_decorators(
            Guid underlyingTrueGuid,
            Guid underlyingFalseGuid,
            Guid guidModel,
            Guid isFalseMetadata)
    {
        // Arrange
        var underlying = Spec
            .Build<Guid>(_ => false)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(model => (model, isFalseMetadata))
            .WhenFalse((model, result) => result.Metadata.Select(meta => (model, meta)))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingFalseGuid)]);
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
    [InlineAutoData(false, "!is true")]
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
            .WhenTrue((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .WhenFalse((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .Create("top-level proposition");
        
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expectedLeft, expectedRight);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason(
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
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue(Metadata.True)
                .WhenFalse((_, _) => [Metadata.False])
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
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
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
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse((_, _) => [Metadata.False])
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
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback(
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
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse((_, _) => [Metadata.False])
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
    [InlineData(false, "!propositional statement")]
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
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse((_, _) => [Metadata.False])
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