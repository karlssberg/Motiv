﻿using AutoFixture.Xunit2;
using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class CompositeExplanationSpecTests
{
    [InlineAutoData(true, "true after - A", "true after + model - B", "true after - C", "true after + model - D")]
    [InlineAutoData(false, "false after - A", "false after - B", "false after + model - C", "false after + model - D")]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata(
        bool isSatisfied,
        string expectedA,
        string expectedB,
        string expectedC,
        string expectedD)
    {
        string[] expectation = [expectedA, expectedB, expectedC, expectedD];
        var underlying = Spec
            .Build<string>(m => isSatisfied)
            .WhenTrue("true before")
            .WhenFalse("false before")
            .CreateSpec();

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue("true after - A")
            .WhenFalse("false after - A")
            .CreateSpec();

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true after + {model} - B")
            .WhenFalse("false after - B")
            .CreateSpec("is second true");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue("true after - C")
            .WhenFalse(model => $"false after + {model} - C")
            .CreateSpec();

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true after + {model} - D")
            .WhenFalse(model => $"false after + {model} - D")
            .CreateSpec("true after + model - D");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Explanation.Assertions.Should().BeEquivalentTo(expectation);
        act.MetadataTree.Should().BeEquivalentTo(expectation);
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
            .Build<string>(m => isSatisfied)
            .WhenTrue(trueReason)
            .WhenFalse(falseReason)
            .CreateSpec();

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue(1)
            .WhenFalse(2)
            .CreateSpec("first spec");

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(model => 3)
            .WhenFalse(4)
            .CreateSpec("second spec");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue(5)
            .WhenFalse(model => 6)
            .CreateSpec("third spec");

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(model => 7)
            .WhenFalse(model => 8)
            .CreateSpec("fourth spec");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.GetRootAssertions().Should().BeEquivalentTo(act.Satisfied
            ? trueReason
            : falseReason);

        act.MetadataTree.Should().BeEquivalentTo(expectation);
    }

    [Fact]
    public void Should_accept_assertion_when_true_for_composite_specs()
    {
        var underlying = Spec
            .Build<string>(_ => true)
            .CreateSpec("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .CreateSpec();

        var act = spec.IsSatisfiedBy("model");

        act.Metadata.Should().BeEquivalentTo("true");
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_true_for_composite_specs(string model)
    {
        var underlying = Spec
            .Build<string>(_ => true)
            .CreateSpec("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(m => m)
            .WhenFalse("false")
            .CreateSpec("is true");

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo(model);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_true_for_composite_specs(string model)
    {
        var underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .CreateSpec();

        var spec = Spec
            .Build(underlying)
            .WhenTrue((trueModel, result) => result.Metadata.Select(meta => $"{trueModel} - {meta}"))
            .WhenFalse("false")
            .CreateSpec("is true");

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo($"{model} - underlying true");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_true_for_composite_specs(
            string model)
    {
        var underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .CreateSpec();

        var spec = Spec
            .Build(underlying)
            .WhenTrue((trueModel, result) => result.Metadata.Select(meta => $"{trueModel} - {meta}").First())
            .WhenFalse("false")
            .CreateSpec("is true");

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo($"{model} - underlying true");
    }


    [Fact]
    public void Should_accept_assertion_when_false_for_composite_specs()
    {
        var underlying = Spec
            .Build<string>(_ => false)
            .CreateSpec("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .CreateSpec();

        var act = spec.IsSatisfiedBy("model");

        act.Metadata.Should().BeEquivalentTo("false");
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_assertion_factory_when_false_for_composite_specs(string model)
    {
        var underlying = Spec
            .Build<string>(_ => false)
            .CreateSpec("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse(m => m)
            .CreateSpec("is false");

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo(model);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_assertion_factory_when_false_for_composite_specs(string model)
    {
        var underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .CreateSpec();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse((falseModel, result) => result.Metadata.Select(meta => $"{falseModel} - {meta}"))
            .CreateSpec("is true");

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_assertion_factory_that_returns_a_collection_of_assertions_when_false_for_composite_specs(
            string model)
    {
        var underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .CreateSpec();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse((falseResult, result) => result.Metadata.Select(meta => $"{falseResult} - {meta}").First())
            .CreateSpec("is true");

        var act = spec.IsSatisfiedBy(model);

        act.Metadata.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "!is true")]
    public void Should_accept_minimally_defined_spec(bool model, string expectedReason)
    {
        var underlying = Spec
            .Build<bool>(m => m)
            .CreateSpec("is underlying true");

        var spec = Spec
            .Build(underlying)
            .CreateSpec("is true");

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
            .CreateSpec("left");
        
        var right = Spec
            .Build<string>(_ => true)
            .WhenTrue("right true")
            .WhenFalse("right false")
            .CreateSpec("right");
        
        var underlying = left | right;
        
        var spec = Spec
            .Build(underlying)
            .CreateSpec("top-level proposition");
        
        var act = spec.IsSatisfiedBy("model");
        
        act.Description.Compact.Should().NotContainAny("left true", "left false", "right true", "right false");
        
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_result(bool model)
    {
        var left = Spec 
            .Build<bool>(m => m)
            .CreateSpec("left");
        
        var right = Spec
            .Build<bool>(m => !m)
            .CreateSpec("right");

        var orSpec = left | right;
        
        var expected = orSpec.IsSatisfiedBy(model);
        
        var sut = Spec
            .Build(orSpec)
            .CreateSpec("composite");
        
        var act = sut.IsSatisfiedBy(model);

        act.Underlying.Should().BeEquivalentTo([expected]);
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_with_metadata_result(bool model)
    {
        var left = Spec 
            .Build<bool>(m => m)
            .CreateSpec("left");
        
        var right = Spec
            .Build<bool>(m => !m)
            .CreateSpec("right");

        var orSpec = left | right;
        
        var expected = orSpec.IsSatisfiedBy(model);
        
        var sut = Spec
            .Build(orSpec)
            .CreateSpec("composite");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.UnderlyingWithMetadata.Should().BeEquivalentTo([expected]);
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_have_a_description_that_has_a_causal_count_value_of_1(bool model)
    {
        var left = Spec 
            .Build<bool>(m => m)
            .CreateSpec("left");
        
        var right = Spec
            .Build<bool>(m => !m)
            .CreateSpec("right");

        var orSpec = left | right;
        
        var sut = Spec
            .Build(orSpec)
            .CreateSpec("composite");
        
        var act = sut.IsSatisfiedBy(model);

        act.Description.CausalOperandCount.Should().Be(1);
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_result(bool model)
    {
        var left = Spec 
            .Build<bool>(m => m)
            .CreateSpec("left");
        
        var right = Spec
            .Build<bool>(m => !m)
            .CreateSpec("right");

        var orSpec = left | right;
        
        var expected = orSpec.IsSatisfiedBy(model).Causes;
        
        var sut = Spec
            .Build(orSpec)
            .CreateSpec("composite");
        
        var act = sut.IsSatisfiedBy(model);

        act.Causes.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_with_metadata_result(bool model)
    {
        var left = Spec 
            .Build<bool>(m => m)
            .CreateSpec("left");
        
        var right = Spec
            .Build<bool>(m => !m)
            .CreateSpec("right");

        var orSpec = left | right;
        
        var expected = orSpec.IsSatisfiedBy(model).CausesWithMetadata;
        
        var sut = Spec
            .Build(orSpec)
            .CreateSpec("composite");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.CausesWithMetadata.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true, "True: left true", "True: right false")]
    [InlineAutoData(false, "False: left false", "False: right true")]
    public void Should_permit_metadata_generated_using_underlying_results(bool model, string expectedLeft, string expectedRight)
    {
        var left = Spec
            .Build<bool>(m => m)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .CreateSpec();
        
        var right = Spec
            .Build<bool>(m => !m)
            .WhenTrue("right true")
            .WhenFalse("right false")
            .CreateSpec();
        
        var underlying = left ^ !right;
        
        var spec = Spec
            .Build(underlying)
            .WhenTrue((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .WhenFalse((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .CreateSpec("top-level proposition");
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Assertions.Should().BeEquivalentTo(expectedLeft, expectedRight);
    }
}