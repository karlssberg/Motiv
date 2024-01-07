﻿using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AtLeastNSatisfiedSpecificationTests
{
    [Theory]
    [AutoParams(false, false, false, false, true)]
    [AutoParams(false, false, false, true, true)]
    [AutoParams(false, false, true, false, true)]
    [AutoParams(false, false, true, true, true)]
    [AutoParams(false, true, false, false, true)]
    [AutoParams(false, true, false, true, true)]
    [AutoParams(false, true, true, false, true)]
    [AutoParams(false, true, true, true, true)]
    [AutoParams(true, false, false, false, true)]
    [AutoParams(true, false, false, true, true)]
    [AutoParams(true, false, true, false, true)]
    [AutoParams(true, false, true, true, true)]
    [AutoParams(true, true, false, false, true)]
    [AutoParams(true, true, false, true, true)]
    [AutoParams(true, true, true, false, true)]
    [AutoParams(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_0_is_supplied_as_the_minimum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns the model");
            
        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtLeastNSatisfiedSpec(0);
        var result = sut.IsSatisfiedBy(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, false, false)]
    [AutoParams(false, false, false, true, true)]
    [AutoParams(false, false, true, false, true)]
    [AutoParams(false, false, true, true, true)]
    [AutoParams(false, true, false, false, true)]
    [AutoParams(false, true, false, true, true)]
    [AutoParams(false, true, true, false, true)]
    [AutoParams(false, true, true, true, true)]
    [AutoParams(true, false, false, false, true)]
    [AutoParams(true, false, false, true, true)]
    [AutoParams(true, false, true, false, true)]
    [AutoParams(true, false, true, true, true)]
    [AutoParams(true, true, false, false, true)]
    [AutoParams(true, true, false, true, true)]
    [AutoParams(true, true, true, false, true)]
    [AutoParams(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_1_is_supplied_as_the_minimum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns the model");
        
        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtLeastNSatisfiedSpec(1);
        var result = sut.IsSatisfiedBy(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, false, false)]
    [AutoParams(false, false, false, true, false)]
    [AutoParams(false, false, true, false, false)]
    [AutoParams(false, false, true, true, true)]
    [AutoParams(false, true, false, false, false)]
    [AutoParams(false, true, false, true, true)]
    [AutoParams(false, true, true, false, true)]
    [AutoParams(false, true, true, true, true)]
    [AutoParams(true, false, false, false, false)]
    [AutoParams(true, false, false, true, true)]
    [AutoParams(true, false, true, false, true)]
    [AutoParams(true, false, true, true, true)]
    [AutoParams(true, true, false, false, true)]
    [AutoParams(true, true, false, true, true)]
    [AutoParams(true, true, true, false, true)]
    [AutoParams(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_2_is_supplied_as_the_minimum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns the model");
        
        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtLeastNSatisfiedSpec(2);
        var result = sut.IsSatisfiedBy(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, false, false)]
    [AutoParams(false, false, false, true, false)]
    [AutoParams(false, false, true, false, false)]
    [AutoParams(false, false, true, true, false)]
    [AutoParams(false, true, false, false, false)]
    [AutoParams(false, true, false, true, false)]
    [AutoParams(false, true, true, false, false)]
    [AutoParams(false, true, true, true, false)]
    [AutoParams(true, false, false, false, false)]
    [AutoParams(true, false, false, true, false)]
    [AutoParams(true, false, true, false, false)]
    [AutoParams(true, false, true, true, false)]
    [AutoParams(true, true, false, false, false)]
    [AutoParams(true, true, false, true, false)]
    [AutoParams(true, true, true, false, false)]
    [AutoParams(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_the_set_size_is_supplied_as_the_minimum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns the model");
        
        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtLeastNSatisfiedSpec(models.Length);
        var result = sut.IsSatisfiedBy(models);
        
                  result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, "AT_LEAST_1:False(False, False, False)")]
    [AutoParams(false, false, true, "AT_LEAST_1:True(False, False, True)")]
    [AutoParams(false, true, false, "AT_LEAST_1:True(False, True, False)")]
    [AutoParams(false, true, true, "AT_LEAST_1:True(False, True, True)")]
    [AutoParams(true, false, false, "AT_LEAST_1:True(True, False, False)")]
    [AutoParams(true, false, true, "AT_LEAST_1:True(True, False, True)")]
    [AutoParams(true, true, false, "AT_LEAST_1:True(True, True, False)")]
    [AutoParams(true, true, true, "AT_LEAST_1:True(True, True, True)")]
    public void Should_serialize_the_result_of_the_at_least_of_1_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns the model");
        
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAtLeastNSatisfiedSpec(1);
        var result = sut.IsSatisfiedBy(models);
        
        
    }
    
    [Theory]
    [AutoParams(false, false, false, "AT_LEAST_1:false(false, false, false)")]
    [AutoParams(false, false, true, "AT_LEAST_1:true(false, false, true)")]
    [AutoParams(false, true, false, "AT_LEAST_1:true(false, true, false)")]
    [AutoParams(false, true, true, "AT_LEAST_1:true(false, true, true)")]
    [AutoParams(true, false, false, "AT_LEAST_1:true(true, false, false)")]
    [AutoParams(true, false, true, "AT_LEAST_1:true(true, false, true)")]
    [AutoParams(true, true, false, "AT_LEAST_1:true(true, true, false)")]
    [AutoParams(true, true, true, "AT_LEAST_1:true(true, true, true)")]
    public void Should_serialize_the_result_of_the_at_least_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString().ToLowerInvariant())
            .YieldWhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();
        
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAtLeastNSatisfiedSpec(1);
        var result = sut.IsSatisfiedBy(models);
        
        result.Description.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, "AT_LEAST_1:false(model:false, model:false, model:false)")]
    [AutoParams(false, false, true, "AT_LEAST_1:true(model:false, model:false, model:true)")]
    [AutoParams(false, true, false, "AT_LEAST_1:true(model:false, model:true, model:false)")]
    [AutoParams(false, true, true, "AT_LEAST_1:true(model:false, model:true, model:true)")]
    [AutoParams(true, false, false, "AT_LEAST_1:true(model:true, model:false, model:false)")]
    [AutoParams(true, false, true, "AT_LEAST_1:true(model:true, model:false, model:true)")]
    [AutoParams(true, true, false, "AT_LEAST_1:true(model:true, model:true, model:false)")]
    [AutoParams(true, true, true, "AT_LEAST_1:true(model:true, model:true, model:true)")]
    public void Should_serialize_the_result_of_the_all_operation(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("model");
        
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAtLeastNSatisfiedSpec(1);
        var result = sut.IsSatisfiedBy(models);
        
        result.Description.Should().Be(expected);
    }
    
    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "AT_LEAST_1(underlying spec description)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("underlying spec description");

        var sut = underlyingSpec.ToAtLeastNSatisfiedSpec(1);

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }
    
    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "AT_LEAST_1(True)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
            
        var sut = underlyingSpec.ToAtLeastNSatisfiedSpec(1);

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }
    
    [Theory]
    [AutoParams]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpecification<object, string>(
            "throws",
            new Exception("should be wrapped"));
        
        var sut = throwingSpec.ToAtLeastNSatisfiedSpec(1);
        
        var act = () => sut.IsSatisfiedBy([model]);
        
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains(sut.Description));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("AtLeastNSatisfiedSpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("ThrowingSpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}