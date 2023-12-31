using FluentAssertions;

namespace Karlssberg.Motive.Tests;

public class AtMostSpecificationTests
{
    [Theory]
    [AutoParams(false, false, false, false, true)]
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
    [AutoParams(true, true, true, true, false)]
    public void Should_perform_the_logical_operation_at_most_when_0_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = new Spec<bool, string>(
            "returns the model",
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtMostSpecification(0);
        var result = sut.IsSatisfiedBy(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, false, true)]
    [AutoParams(false, false, false, true, true)]
    [AutoParams(false, false, true, false, true)]
    [AutoParams(false, false, true, true, false)]
    [AutoParams(false, true, false, false, true)]
    [AutoParams(false, true, false, true, false)]
    [AutoParams(false, true, true, false, false)]
    [AutoParams(false, true, true, true, false)]
    [AutoParams(true, false, false, false, true)]
    [AutoParams(true, false, false, true, false)]
    [AutoParams(true, false, true, false, false)]
    [AutoParams(true, false, true, true, false)]
    [AutoParams(true, true, false, false, false)]
    [AutoParams(true, true, false, true, false)]
    [AutoParams(true, true, true, false, false)]
    [AutoParams(true, true, true, true, false)]
    public void Should_perform_the_logical_operation_at_most_when_1_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = new Spec<bool, string>(
            "returns the model",
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtMostSpecification(1);
        var result = sut.IsSatisfiedBy(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, false, true)]
    [AutoParams(false, false, false, true, true)]
    [AutoParams(false, false, true, false, true)]
    [AutoParams(false, false, true, true, true)]
    [AutoParams(false, true, false, false, true)]
    [AutoParams(false, true, false, true, true)]
    [AutoParams(false, true, true, false, true)]
    [AutoParams(false, true, true, true, false)]
    [AutoParams(true, false, false, false, true)]
    [AutoParams(true, false, false, true, true)]
    [AutoParams(true, false, true, false, true)]
    [AutoParams(true, false, true, true, false)]
    [AutoParams(true, true, false, false, true)]
    [AutoParams(true, true, false, true, false)]
    [AutoParams(true, true, true, false, false)]
    [AutoParams(true, true, true, true, false)]
    public void Should_perform_the_logical_operation_at_most_when_2_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = new Spec<bool, string>(
            "returns the model",
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtMostSpecification(2);
        var result = sut.IsSatisfiedBy(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
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
    public void Should_perform_the_logical_operation_at_most_when_the_set_size_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = new Spec<bool, string>(
            "returns the model",
            m => m,
            true.ToString(),
            false.ToString());
        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtMostSpecification(models.Length);
        var result = sut.IsSatisfiedBy(models);
        
        result.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(false, false, false, "AT_MOST[1]:True(False, False, False)")]
    [AutoParams(false, false, true, "AT_MOST[1]:True(False, False, True)")]
    [AutoParams(false, true, false, "AT_MOST[1]:True(False, True, False)")]
    [AutoParams(false, true, true, "AT_MOST[1]:False(False, True, True)")]
    [AutoParams(true, false, false, "AT_MOST[1]:True(True, False, False)")]
    [AutoParams(true, false, true, "AT_MOST[1]:False(True, False, True)")]
    [AutoParams(true, true, false, "AT_MOST[1]:False(True, True, False)")]
    [AutoParams(true, true, true, "AT_MOST[1]:False(True, True, True)")]
    public void Should_serialize_the_result_of_the_at_most_of_1_operation_when_metadata_is_a_string(
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

        var sut = underlyingSpec.ToAtMostSpecification(1);
        var result = sut.IsSatisfiedBy(models);
        
        
    }
    
    [Theory]
    [AutoParams(false, false, false, "AT_MOST[1]:True(False, False, False)")]
    [AutoParams(false, false, true, "AT_MOST[1]:True(False, False, True)")]
    [AutoParams(false, true, false, "AT_MOST[1]:True(False, True, False)")]
    [AutoParams(false, true, true, "AT_MOST[1]:False(False, True, True)")]
    [AutoParams(true, false, false, "AT_MOST[1]:True(True, False, False)")]
    [AutoParams(true, false, true, "AT_MOST[1]:False(True, False, True)")]
    [AutoParams(true, true, false, "AT_MOST[1]:False(True, True, False)")]
    [AutoParams(true, true, true, "AT_MOST[1]:False(True, True, True)")]
    public void Should_serialize_the_result_of_the_at_most_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
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

        var sut = underlyingSpec.ToAtMostSpecification(1);
        var result = sut.IsSatisfiedBy(models);
        
        
    }
    
    [Theory]
    [AutoParams(false, false, false, "AT_MOST[1]:True(model:False, model:False, model:False)")]
    [AutoParams(false, false, true, "AT_MOST[1]:True(model:False, model:False, model:True)")]
    [AutoParams(false, true, false, "AT_MOST[1]:True(model:False, model:True, model:False)")]
    [AutoParams(false, true, true, "AT_MOST[1]:False(model:False, model:True, model:True)")]
    [AutoParams(true, false, false, "AT_MOST[1]:True(model:True, model:False, model:False)")]
    [AutoParams(true, false, true, "AT_MOST[1]:False(model:True, model:False, model:True)")]
    [AutoParams(true, true, false, "AT_MOST[1]:False(model:True, model:True, model:False)")]
    [AutoParams(true, true, true, "AT_MOST[1]:False(model:True, model:True, model:True)")]
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

        var sut = underlyingSpec.ToAtMostSpecification(1);
        var result = sut.IsSatisfiedBy(models);
        
        
    }
    
    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "AT_MOST[1](underlying spec description)";
        var underlyingSpec = new Spec<bool, object>(
            "underlying spec description",
            m => m,
            true.ToString(),
            false.ToString());

        var sut = underlyingSpec.ToAtMostSpecification(1);

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }
    
    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "AT_MOST[1](True)";
        var underlyingSpec = new Spec<bool>(
            m => m,
            true.ToString(),
            false.ToString());

        var sut = underlyingSpec.ToAtMostSpecification(1);

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
            .ToAtMostSpecification(1)
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
            .ToAtMostSpecification(1,
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
            .ToAtMostSpecification(1, _ => null)
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
            .ToAtMostSpecification(1)
            .IsSatisfiedBy(models);

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
        
        var sut = throwingSpec.ToAtMostSpecification(1);
        
        var act = () => sut.IsSatisfiedBy([model]);
        
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains(sut.Description));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("AtMostSpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains("ThrowingSpecification<Object, String>"));
        act.Should().Throw<SpecificationException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}