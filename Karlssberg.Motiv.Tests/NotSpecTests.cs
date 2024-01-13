using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class NotSpecTests
{
    [Theory]
    [AutoParams(true, false)]
    [AutoParams(false, true)]
    public void Should_perform_logical_not(
        bool operand,
        bool expected,
        object model)
    {
        var spec = Spec
            .Build<object>(m => operand)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec($"is {operand}");
        
        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);
        
        result.IsSatisfied.Should().Be(expected);
        result.GetMetadata().Should().AllBeEquivalentTo(operand);
    }
    
    [Theory]
    [AutoParams(true, "NOT:false(underlying:true)")]
    [AutoParams(false, "NOT:true(underlying:false)")]
    public void  Should_serialize_the_result_of_the_not_operation(
        bool operand,
        string expected,
        object model)
    {
        var spec = Spec
            .Build<object>(m => operand)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("underlying");
        
        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams(true, "NOT:false(True)")]
    [AutoParams(false, "NOT:true(False)")]
    public void  Should_serialize_the_result_of_the_not_operation_when_metadata_is_a_string(
        bool operand,
        string expected,
        object model)
    {
        var spec = Spec
            .Build<object>(m => operand)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
        
        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams(true, "NOT:false(True)")]
    [AutoParams(false, "NOT:true(False)")]
    public void  Should_serialize_the_result_of_the_not_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool operand,
        string expected,
        object model)
    {
        var spec = Spec
            .Build<object>(m => operand)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
        
        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);
        
        result.Description.Should().Be(expected);
        
    }
    
    [Theory]
    [AutoParams]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpec<object, string>(
            "should always throw",
            new Exception("should be wrapped"));
        
        var sut = !throwingSpec;
        
        var act = () => sut.IsSatisfiedBy(model);
        
        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains(throwingSpec.Description));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}