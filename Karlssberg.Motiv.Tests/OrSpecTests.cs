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
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var sut = left | right;

        var result = sut.IsSatisfiedBy(model);

        result.Satisfied.Should().Be(expected);
        result.MetadataTree.Should().AllBeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true, true, "left | right")]
    [InlineAutoData(true, false, "left")]
    [InlineAutoData(false, true, "right")]
    [InlineAutoData(false, false, "!left | !right")]
    public void Should_serialize_the_result_of_the_or_operation(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var sut = left | right;

        var result = sut.IsSatisfiedBy(model);

        result.Reason.Should().Be(expected);
    }


    [Theory]
    [InlineAutoData(true, true, "left | right")]
    [InlineAutoData(true, false, "left")]
    [InlineAutoData(false, true, "right")]
    [InlineAutoData(false, false, "!left | !right")]
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
            .Create("left");


        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("right");

        var sut = left | right;

        var result = sut.IsSatisfiedBy(model);

        result.Reason.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true, "True | True")]
    [InlineAutoData(true, false, "True")]
    [InlineAutoData(false, true, "True")]
    [InlineAutoData(false, false, "False | False")]
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
            .Create();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var sut = left | right;

        var result = sut.IsSatisfiedBy(model);

        result.Reason.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_provide_a_description_of_the_specification(bool leftResult, bool rightResult)
    {
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var expected = $"{left.Proposition} | {right.Proposition}";

        var sut = left | right;

        sut.Proposition.Statement.Should().Be(expected);
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
            .Create();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var expected = $"{left.Proposition} | {right.Proposition}";

        var sut = left | right;

        sut.Proposition.Statement.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineAutoData]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var normalSpec = Spec
            .Build<object>(_ => true)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var throwingSpec = new ThrowingSpec<object, string>(
            "should always throw",
            new Exception("should be wrapped"));
        var sut = throwingSpec | normalSpec;

        var act = () => sut.IsSatisfiedBy(model);

        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
    
    

    [Theory]
    [InlineAutoData(false, false, 2)]
    [InlineAutoData(false, true, 1)]
    [InlineAutoData(true, false, 1)]
    [InlineAutoData(true, true, 2)]
    public void Should_accurately_report_the_number_of_causal_operands(bool left, bool right, int expected,
        object model)
    {
        var leftSpec = Spec
            .Build<object>(_ => left)
            .Create("left");

        var rightSpec = Spec
            .Build<object>(_ => right)
            .Create("right");

        var sut = leftSpec | rightSpec;

        var result = sut.IsSatisfiedBy(model);

        result.Description.CausalOperandCount.Should().Be(expected);
    }
}