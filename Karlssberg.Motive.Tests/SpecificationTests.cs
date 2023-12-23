using FluentAssertions;

namespace Karlssberg.Motive.Tests;

public class SpecificationTests
{
    [Theory]
    [AutoParams(true)]
    [AutoParams(false)]
    public void Should_return_a_result_that_satisfies_the_predicate(bool model)
    {
        var sut = new Specification<bool, string>(
            "returns model value",
            m => m, 
            true.ToString(), 
            false.ToString());

        var result = sut.Evaluate(model);

        result.IsSatisfied.Should().Be(model);
        result.Causes.Should().HaveCount(1);
        result.Causes.Should().AllBe(model.ToString());
    }
    
    [Fact]
    public void Should_handle_null_model_without_throwing()
    {
        var sut = new Specification<string?, string>(
            "is null",
            m => m is null, 
            true.ToString(), 
            false.ToString());

        var result = sut.Evaluate(null);

        result.IsSatisfied.Should().Be(true);
        result.Causes.Should().HaveCount(1);
        result.Causes.Should().AllBe(true.ToString());
    }
    
    [Theory]
    [AutoParams(true)]
    [AutoParams(false)]
    public void Should_return_a_result_that_satisfies_the_predicate_when_using_textual_specification(bool model)
    {
        var sut = new Specification<bool>(
            m => m, 
            true.ToString(), 
            false.ToString());

        var result = sut.Evaluate(model);

        result.IsSatisfied.Should().Be(model);
        result.Causes.Should().HaveCount(1);
        result.Causes.Should().AllBe(model.ToString());
    }
    
    [Fact]
    public void Should_handle_null_model_without_throwing_when_using_textual_specification()
    {
        var sut = new Specification<string?>(
            m => m is null, 
            true.ToString(), 
            false.ToString());

        var result = sut.Evaluate(null);

        result.IsSatisfied.Should().Be(true);
        result.Causes.Should().HaveCount(1);
        result.Causes.Should().AllBe(true.ToString());
    }
    
    [Fact]
    public void Should_throw_if_null_predicate_is_supplied()
    {
        var act = () =>  new Specification<string?>(
            null!, 
            true.ToString(), 
            false.ToString());

        act.Should().Throw<ArgumentNullException>();
    }
    
    [Theory]
    [AutoParams("true",  null)]
    [AutoParams(null, "false")]
    public void Should_not_throw_if_null_metadata_supplied(string? trueMetadata, string? falseMetadata)
    {
        var act = () =>  new Specification<string?, string?>(
            "is null",
            m => m is null, 
            trueMetadata, 
            falseMetadata);

        act.Should().NotThrow();
    }
    
    [Theory]
    [AutoParams("hello world", null)]
    [AutoParams("hello world", "")]
    [AutoParams("hello world", " ")]
    [AutoParams(null, "hello world")]
    [AutoParams("", "hello world")]
    [AutoParams(" ", "hello world")]
    public void Should_throw_if_invalid_reasons_are_supplied(string? trueBecause, string? falseBecause)
    {
        var act = () =>  new Specification<string?>(
            m => m is null, 
            trueBecause, 
            falseBecause);

        act.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception_when_using_text_metadata()
    {
        var act = () =>  new Specification<string?>(
            _ => throw new Exception("should be wrapped"), 
            true.ToString(), 
            false.ToString())
            .Evaluate(null);

        act.Should().Throw<SpecificationException>().WithInnerExceptionExactly<Exception>();
        act.Should().Throw<SpecificationException>().WithMessage("*should be wrapped*");
        act.Should().Throw<SpecificationException>().WithMessage($"*{true}*");
    }
    
    [Fact]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception()
    {
        var spec = new Specification<string?, object>(
            "should throw",
            _ => throw new Exception("should be wrapped"),
            true.ToString(),
            false.ToString());
        
        var act = () => spec.Evaluate(null);

        act.Should().Throw<SpecificationException>().WithInnerExceptionExactly<Exception>();
        act.Should().Throw<SpecificationException>().WithMessage("*should be wrapped*");
        act.Should().Throw<SpecificationException>().WithMessage("*should throw*");
    }
}