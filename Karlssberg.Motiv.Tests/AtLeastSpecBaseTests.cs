using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AtLeastSpecBaseTests
{
    [Theory]
    [InlineAutoData(false, false, false, false, true)]
    [InlineAutoData(false, false, false, true, true)]
    [InlineAutoData(false, false, true, false, true)]
    [InlineAutoData(false, false, true, true, true)]
    [InlineAutoData(false, true, false, false, true)]
    [InlineAutoData(false, true, false, true, true)]
    [InlineAutoData(false, true, true, false, true)]
    [InlineAutoData(false, true, true, true, true)]
    [InlineAutoData(true, false, false, false, true)]
    [InlineAutoData(true, false, false, true, true)]
    [InlineAutoData(true, false, true, false, true)]
    [InlineAutoData(true, false, true, true, true)]
    [InlineAutoData(true, true, false, false, true)]
    [InlineAutoData(true, true, false, true, true)]
    [InlineAutoData(true, true, true, false, true)]
    [InlineAutoData(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_0_is_supplied_as_the_minimum(
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

        var sut = underlyingSpec.AtLeast(0);
        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, false)]
    [InlineAutoData(false, false, false, true, true)]
    [InlineAutoData(false, false, true, false, true)]
    [InlineAutoData(false, false, true, true, true)]
    [InlineAutoData(false, true, false, false, true)]
    [InlineAutoData(false, true, false, true, true)]
    [InlineAutoData(false, true, true, false, true)]
    [InlineAutoData(false, true, true, true, true)]
    [InlineAutoData(true, false, false, false, true)]
    [InlineAutoData(true, false, false, true, true)]
    [InlineAutoData(true, false, true, false, true)]
    [InlineAutoData(true, false, true, true, true)]
    [InlineAutoData(true, true, false, false, true)]
    [InlineAutoData(true, true, false, true, true)]
    [InlineAutoData(true, true, true, false, true)]
    [InlineAutoData(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_1_is_supplied_as_the_minimum(
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

        var sut = underlyingSpec.AtLeast(1);
        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, false)]
    [InlineAutoData(false, false, false, true, false)]
    [InlineAutoData(false, false, true, false, false)]
    [InlineAutoData(false, false, true, true, true)]
    [InlineAutoData(false, true, false, false, false)]
    [InlineAutoData(false, true, false, true, true)]
    [InlineAutoData(false, true, true, false, true)]
    [InlineAutoData(false, true, true, true, true)]
    [InlineAutoData(true, false, false, false, false)]
    [InlineAutoData(true, false, false, true, true)]
    [InlineAutoData(true, false, true, false, true)]
    [InlineAutoData(true, false, true, true, true)]
    [InlineAutoData(true, true, false, false, true)]
    [InlineAutoData(true, true, false, true, true)]
    [InlineAutoData(true, true, true, false, true)]
    [InlineAutoData(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_2_is_supplied_as_the_minimum(
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

        var sut = underlyingSpec.AtLeast(2);
        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, false)]
    [InlineAutoData(false, false, false, true, false)]
    [InlineAutoData(false, false, true, false, false)]
    [InlineAutoData(false, false, true, true, false)]
    [InlineAutoData(false, true, false, false, false)]
    [InlineAutoData(false, true, false, true, false)]
    [InlineAutoData(false, true, true, false, false)]
    [InlineAutoData(false, true, true, true, false)]
    [InlineAutoData(true, false, false, false, false)]
    [InlineAutoData(true, false, false, true, false)]
    [InlineAutoData(true, false, true, false, false)]
    [InlineAutoData(true, false, true, true, false)]
    [InlineAutoData(true, true, false, false, false)]
    [InlineAutoData(true, true, false, true, false)]
    [InlineAutoData(true, true, true, false, false)]
    [InlineAutoData(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_the_set_size_is_supplied_as_the_minimum(
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

        var sut = underlyingSpec.AtLeast(models.Length);
        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "AT_LEAST_1{3/3}:False(received false)")]
    [InlineAutoData(false, false, true,  "AT_LEAST_1{1/3}:True(received true")]
    [InlineAutoData(false, true,  false, "AT_LEAST_1{1/3}:True(received true)")]
    [InlineAutoData(false, true,  true,  "AT_LEAST_1{2/3}:True(received true)")]
    [InlineAutoData(true,  false, false, "AT_LEAST_1{1/3}:True(received true)")]
    [InlineAutoData(true,  false, true,  "AT_LEAST_1{2/3}:True(received true)")]
    [InlineAutoData(true,  true,  false, "AT_LEAST_1{2/3}:True(received true)")]
    [InlineAutoData(true,  true,  true,  "AT_LEAST_1{3/3}:True(received true)")]
    public void Should_serialize_the_result_of_the_at_least_of_1_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue("received true")
            .YieldWhenFalse("received false")
            .CreateSpec("returns the model");

        bool[] models = [first, second, third];

        var sut = underlyingSpec.AtLeast(1);
        var result = sut.IsSatisfiedBy(models);


    }

    [Theory]
    [InlineAutoData(false, false, false, "AT_LEAST_1{0/3}:false")]
    [InlineAutoData(false, false, true,  "AT_LEAST_1{1/3}:true(underlying satisfied)")]
    [InlineAutoData(false, true, false,  "AT_LEAST_1{1/3}:true(underlying satisfied)")]
    [InlineAutoData(false, true, true,   "AT_LEAST_1{2/3}:true(underlying satisfied x2)")]
    [InlineAutoData(true, false, false,  "AT_LEAST_1{1/3}:true(underlying satisfied)")]
    [InlineAutoData(true, false, true,   "AT_LEAST_1{2/3}:true(underlying satisfied x2)")]
    [InlineAutoData(true, true, false,   "AT_LEAST_1{2/3}:true(underlying satisfied x2)")]
    [InlineAutoData(true, true, true,    "AT_LEAST_1{3/3}:true(underlying satisfied x3)")]
    public void Should_serialize_the_result_of_the_at_least_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue("underlying satisfied")
            .YieldWhenFalse("underlying not satisfied")
            .CreateSpec();

        bool[] models = [first, second, third];

        var sut = underlyingSpec.AtLeast(1);
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "AT_LEAST_1{0/3}:false")]
    [InlineAutoData(false, false, true,  "AT_LEAST_1{1/3}:true(underlying model is true)")]
    [InlineAutoData(false, true, false,  "AT_LEAST_1{1/3}:true(underlying model is true)")]
    [InlineAutoData(false, true, true,   "AT_LEAST_1{2/3}:true(underlying model is true x2)")]
    [InlineAutoData(true, false, false,  "AT_LEAST_1{1/3}:true(underlying model is true)")]
    [InlineAutoData(true, false, true,   "AT_LEAST_1{2/3}:true(underlying model is true x2)")]
    [InlineAutoData(true, true, false,   "AT_LEAST_1{2/3}:true(underlying model is true x2)")]
    [InlineAutoData(true, true, true,    "AT_LEAST_1{3/3}:true(underlying model is true x3)")]
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

        var sut = underlyingSpec.AtLeast(1);
        var result = sut.IsSatisfiedBy(models);

          result.Description.Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "AT_LEAST_1(underlying spec description)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("underlying spec description");

        var sut = underlyingSpec.AtLeast(1);

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "AT_LEAST_1(True)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        var sut = underlyingSpec.AtLeast(1);

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineAutoData]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpec<object, string>(
            "throws",
            new Exception("should be wrapped"));

        var sut = throwingSpec.AtLeast(1);

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}