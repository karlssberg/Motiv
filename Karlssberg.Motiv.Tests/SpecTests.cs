using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class SpecTests
{
    [Theory]
    [AutoParams(true)]
    [AutoParams(false)]
    public void Should_return_a_result_that_satisfies_the_predicate(bool model)
    {
        var sut = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns model value");

        var result = sut.IsSatisfiedBy(model);

        result.Value.Should().Be(model);
        result.GetMetadata().Should().HaveCount(1);
        result.GetMetadata().Should().AllBe(model.ToString());
    }
    
    [Theory]
    [AutoParams(true, "underlying true")]
    [AutoParams(false, "underlying false")]
    public void Should_return_a_result_that_satisfies_the_spec(bool model, string expectedReason)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
        
        var sut = Spec
            .Build<bool>(() => underlyingSpec)
            .YieldWhenTrue("underlying true")
            .YieldWhenFalse("underlying false")
            .CreateSpec("returns model value");

        var result = sut.IsSatisfiedBy(model);

        result.Value.Should().Be(model);
        result.GetMetadata().Should().HaveCount(1);
        result.GetMetadata().Should().AllBe(expectedReason);
        result.Reasons.Should().BeEquivalentTo(expectedReason);
    }

    [Fact]
    public void Should_handle_null_model_without_throwing()
    {
        var sut = Spec
            .Build<string?>(m => m is null)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("is null");

        var result = sut.IsSatisfiedBy(null);

        result.Value.Should().BeTrue();
        result.GetMetadata().Should().HaveCount(1);
        result.GetMetadata().Should().AllBe(true.ToString());
    }

    [Theory]
    [AutoParams(true)]
    [AutoParams(false)]
    public void Should_return_a_result_that_satisfies_the_predicate_when_using_textual_specification(bool model)
    {
        var sut = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        var result = sut.IsSatisfiedBy(model);

        result.Value.Should().Be(model);
        result.GetMetadata().Should().HaveCount(1);
        result.GetMetadata().Should().AllBe(model.ToString());
    }

    [Fact]
    public void Should_handle_null_model_without_throwing_when_using_textual_specification()
    {
        var sut = Spec
            .Build<string?>(m => m is null)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        var result = sut.IsSatisfiedBy(null);

        result.Value.Should().Be(true);
        result.GetMetadata().Should().HaveCount(1);
        result.GetMetadata().Should().AllBe(true.ToString());
    }

    [Fact]
    public void Should_throw_if_null_predicate_is_supplied()
    {
        var act = () => Spec
            .Build<string?>(default(Func<string, bool>)!)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [AutoParams("true", null)]
    [AutoParams(null, "false")]
    public void Should_throw_if_null_metadata_supplied(string? trueMetadata, string? falseMetadata)
    {
        var act = () => Spec
            .Build<string?>(m => m is null)
            .YieldWhenTrue(trueMetadata!)
            .YieldWhenFalse(falseMetadata!)
            .CreateSpec("is null");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [AutoParams("hello world", null)]
    [AutoParams(null, "hello world")]
    public void Should_throw_if_invalid_reasons_are_supplied(string? trueBecause, string? falseBecause)
    {
        var act = () => Spec
            .Build<string?>(m => m is null)
            .YieldWhenTrue(trueBecause!)
            .YieldWhenFalse(falseBecause!)
            .CreateSpec();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception_when_using_text_metadata()
    {
        var act = () => Spec
            .Build((Func<string?, bool>)(_ => throw new Exception("should be wrapped")))
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec()
            .IsSatisfiedBy(null);

        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>();
        act.Should().Throw<SpecException>().WithMessage("*should be wrapped*");
        act.Should().Throw<SpecException>().WithMessage($"*{true}*");
    }

    [Fact]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception()
    {
        var spec = Spec
            .Build((Func<string?, bool>)(_ => throw new Exception("should be wrapped")))
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("should throw");

        var act = () => spec.IsSatisfiedBy(null);

        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>();
        act.Should().Throw<SpecException>().WithMessage("*should be wrapped*");
        act.Should().Throw<SpecException>().WithMessage("*should throw*");
    }
}