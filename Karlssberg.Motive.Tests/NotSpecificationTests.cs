using FluentAssertions;

namespace Karlssberg.Motive.Tests;

public class NotSpecificationTests
{
    [Theory]
    [AutoParams(true, false)]
    [AutoParams(false, true)]
    public void Should_perform_logical_not(
        bool operand,
        bool expected,
        object model)
    {
        var spec = new Spec<object, bool>(
            $"is {operand}",
            _ => operand,
            true,
            false);
        
        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);
        
        result.IsSatisfied.Should().Be(expected);
        result.GetInsights().Should().AllBeEquivalentTo(operand);
    }
    
    [Theory]
    [AutoParams(true, "NOT:False(underlying:True)")]
    [AutoParams(false, "NOT:True(underlying:False)")]
    public void  Should_serialize_the_result_of_the_not_operation(
        bool operand,
        string expected,
        object model)
    {
        var spec = new Spec<object, bool>(
            "underlying",
            _ => operand,
            true,
            false);
        
        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams(true, "NOT:False(True)")]
    [AutoParams(false, "NOT:True(False)")]
    public void  Should_serialize_the_result_of_the_not_operation_when_metadata_is_a_string(
        bool operand,
        string expected,
        object model)
    {
        var spec = new Spec<object, string>(
            "underlying",
            _ => operand,
            true.ToString(),
            false.ToString());
        
        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams(true, "NOT:False(True)")]
    [AutoParams(false, "NOT:True(False)")]
    public void  Should_serialize_the_result_of_the_not_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool operand,
        string expected,
        object model)
    {
        var spec = new Spec<object>(
            _ => operand,
            true.ToString(),
            false.ToString());
        
        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams("true",  null)]
    [AutoParams(null, "false")]
    public void Should_not_throw_if_null_metadata_supplied(
        string? trueMetadata, 
        string? falseMetadata,
        string? model)
    {
        var spec = new Spec<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        var act = () =>
        {
            var sut = !spec;
            sut.IsSatisfiedBy(model);
        };

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
        
        var sut = !throwingSpec;
        
        var act = () => sut.IsSatisfiedBy(model);
        
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains(throwingSpec.Description));
        act.Should().Throw<SpecificationException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}