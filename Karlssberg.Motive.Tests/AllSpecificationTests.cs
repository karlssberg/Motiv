using FluentAssertions;

namespace Karlssberg.Motive.Tests;

public class AllSpecificationTests
{
    [Theory]
    [AutoParams(false, false, false, false)]
    [AutoParams(false, false, true, false)]
    [AutoParams(false, true, false, false)]
    [AutoParams(false, true, true, false)]
    [AutoParams(true, false, false, false)]
    [AutoParams(true, false, true, false)]
    [AutoParams(true, true, false, false)]
    [AutoParams(true, true, true, true)]
    public void Should_perform_the_logical_operation_All(
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

        var sut = underlyingSpec.ToAllSpecification();
        var result = sut.Evaluate(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, "ALL:False(False, False, False)")]
    [AutoParams(false, false, true, "ALL:False(False, False, True)")]
    [AutoParams(false, true, false, "ALL:False(False, True, False)")]
    [AutoParams(false, true, true, "ALL:False(False, True, True)")]
    [AutoParams(true, false, false, "ALL:False(True, False, False)")]
    [AutoParams(true, false, true, "ALL:False(True, False, True)")]
    [AutoParams(true, true, false, "ALL:False(True, True, False)")]
    [AutoParams(true, true, true, "ALL:True(True, True, True)")]
    public void Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string(
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

        var sut = underlyingSpec.ToAllSpecification();
        var result = sut.Evaluate(models);
        
        
    }
    
    [Theory]
    [AutoParams(false, false, false, "ALL:False(False, False, False)")]
    [AutoParams(false, false, true, "ALL:False(False, False, True)")]
    [AutoParams(false, true, false, "ALL:False(False, True, False)")]
    [AutoParams(false, true, true, "ALL:False(False, True, True)")]
    [AutoParams(true, false, false, "ALL:False(True, False, False)")]
    [AutoParams(true, false, true, "ALL:False(True, False, True)")]
    [AutoParams(true, true, false, "ALL:False(True, True, False)")]
    [AutoParams(true, true, true, "ALL:True(True, True, True)")]
    public void Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
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

        var sut = underlyingSpec.ToAllSpecification();
        var result = sut.Evaluate(models);
        
        
    }
    
    [Theory]
    [AutoParams(false, false, false, "ALL:False(model:False, model:False, model:False)")]
    [AutoParams(false, false, true, "ALL:False(model:False, model:False, model:True)")]
    [AutoParams(false, true, false, "ALL:False(model:False, model:True, model:False)")]
    [AutoParams(false, true, true, "ALL:False(model:False, model:True, model:True)")]
    [AutoParams(true, false, false, "ALL:False(model:True, model:False, model:False)")]
    [AutoParams(true, false, true, "ALL:False(model:True, model:False, model:True)")]
    [AutoParams(true, true, false, "ALL:False(model:True, model:True, model:False)")]
    [AutoParams(true, true, true, "ALL:True(model:True, model:True, model:True)")]
    public void Should_serialize_the_result_of_the_all_operation(
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

        var sut = underlyingSpec.ToAllSpecification();
        var result = sut.Evaluate(models);
        
        
    }
    
    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "ALL(underlying spec description)";
        var underlyingSpec = new Spec<bool, object>(
            "underlying spec description",
            m => m,
            true.ToString(),
            false.ToString());

        var sut = underlyingSpec.ToAllSpecification();

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }
    
    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "ALL(True)";
        var underlyingSpec = new Spec<bool>(
            m => m,
            true.ToString(),
            false.ToString());

        var sut = underlyingSpec.ToAllSpecification();

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
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
            falseMetadata);
        
        var act = () => spec
            .ToAllSpecification()
            .Evaluate(models);

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
            .ToAllSpecification(
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
        var spec = new Spec<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAllSpecification(
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
        var spec = new Spec<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAllSpecification(_ => null)
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
        var spec = new Spec<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () => spec
            .ToAllSpecification()
            .Evaluate(models);

        act.Should().NotThrow();
    }
    
    [Theory]
    [AutoParams]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpecification<object, string>(
            "throws",
            new Exception("should be wrapped"));
        
        var sut = throwingSpec.ToAllSpecification();
        
        var act = () => sut.Evaluate([model]);
        
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains(sut.Description));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("AllSpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("ThrowingSpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}