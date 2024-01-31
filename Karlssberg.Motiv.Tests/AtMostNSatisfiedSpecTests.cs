using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AtMostNSatisfiedSpecTests
{
    [Theory]
    [AutoParams(false, false, false, false, true )]
    [AutoParams(false, false, false, true,  false)]
    [AutoParams(false, false, true,  false, false)]
    [AutoParams(false, false, true,  true,  false)]
    [AutoParams(false, true,  false, false, false)]
    [AutoParams(false, true,  false, true,  false)]
    [AutoParams(false, true,  true,  false, false)]
    [AutoParams(false, true,  true,  true,  false)]
    [AutoParams(true,  false, false, false, false)]
    [AutoParams(true,  false, false, true,  false)]
    [AutoParams(true,  false, true,  false, false)]
    [AutoParams(true,  false, true,  true,  false)]
    [AutoParams(true,  true,  false, false, false)]
    [AutoParams(true,  true,  false, true,  false)]
    [AutoParams(true,  true,  true,  false, false)]
    [AutoParams(true,  true,  true,  true,  false)]
    public void Should_perform_the_logical_operation_at_most_when_0_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns the model");

        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtMostNSatisfiedSpec(0);
        var result = sut.IsSatisfiedBy(models);

        result.IsSatisfied.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, false, true )]
    [AutoParams(false, false, false, true,  true )]
    [AutoParams(false, false, true,  false, true )]
    [AutoParams(false, false, true,  true,  false)]
    [AutoParams(false, true,  false, false, true )]
    [AutoParams(false, true,  false, true,  false)]
    [AutoParams(false, true,  true,  false, false)]
    [AutoParams(false, true,  true,  true,  false)]
    [AutoParams(true,  false, false, false, true )]
    [AutoParams(true,  false, false, true,  false)]
    [AutoParams(true,  false, true,  false, false)]
    [AutoParams(true,  false, true,  true,  false)]
    [AutoParams(true,  true,  false, false, false)]
    [AutoParams(true,  true,  false, true,  false)]
    [AutoParams(true,  true,  true,  false, false)]
    [AutoParams(true,  true,  true,  true,  false)]
    public void Should_perform_the_logical_operation_at_most_when_1_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns the model");

        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtMostNSatisfiedSpec(1);
        var result = sut.IsSatisfiedBy(models);

        result.IsSatisfied.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, false, true )]
    [AutoParams(false, false, false, true,  true )]
    [AutoParams(false, false, true,  false, true )]
    [AutoParams(false, false, true,  true,  true )]
    [AutoParams(false, true,  false, false, true )]
    [AutoParams(false, true,  false, true,  true )]
    [AutoParams(false, true,  true,  false, true )]
    [AutoParams(false, true,  true,  true,  false)]
    [AutoParams(true,  false, false, false, true )]
    [AutoParams(true,  false, false, true,  true )]
    [AutoParams(true,  false, true,  false, true )]
    [AutoParams(true,  false, true,  true,  false)]
    [AutoParams(true,  true,  false, false, true )]
    [AutoParams(true,  true,  false, true,  false)]
    [AutoParams(true,  true,  true,  false, false)]
    [AutoParams(true,  true,  true,  true,  false)]
    public void Should_perform_the_logical_operation_at_most_when_2_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns the model");

        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtMostNSatisfiedSpec(2);
        var result = sut.IsSatisfiedBy(models);

        result.IsSatisfied.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, false, true )]
    [AutoParams(false, false, false, true,  true )]
    [AutoParams(false, false, true,  false, true )]
    [AutoParams(false, false, true,  true,  true )]
    [AutoParams(false, true,  false, false, true )]
    [AutoParams(false, true,  false, true,  true )]
    [AutoParams(false, true,  true,  false, true )]
    [AutoParams(false, true,  true,  true,  true )]
    [AutoParams(true,  false, false, false, true )]
    [AutoParams(true,  false, false, true,  true )]
    [AutoParams(true,  false, true,  false, true )]
    [AutoParams(true,  false, true,  true,  true )]
    [AutoParams(true,  true,  false, false, true )]
    [AutoParams(true,  true,  false, true,  true )]
    [AutoParams(true,  true,  true,  false, true )]
    [AutoParams(true,  true,  true,  true,  true )]
    public void Should_perform_the_logical_operation_at_most_when_the_set_size_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("returns the model");

        bool[] models = [first, second, third, fourth];

        var sut = underlyingSpec.ToAtMostNSatisfiedSpec(models.Length);
        var result = sut.IsSatisfiedBy(models);

        result.IsSatisfied.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, "AT_MOST_1{0/3}:true")]
    [AutoParams(false, false, true,  "AT_MOST_1{1/3}:true(1x is satisfied)")]
    [AutoParams(false, true,  false, "AT_MOST_1{1/3}:true(1x is satisfied)")]
    [AutoParams(false, true,  true,  "AT_MOST_1{2/3}:false(2x is satisfied)")]
    [AutoParams(true,  false, false, "AT_MOST_1{1/3}:true(1x is satisfied)")]
    [AutoParams(true,  false, true,  "AT_MOST_1{2/3}:false(2x is satisfied)")]
    [AutoParams(true,  true,  false, "AT_MOST_1{2/3}:false(2x is satisfied)")]
    [AutoParams(true,  true,  true,  "AT_MOST_1{3/3}:false(3x is satisfied)")]
    public void Should_serialize_the_result_of_the_at_most_of_1_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue("is satisfied")
            .YieldWhenFalse("is not satisfied")
            .CreateSpec("returns the model");

        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAtMostNSatisfiedSpec(1);
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, "AT_MOST_1{0/3}:true")]
    [AutoParams(false, false, true,  "AT_MOST_1{1/3}:true(1x True)")]
    [AutoParams(false, true,  false, "AT_MOST_1{1/3}:true(1x True)")]
    [AutoParams(false, true,  true,  "AT_MOST_1{2/3}:false(2x True)")]
    [AutoParams(true,  false, false, "AT_MOST_1{1/3}:true(1x True)")]
    [AutoParams(true,  false, true,  "AT_MOST_1{2/3}:false(2x True)")]
    [AutoParams(true,  true,  false, "AT_MOST_1{2/3}:false(2x True)")]
    [AutoParams(true,  true,  true,  "AT_MOST_1{3/3}:false(3x True)")]
    public void Should_serialize_the_result_of_the_at_most_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
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

        var sut = underlyingSpec.ToAtMostNSatisfiedSpec(1);
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, "AT_MOST_1{0/3}:true")]
    [AutoParams(false, false, true,  "AT_MOST_1{1/3}:true(1x underlying model is true)")]
    [AutoParams(false, true,  false, "AT_MOST_1{1/3}:true(1x underlying model is true)")]
    [AutoParams(false, true,  true,  "AT_MOST_1{2/3}:false(2x underlying model is true)")]
    [AutoParams(true,  false, false, "AT_MOST_1{1/3}:true(1x underlying model is true)")]
    [AutoParams(true,  false, true,  "AT_MOST_1{2/3}:false(2x underlying model is true)")]
    [AutoParams(true,  true,  false, "AT_MOST_1{2/3}:false(2x underlying model is true)")]
    [AutoParams(true,  true,  true,  "AT_MOST_1{3/3}:false(3x underlying model is true)")]
    public void Should_serialize_the_result_of_the_all_operation(
        bool first, 
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("underlying model");

        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAtMostNSatisfiedSpec(1);
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "AT_MOST_1(underlying spec description)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue("underlying model is true")
            .YieldWhenFalse("underlying model is false")
            .CreateSpec("underlying spec description");

        var sut = underlyingSpec.ToAtMostNSatisfiedSpec(1);

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "AT_MOST_1(True)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        var sut = underlyingSpec.ToAtMostNSatisfiedSpec(1);

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [AutoParams]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpec<object, string>(
            "throws",
            new Exception("should be wrapped"));

        var sut = throwingSpec.ToAtMostNSatisfiedSpec(1);

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}