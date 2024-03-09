using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class NotSpecTests
{
    [Theory]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    public void Should_perform_logical_not(
        bool operand,
        bool expected,
        object model)
    {
        var spec = Spec
            .Build<object>(_ => operand)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec($"is {operand}");

        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);

        result.Satisfied.Should().Be(expected);
        result.MetadataTree.Should().AllBeEquivalentTo(operand);
    }

    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "!is true")]
    public void Should_serialize_the_result_of_the_not_operation(
        bool operand,
        string expected,
        object model)
    {
        var spec = Spec
            .Build<object>(_ => operand)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("is true");

        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);

        result.Reason.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, "True")]
    [InlineAutoData(false, "False")]
    public void Should_serialize_the_result_of_the_not_operation_when_metadata_is_a_string(
        bool operand,
        string expected,
        object model)
    {
        var spec = Spec
            .Build<object>(_ => operand)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);

        result.Reason.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, "True")]
    [InlineAutoData(false, "False")]
    public void Should_serialize_the_result_of_the_not_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool operand,
        string expected,
        object model)
    {
        var spec = Spec
            .Build<object>(_ => operand)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        var sut = !spec;

        var result = sut.IsSatisfiedBy(model);

        result.Reason.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpec<object, string>(
            "should always throw",
            new Exception("should be wrapped"));

        var sut = !throwingSpec;

        var act = () => sut.IsSatisfiedBy(model);

        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}