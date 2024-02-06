using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AndSpecTests
{
    [Theory]
    [AutoParams(true, true, true)]
    [AutoParams(true, false, false)]
    [AutoParams(false, true, false)]
    [AutoParams(false, false, false)]
    public void Should_perform_logical_and(
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

        var sut = left & right;

        var result = sut.IsSatisfiedBy(model);

        result.Value.Should().Be(expected);
        result.GetMetadata().Should().AllBeEquivalentTo(expected);
    }

    [Theory]
    [AutoParams(true, true, "(left is true) AND:True (right is true)")]
    [AutoParams(true, false, "(left is true) AND:False (right is false)")]
    [AutoParams(false, true, "(left is false) AND:False (right is true)")]
    [AutoParams(false, false, "(left is false) AND:False (right is false)")]
    public void Should_serialize_the_result_of_the_and_operation(
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

        var sut = left & right;

        var result = sut.IsSatisfiedBy(model);


    }

    [Theory]
    [AutoParams(true, true, "(True) AND:true (True)")]
    [AutoParams(true, false, "(True) AND:false (False)")]
    [AutoParams(false, true, "(False) AND:false (True)")]
    [AutoParams(false, false, "(False) AND:false (False)")]
    public void Should_serialize_the_result_of_the_and_operation_when_metadata_is_a_string(
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

        var sut = left & right;

        var result = sut.IsSatisfiedBy(model);


    }

    [Theory]
    [AutoParams(true, true, "(True) AND:true (True)")]
    [AutoParams(true, false, "(True) AND:false (False)")]
    [AutoParams(false, true, "(False) AND:false (True)")]
    [AutoParams(false, false, "(False) AND:false (False)")]
    public void Should_serialize_the_result_of_the_and_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
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

        var sut = left & right;

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

        var expected = $"({left.Description}) & ({right.Description})";

        var sut = left & right;

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

        var left = Spec.Build<object>(m => leftResult)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
        ;

        var right = Spec.Build<object>(m => rightResult)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();
        ;

        var expected = $"({left.Description}) & ({right.Description})";

        var sut = left & right;

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [AutoParams]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        Func<object, bool> predicate = m => true;

        var normalSpec = predicate.ToSpec()
            .YieldWhenTrue("true")
            .YieldWhenFalse("false")
            .CreateSpec();

        var throwingSpec = new ThrowingSpec<object, string>(
            "should always throw",
            new Exception("should be wrapped"));

        var sut = throwingSpec & normalSpec;

        var act = () => sut.IsSatisfiedBy(model);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}