using FluentAssertions;

namespace Karlssberg.Motive.Tests;

public class AnySpecificationTests
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
        var underlyingSpec = new Specification<bool, string>(
            "returns the model",
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySpecification();
        var result = sut.Evaluate(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, "ANY:False(model:False, model:False, model:False)")]
    [AutoParams(false, false, true, "ANY:True(model:False, model:False, model:True)")]
    [AutoParams(false, true, false, "ANY:True(model:False, model:True, model:False)")]
    [AutoParams(false, true, true, "ANY:True(model:False, model:True, model:True)")]
    [AutoParams(true, false, false, "ANY:True(model:True, model:False, model:False)")]
    [AutoParams(true, false, true, "ANY:True(model:True, model:False, model:True)")]
    [AutoParams(true, true, false, "ANY:True(model:True, model:True, model:False)")]
    [AutoParams(true, true, true, "ANY:True(model:True, model:True, model:True)")]
    public void Should_serialize_the_result_of_the_any_operation(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = new Specification<bool, bool>(
            "model",
            m => m,
            true,
            false);
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySpecification();
        var result = sut.Evaluate(models);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams(false, false, false, "ANY:False(False, False, False)")]
    [AutoParams(false, false, true, "ANY:True(False, False, True)")]
    [AutoParams(false, true, false, "ANY:True(False, True, False)")]
    [AutoParams(false, true, true, "ANY:True(False, True, True)")]
    [AutoParams(true, false, false, "ANY:True(True, False, False)")]
    [AutoParams(true, false, true, "ANY:True(True, False, True)")]
    [AutoParams(true, true, false, "ANY:True(True, True, False)")]
    [AutoParams(true, true, true, "ANY:True(True, True, True)")]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = new Specification<bool, string>(
            "returns the model",
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySpecification();
        var result = sut.Evaluate(models);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams(false, false, false, "ANY:False(False, False, False)")]
    [AutoParams(false, false, true, "ANY:True(False, False, True)")]
    [AutoParams(false, true, false, "ANY:True(False, True, False)")]
    [AutoParams(false, true, true, "ANY:True(False, True, True)")]
    [AutoParams(true, false, false, "ANY:True(True, False, False)")]
    [AutoParams(true, false, true, "ANY:True(True, False, True)")]
    [AutoParams(true, true, false, "ANY:True(True, True, False)")]
    [AutoParams(true, true, true, "ANY:True(True, True, True)")]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = new Specification<bool>(
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySpecification();
        var result = sut.Evaluate(models);
        
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
        var spec = new Specification<string?, string?>(
                "is null",
                m => m is null, 
                trueMetadata, 
                falseMetadata)
            .ToAnySpecification();
        
        var act = () =>  spec.Evaluate(models);

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
        var spec = new Specification<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAnySpecification(
                _ => null,
                _ => null,
                _ => null)
            .Evaluate(models);

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
        var spec = new Specification<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAnySpecification(
                null as string,
                null as string)
            .Evaluate(models);

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
        var spec = new Specification<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAnySpecification(_ => null)
            .Evaluate(models);

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
        var spec = new Specification<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAnySpecification()
            .Evaluate(models);

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
        
        var sut = throwingSpec.ToAnySpecification();
        
        var act = () => sut.Evaluate([model]);
        
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains(sut.Description));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("AnySpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("ThrowingSpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}