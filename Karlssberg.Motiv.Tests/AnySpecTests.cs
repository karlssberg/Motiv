using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AnySpecTests
{
    [Theory]
    [InlineAutoData(false, false, false, false)]
    [InlineAutoData(false, false, true, true)]
    [InlineAutoData(false, true, false, true)]
    [InlineAutoData(false, true, true, true)]
    [InlineAutoData(true, false, false, true)]
    [InlineAutoData(true, false, true, true)]
    [InlineAutoData(true, true, false, true)]
    [InlineAutoData(true, true, true, true)]
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

        var sut = underlyingSpec.Any();
        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }
    
    [Fact]
    public void Should_provide_a_high_level_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "<high-level description>(True)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())   
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        var sut = underlyingSpec
            .Any("high-level description")
            .YieldWhenTrue(true)
            .YieldWhenFalse(false);

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "ANY{0/3}:false(model is false x3)")]
    [InlineAutoData(false, false, true,  "ANY{1/3}:true(model is true)")]
    [InlineAutoData(false, true, false,  "ANY{1/3}:true(model is true)")]
    [InlineAutoData(false, true, true,   "ANY{2/3}:true(model is true x2)")]
    [InlineAutoData(true, false, false,  "ANY{1/3}:true(model is true)")]
    [InlineAutoData(true, false, true,   "ANY{2/3}:true(model is true x2)")]
    [InlineAutoData(true, true, false,   "ANY{2/3}:true(model is true x2)")]
    [InlineAutoData(true, true, true,    "ANY{3/3}:true(model is true x3)")]
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

        var sut = underlyingSpec.Any();
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "ANY{0/3}:false(False x3)")]
    [InlineAutoData(false, false, true,  "ANY{1/3}:true(True)")]
    [InlineAutoData(false, true, false,  "ANY{1/3}:true(True)")]
    [InlineAutoData(false, true, true,   "ANY{2/3}:true(True x2)")]
    [InlineAutoData(true, false, false,  "ANY{1/3}:true(True)")]
    [InlineAutoData(true, false, true,   "ANY{2/3}:true(True x2)")]
    [InlineAutoData(true, true, false,   "ANY{2/3}:true(True x2)")]
    [InlineAutoData(true, true, true,    "ANY{3/3}:true(True x3)")]
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
            .Any();

        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "ANY{0/3}:false(False x3)")]
    [InlineAutoData(false, false, true,  "ANY{1/3}:true(True)")]
    [InlineAutoData(false, true, false,  "ANY{1/3}:true(True)")]
    [InlineAutoData(false, true, true,   "ANY{2/3}:true(True x2)")]
    [InlineAutoData(true, false, false,  "ANY{1/3}:true(True)")]
    [InlineAutoData(true, false, true,   "ANY{2/3}:true(True x2)")]
    [InlineAutoData(true, true, false,   "ANY{2/3}:true(True x2)")]
    [InlineAutoData(true, true, true,    "ANY{3/3}:true(True x3)")]
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

        var sut = underlyingSpec.Any();
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpec<object, string>(
            "should always throw",
            new Exception("should be wrapped"));

        var sut = throwingSpec.Any();

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}