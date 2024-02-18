using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class OrSpecTests
{
    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public void Should_perform_logical_and(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        var left = Spec
            .Build<object>(m => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("left");

        var right = Spec
            .Build<object>(m => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("right");

        var sut = left | right;

        var result = sut.IsSatisfiedBy(model);

        result.Satisfied.Should().Be(expected);
        result.Metadata.Should().AllBeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true, true, "('left' is true) OR:true ('right' is true)")]
    [InlineAutoData(true, false, "('left' is true) OR:true ('right' is false)")]
    [InlineAutoData(false, true, "('left' is false) OR:true ('right' is true)")]
    [InlineAutoData(false, false, "('left' is false) OR:false ('right' is false)")]
    public void Should_serialize_the_result_of_the_or_operation(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        var left = Spec
            .Build<object>(m => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("left");

        var right = Spec
            .Build<object>(m => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("right");

        var sut = left | right;

        var result = sut.IsSatisfiedBy(model);

        result.Description.Should().Be(expected);
    }


    [Theory]
    [InlineAutoData(true, true, "(True) OR:true (True)")]
    [InlineAutoData(true, false, "(True) OR:true (False)")]
    [InlineAutoData(false, true, "(False) OR:true (True)")]
    [InlineAutoData(false, false, "(False) OR:false (False)")]
    public void Should_serialize_the_result_of_the_or_operation_when_metadata_is_a_string(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("left");


        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("right");

        var sut = left | right;

        var result = sut.IsSatisfiedBy(model);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true, "(True) OR:true (True)")]
    [InlineAutoData(true, false, "(True) OR:true (False)")]
    [InlineAutoData(false, true, "(False) OR:true (True)")]
    [InlineAutoData(false, false, "(False) OR:false (False)")]
    public void Should_serialize_the_result_of_the_or_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        var sut = left | right;

        var result = sut.IsSatisfiedBy(model);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_provide_a_description_of_the_specification(bool leftResult, bool rightResult)
    {
        var left = Spec
            .Build<object>(m => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("left");

        var right = Spec
            .Build<object>(m => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("right");

        var expected = $"({left.Description}) | ({right.Description})";

        var sut = left | right;

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_provide_a_description_of_the_specification_when_using_convenience_specification(bool leftResult, bool rightResult)
    {
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        var expected = $"({left.Description}) | ({right.Description})";

        var sut = left | right;

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineAutoData]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var normalSpec = Spec
            .Build<object>(m => true)
            .WhenTrue("true")
            .WhenFalse("false")
            .CreateSpec();

        var throwingSpec = new ThrowingSpec<object, string>(
            "should always throw",
            new Exception("should be wrapped"));
        var sut = throwingSpec | normalSpec;

        var act = () => sut.IsSatisfiedBy(model);

        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}