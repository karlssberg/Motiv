using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AnySatisfiedSpecificationTests
{
    [Theory]
    [AutoParams(false, false, false, false)]
    [AutoParams(false, false, true, true)]
    [AutoParams(false, true, false, true)]
    [AutoParams(false, true, true, true)]
    [AutoParams(true, false, false, true)]
    [AutoParams(true, false, true, true)]
    [AutoParams(true, true, false, true)]
    [AutoParams(true, true, true, true)]
    public void Should_perform_the_logical_operation_Any(
        bool first,
        bool second,
        bool third,
        bool expected)
    {
        var underlyingSpec = new Spec<bool, string>(
            "returns the model",
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, "ANY:false(model:false, model:false, model:false)")]
    [AutoParams(false, false, true, "ANY:true(model:false, model:false, model:true)")]
    [AutoParams(false, true, false, "ANY:true(model:false, model:true, model:false)")]
    [AutoParams(false, true, true, "ANY:true(model:false, model:true, model:true)")]
    [AutoParams(true, false, false, "ANY:true(model:true, model:false, model:false)")]
    [AutoParams(true, false, true, "ANY:true(model:true, model:false, model:true)")]
    [AutoParams(true, true, false, "ANY:true(model:true, model:true, model:false)")]
    [AutoParams(true, true, true, "ANY:true(model:true, model:true, model:true)")]
    public void Should_serialize_the_result_of_the_any_operation(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = new Spec<bool, bool>(
            "model",
            m => m,
            true,
            false);
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams(false, false, false, "ANY:false(False, False, False)")]
    [AutoParams(false, false, true, "ANY:true(False, False, True)")]
    [AutoParams(false, true, false, "ANY:true(False, True, False)")]
    [AutoParams(false, true, true, "ANY:true(False, True, True)")]
    [AutoParams(true, false, false, "ANY:true(True, False, False)")]
    [AutoParams(true, false, true, "ANY:true(True, False, True)")]
    [AutoParams(true, true, false, "ANY:true(True, True, False)")]
    [AutoParams(true, true, true, "ANY:true(True, True, True)")]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = new Spec<bool, string>(
            "returns the model",
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams(false, false, false, "ANY:false(False, False, False)")]
    [AutoParams(false, false, true, "ANY:true(False, False, True)")]
    [AutoParams(false, true, false, "ANY:true(False, True, False)")]
    [AutoParams(false, true, true, "ANY:true(False, True, True)")]
    [AutoParams(true, false, false, "ANY:true(True, False, False)")]
    [AutoParams(true, false, true, "ANY:true(True, False, True)")]
    [AutoParams(true, true, false, "ANY:true(True, True, False)")]
    [AutoParams(true, true, true, "ANY:true(True, True, True)")]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = new Spec<bool>(
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams("true",  null)]
    [AutoParams(null, "false")]
    public void Should_not_throw_if_null_metadata_supplied(
        string? trueMetadata, 
        string? falseMetadata,
        IEnumerable<string> models)
    {
        var spec = new Spec<string?, string?>(
                "is null",
                m => m is null, 
                trueMetadata, 
                falseMetadata)
            .ToAnySatisfiedSpec();
        
        var act = () =>  spec.IsSatisfiedBy(models);

        act.Should().NotThrow();
    }
    
    [Theory]
    [AutoParams("true",  null)]
    [AutoParams(null, "false")]
    public void Should_not_throw_if_null_metadata_supplied_with_ternary_parameters(
        string? trueMetadata, 
        string? falseMetadata,
        IEnumerable<string> models)
    {
        var spec = new Spec<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAnySatisfiedSpec(
                _ => null,
                _ => null,
                _ => null)
            .IsSatisfiedBy(models);

        act.Should().NotThrow();
    }
    
    [Theory]
    [AutoParams("true",  null)]
    [AutoParams(null, "false")]
    public void Should_not_throw_if_null_metadata_supplied_with_binary_parameters(
        string? trueMetadata, 
        string? falseMetadata,
        IEnumerable<string> models)
    {
        var spec = new Spec<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAnySatisfiedSpec(
                null as string,
                null as string)
            .IsSatisfiedBy(models);

        act.Should().NotThrow();
    }
    
    [Theory]
    [AutoParams("true",  null)]
    [AutoParams(null, "false")]
    public void Should_not_throw_if_null_metadata_supplied_with_unary_parameters(
        string? trueMetadata, 
        string? falseMetadata,
        IEnumerable<string> models)
    {
        var spec = new Spec<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAnySatisfiedSpec(_ => null)
            .IsSatisfiedBy(models);

        act.Should().NotThrow();
    }
    
    [Theory]
    [AutoParams("true",  null)]
    [AutoParams(null, "false")]
    public void Should_not_throw_if_null_metadata_supplied_without_parameters(
        string? trueMetadata, 
        string? falseMetadata,
        IEnumerable<string> models)
    {
        var spec = new Spec<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAnySatisfiedSpec()
            .IsSatisfiedBy(models);

        act.Should().NotThrow();
    }
    
    [Theory]
    [AutoParams]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpecification<object, string>(
            "should always throw",
            new Exception("should be wrapped"));
        
        var sut = throwingSpec.ToAnySatisfiedSpec();
        
        var act = () => sut.IsSatisfiedBy([model]);
        
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains(sut.Description));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("AnySatisfiedSpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("ThrowingSpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}