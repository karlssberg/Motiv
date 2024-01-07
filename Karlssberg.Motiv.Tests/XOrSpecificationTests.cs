using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class XOrSpecificationTests
{
    [Theory]
    [AutoParams(true, true, false)]
    [AutoParams(true, false, true)]
    [AutoParams(false, true, true)]
    [AutoParams(false, false, false)]
    public void Should_perform_logical_xor(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        var left = Spec
            .Build<object>(m => leftResult)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("left");
        
        var right = Spec
            .Build<object>(m => rightResult)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("right");
        
        var sut = left ^ right;

        var result = sut.IsSatisfiedBy(model);

        result.IsSatisfied.Should().Be(expected);
        result.GetInsights().Should().HaveCount(leftResult == rightResult ? 1 : 2);
        result.GetInsights().Should().Contain(leftResult);
        result.GetInsights().Should().Contain(rightResult);
    }
    
    [Theory]
    [AutoParams(true, true, "(left:true) XOR:false (right:true)")]
    [AutoParams(true, false, "(left:true) XOR:true (right:false)")]
    [AutoParams(false, true, "(left:false) XOR:true (right:true)")]
    [AutoParams(false, false, "(left:false) XOR:false (right:false)")]
    public void Should_serialize_the_result_of_the_xor_operation(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        var left = Spec
            .Build<object>(m => leftResult)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("left");
        
        var right = Spec
            .Build<object>(m => rightResult)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("right");
        
        var sut = left ^ right;

        var result = sut.IsSatisfiedBy(model);

        result.Description.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(true, true, "(True) XOR:false (True)")]
    [AutoParams(true, false, "(True) XOR:true (False)")]
    [AutoParams(false, true, "(False) XOR:true (True)")]
    [AutoParams(false, false, "(False) XOR:false (False)")]
    public void Should_serialize_the_result_of_the_xor_operation_when_metadata_is_a_string(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        var left = Spec
            .Build<object>(m => leftResult)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
            
        var right = Spec
            .Build<object>(m => rightResult)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
        
        var sut = left ^ right;

        var result = sut.IsSatisfiedBy(model);

        result.Description.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(true, true, "(True) XOR:false (True)")]
    [AutoParams(true, false, "(True) XOR:true (False)")]
    [AutoParams(false, true, "(False) XOR:true (True)")]
    [AutoParams(false, false, "(False) XOR:false (False)")]
    public void Should_serialize_the_result_of_the_xor_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        var left = Spec
            .Build<object>(m => leftResult)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
            
        var right = Spec
            .Build<object>(m => rightResult)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
        
        var sut = left ^ right;

        var result = sut.IsSatisfiedBy(model);

        result.Description.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(true, true)]
    [AutoParams(true, false)]
    [AutoParams(false, true)]
    [AutoParams(false, false)]
    public void Should_provide_a_description_of_the_specification(bool leftResult, bool rightResult)
    {
        var left = Spec
            .Build<object>(m => leftResult)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("left");
        
        var right = Spec
            .Build<object>(m => rightResult)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("right");
        
        var expected = $"({left.Description}) ^ ({right.Description})";
        
        var sut = left ^ right;

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(true, true)]
    [AutoParams(true, false)]
    [AutoParams(false, true)]
    [AutoParams(false, false)]
    public void Should_provide_a_description_of_the_specification_when_using_convenience_specification(bool leftResult, bool rightResult)
    {
        var left = Spec
            .Build<object>(m => leftResult)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
            
        var right = Spec
            .Build<object>(m => rightResult)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
        
        var expected = $"({left.Description}) ^ ({right.Description})";
        
        var sut = left ^ right;

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }
    
    [Theory]
    [AutoParams]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    { 
        var normalSpec = Spec
            .Build<object>(m => true)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
        
        var throwingSpec = new ThrowingSpecification<object, string>(
            "should always throw",
            new Exception("should be wrapped"));
        
        var sut = throwingSpec ^ normalSpec;
        
        var act = () => sut.IsSatisfiedBy(model);
        
        act.Should().Throw<SpecificationException>().Where(ex => ex.Message.Contains(throwingSpec.Description));
        act.Should().Throw<SpecificationException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}