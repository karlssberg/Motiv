using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AllSpecTests
{
    [Theory]
    [InlineAutoData(false, false, false, false)]
    [InlineAutoData(false, false, true, false)]
    [InlineAutoData(false, true, false, false)]
    [InlineAutoData(false, true, true, false)]
    [InlineAutoData(true, false, false, false)]
    [InlineAutoData(true, false, true, false)]
    [InlineAutoData(true, true, false, false)]
    [InlineAutoData(true, true, true, true)]
    public void Should_perform_the_logical_operation_All(
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

        var sut = underlyingSpec
            .All("all booleans are true")
            .Yield((allSatisfied, results) =>
                $"{results.Count(result => result == allSatisfied)} are {allSatisfied.ToString().ToLowerInvariant()}");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "ALL{0/3}:false(false x3)")]
    [InlineAutoData(false, false, true, "ALL{1/3}:false(false x2)")]
    [InlineAutoData(false, true, false, "ALL{1/3}:false(false x2)")]
    [InlineAutoData(false, true, true, "ALL{2/3}:false(false)")]
    [InlineAutoData(true, false, false, "ALL{1/3}:false(false x2)")]
    [InlineAutoData(true, false, true, "ALL{2/3}:false(false)")]
    [InlineAutoData(true, true, false, "ALL{2/3}:false(false)")]
    [InlineAutoData(true, true, true, "ALL{3/3}:true(true x3)")]
    public void Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString().ToLowerInvariant())
            .YieldWhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();

        bool[] models = [first, second, third];

        var sut = underlyingSpec.All("all booleans are true");
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "ALL{0/3}:false(false x3)")]
    [InlineAutoData(false, false, true, "ALL{1/3}:false(false x2)")]
    [InlineAutoData(false, true, false, "ALL{1/3}:false(false x2)")]
    [InlineAutoData(false, true, true, "ALL{2/3}:false(false)")]
    [InlineAutoData(true, false, false, "ALL{1/3}:false(false x2)")]
    [InlineAutoData(true, false, true, "ALL{2/3}:false(false)")]
    [InlineAutoData(true, true, false, "ALL{2/3}:false(false)")]
    [InlineAutoData(true, true, true, "ALL{3/3}:true(true x3)")]
    public void
        Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
            bool first,
            bool second,
            bool third,
            string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString().ToLowerInvariant())
            .YieldWhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();
        ;

        bool[] models = [first, second, third];

        var sut = underlyingSpec.All("all booleans are true");
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "ALL{0/3}:false(model is false x3)")]
    [InlineAutoData(false, false, true, "ALL{1/3}:false(model is false x2)")]
    [InlineAutoData(false, true, false, "ALL{1/3}:false(model is false x2)")]
    [InlineAutoData(false, true, true, "ALL{2/3}:false(model is false)")]
    [InlineAutoData(true, false, false, "ALL{1/3}:false(model is false x2)")]
    [InlineAutoData(true, false, true, "ALL{2/3}:false(model is false)")]
    [InlineAutoData(true, true, false, "ALL{2/3}:false(model is false)")]
    [InlineAutoData(true, true, true, "ALL{3/3}:true(model is true x3)")]
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
            .CreateSpec("model");

        bool[] models = [first, second, third];

        var sut = underlyingSpec.All("all booleans are true");
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }


    [Theory]
    [InlineAutoData(false, false, false, "ALL{0/3}:false(left is false x3 and right is false x3)")]
    [InlineAutoData(false, false, true, "ALL{1/3}:false(left is false x2 and right is false x2)")]
    [InlineAutoData(false, true, false, "ALL{1/3}:false(left is false x2 and right is false x2)")]
    [InlineAutoData(false, true, true, "ALL{2/3}:false(left is false and right is false)")]
    [InlineAutoData(true, false, false, "ALL{1/3}:false(left is false x2 and right is false x2)")]
    [InlineAutoData(true, false, true, "ALL{2/3}:false(left is false and right is false)")]
    [InlineAutoData(true, true, false, "ALL{2/3}:false(left is false and right is false)")]
    [InlineAutoData(true, true, true, "ALL{3/3}:true(left is true x3 and right is true x3)")]
    public void Should_serialize_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpecLeft = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("left");

        var underlyingSpecRight = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("right");

        var underlyingSpec = underlyingSpecLeft & underlyingSpecRight;

        bool[] models = [first, second, third];

        var sut = underlyingSpec.All("all booleans are true");
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "<all booleans are true>(is true or false)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("is true or false");

        var sut = underlyingSpec
            .All("all booleans are true");

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
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
            .All("high-level description")
            .YieldWhenTrue(true)
            .YieldWhenFalse(false);

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "<all booleans are true>(True)";  
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        var sut = underlyingSpec
            .All("all booleans are true")
            .YieldWhenTrue(true)
            .YieldWhenFalse(false);

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

        var sut = throwingSpec
            .All("all booleans are true") 
            .YieldWhenTrue(results => $"{results.Count()} true")
            .YieldWhenFalse(results => $"{results.Count()} false");

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>()
            .Where(ex => ex.Message.Contains("should be wrapped"));
    }
}