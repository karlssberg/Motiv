using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class SpecDecoratorExplanationPropositionTests
{
    [InlineData(true, "true - A", "true + model - B", "true - C", "true + model - D")]
    [InlineData(false, "false - A", "false - B", "false + model - C", "false + model - D")]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata(
        bool isSatisfied,
        params string[] expected)
    {
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

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Assertions.Should().BeEquivalentTo(expected);
        act.MetadataTree.Should().BeEquivalentTo(expected);
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
            .WhenTrue(model => 3)
            .WhenFalse(4)
            .Create("second spec");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue(5)
            .WhenFalse(model => 6)
            .Create("third spec");

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(model => 7)
            .WhenFalse(model => 8)
            .Create("fourth spec");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.GetRootAssertions().Should().BeEquivalentTo(act.Satisfied
            ? trueReason
            : falseReason);

        act.MetadataTree.Should().BeEquivalentTo(expectation);
    }

    [Fact]
    public void Should_accept_assertion_when_true_for_spec_decorators()
    {
        var underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var act = spec.IsSatisfiedBy("model");

        act.Metadata.Should().BeEquivalentTo("true");
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_true_for_spec_decorators(string model)
    {
        var underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(m => m)
            .WhenFalse("false")
            .Create("is true");

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo(model);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_true_for_spec_decorators(string model)
    {
        var underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue((trueModel, result) => result.Metadata.Select(meta => $"{trueModel} - {meta}"))
            .WhenFalse("false")
            .Create("is true");

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo($"{model} - underlying true");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_true_for_spec_decorators(
            string model)
    {
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

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo($"{model} - underlying true");
    }


    [Fact]
    public void Should_accept_assertion_when_false_for_spec_decorators()
    {
        var underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var act = spec.IsSatisfiedBy("model");

        act.Metadata.Should().BeEquivalentTo("false");
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_false_for_spec_decorators(string model)
    {
        var underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse(m => m)
            .Create("is false");

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo(model);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_false_for_spec_decorators(string model)
    {
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

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_false_for_spec_decorators(
            string model)
    {
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

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "!is true")]
    public void Should_accept_minimally_defined_spec(bool model, string expectedReason)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .Create("is true");

        var act = spec.IsSatisfiedBy(model);

        act.Satisfied.Should().Be(model);
        act.Reason.Should().Be(expectedReason);
    }

    [Fact]
    public void Should_generate_multi_metadata_description()
    {
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
        
        var act = spec.IsSatisfiedBy("model");
        
        act.Description.Reason.Should().NotContainAny("left true", "left false", "right true", "right false");
        
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_result(bool model)
    {
        var left = Spec 
            .Build((bool m) => m)
            .Create("left");
        
        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;
        
        var expected = orSpec.IsSatisfiedBy(model);
        
        var sut = Spec
            .Build(orSpec)
            .Create("composite");
        
        var act = sut.IsSatisfiedBy(model);

        act.Underlying.Should().BeEquivalentTo([expected]);
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_with_metadata_result(bool model)
    {
        var left = Spec 
            .Build((bool m) => m)
            .Create("left");
        
        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;
        
        var expected = orSpec.IsSatisfiedBy(model);
        
        var sut = Spec
            .Build(orSpec)
            .Create("composite");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.UnderlyingWithMetadata.Should().BeEquivalentTo([expected]);
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_have_a_description_that_has_a_causal_count_value_of_1(bool model)
    {
        var left = Spec 
            .Build((bool m) => m)
            .Create("left");
        
        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;
        
        var sut = Spec
            .Build(orSpec)
            .Create("composite");
        
        var act = sut.IsSatisfiedBy(model);

        act.Description.CausalOperandCount.Should().Be(1);
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_result(bool model)
    {
        var left = Spec 
            .Build((bool m) => m)
            .Create("left");
        
        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;
        
        var expected = orSpec.IsSatisfiedBy(model).Causes;
        
        var sut = Spec
            .Build(orSpec)
            .Create("composite");
        
        var act = sut.IsSatisfiedBy(model);

        act.Causes.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_with_metadata_result(bool model)
    {
        var left = Spec 
            .Build((bool m) => m)
            .Create("left");
        
        var right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;
        
        var expected = orSpec.IsSatisfiedBy(model).CausesWithMetadata;
        
        var sut = Spec
            .Build(orSpec)
            .Create("composite");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.CausesWithMetadata.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true, "True: left true", "True: right false")]
    [InlineAutoData(false, "False: left false", "False: right true")]
    public void Should_permit_metadata_generated_using_underlying_results(bool model, string expectedLeft, string expectedRight)
    {
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
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Assertions.Should().BeEquivalentTo(expectedLeft, expectedRight);
    }
    
    [Theory]
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_harvest_propositionStatement_from_assertion(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
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
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
    
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
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
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse((_, _) => ["false assertion"])
                .Create("propositional statement");
        
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
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
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue(_ => "true assertion")
                .WhenFalse((_, _) => ["false assertion"])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
    
    
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
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
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse((_, _) => ["false assertion"])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
    
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse((_, _) => ["false assertion"])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
}