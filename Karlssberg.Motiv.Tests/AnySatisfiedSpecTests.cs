using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AnySatisfiedSpecTests
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
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);

        result.IsSatisfied.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, "ANY{3/3}:false(model is false)")]
    [AutoParams(false, false, true,  "ANY{1/3}:true(model is true)")]
    [AutoParams(false, true, false,  "ANY{1/3}:true(model is true)")]
    [AutoParams(false, true, true,   "ANY{2/3}:true(model is true)")]
    [AutoParams(true, false, false,  "ANY{1/3}:true(model is true)")]
    [AutoParams(true, false, true,   "ANY{2/3}:true(model is true)")]
    [AutoParams(true, true, false,   "ANY{2/3}:true(model is true)")]
    [AutoParams(true, true, true,    "ANY{3/3}:true(model is true)")]
    public void Should_serialize_the_result_of_the_any_operation(
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

        var sut = underlyingSpec.ToAnySatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, "ANY{3/3}:false(False)")]
    [AutoParams(false, false, true,  "ANY{1/3}:true(True)")]
    [AutoParams(false, true, false,  "ANY{1/3}:true(True)")]
    [AutoParams(false, true, true,   "ANY{2/3}:true(True)")]
    [AutoParams(true, false, false,  "ANY{1/3}:true(True)")]
    [AutoParams(true, false, true,   "ANY{2/3}:true(True)")]
    [AutoParams(true, true, false,   "ANY{2/3}:true(True)")]
    [AutoParams(true, true, true,    "ANY{3/3}:true(True)")]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string(
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

        var sut = underlyingSpec
            .BuildAnySatisfiedSpec()
            .YieldWhenAnyTrue("any true")
            .YieldWhenAllFalse("all false")
            .CreateSpec();

        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, "ANY{3/3}:false(False)")]
    [AutoParams(false, false, true,  "ANY{1/3}:true(True)")]
    [AutoParams(false, true, false,  "ANY{1/3}:true(True)")]
    [AutoParams(false, true, true,   "ANY{2/3}:true(True)")]
    [AutoParams(true, false, false,  "ANY{1/3}:true(True)")]
    [AutoParams(true, false, true,   "ANY{2/3}:true(True)")]
    [AutoParams(true, true, false,   "ANY{2/3}:true(True)")]
    [AutoParams(true, true, true,    "ANY{3/3}:true(True)")]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAnySatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);

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

        var sut = throwingSpec.ToAnySatisfiedSpec();

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains(throwingSpec.Description));
        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}