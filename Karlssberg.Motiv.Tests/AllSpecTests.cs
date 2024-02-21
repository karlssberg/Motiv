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
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        bool[] models = [first, second, third];

        var sut = Spec
            .Build(underlyingSpec).AsAllSatisfied()
            .WhenTrue(results => $"{results.Count()} are true")
            .WhenFalse(results => $"{results.Count()} are false")
            .CreateSpec("all are true");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "<all are true>{0/3}:false(false x3)")]
    [InlineAutoData(false, false, true, "<all are true>{1/3}:false(false x2)")]
    [InlineAutoData(false, true, false, "<all are true>{1/3}:false(false x2)")]
    [InlineAutoData(false, true, true, "<all are true>{2/3}:false(false x1)")]
    [InlineAutoData(true, false, false, "<all are true>{1/3}:false(false x2)")]
    [InlineAutoData(true, false, true, "<all are true>{2/3}:false(false x1)")]
    [InlineAutoData(true, true, false, "<all are true>{2/3}:false(false x1)")]
    [InlineAutoData(true, true, true, "<all are true>{3/3}:true(true x3)")]
    public void Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec).AsAllSatisfied()
            .WhenTrue(results => results.Metadata)
            .WhenFalse(results => results.Metadata)
            .CreateSpec("all are true");
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "<all are true>{0/3}:false(false x3)")]
    [InlineAutoData(false, false, true, "<all are true>{1/3}:false(false x2)")]
    [InlineAutoData(false, true, false, "<all are true>{1/3}:false(false x2)")]
    [InlineAutoData(false, true, true, "<all are true>{2/3}:false(false x1)")]
    [InlineAutoData(true, false, false, "<all are true>{1/3}:false(false x2)")]
    [InlineAutoData(true, false, true, "<all are true>{2/3}:false(false x1)")]
    [InlineAutoData(true, true, false, "<all are true>{2/3}:false(false x1)")]
    [InlineAutoData(true, true, true, "<all are true>{3/3}:true(true x3)")]
    public void
        Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
            bool first,
            bool second,
            bool third,
            string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec).AsAllSatisfied()
            .WhenTrue(results => results.Metadata)
            .WhenFalse(results => results.Metadata)
            .CreateSpec("all are true");
        

        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "<'all are true' is false>{0/3}('model' is false x3)")]
    [InlineAutoData(false, false, true,  "<'all are true' is false>{1/3}('model' is false x2)")]
    [InlineAutoData(false, true, false,  "<'all are true' is false>{1/3}('model' is false x2)")]
    [InlineAutoData(false, true, true,   "<'all are true' is false>{2/3}('model' is false x1)")]
    [InlineAutoData(true, false, false,  "<'all are true' is false>{1/3}('model' is false x2)")]
    [InlineAutoData(true, false, true,   "<'all are true' is false>{2/3}('model' is false x1)")]
    [InlineAutoData(true, true, false,   "<'all are true' is false>{2/3}('model' is false x1)")]
    [InlineAutoData(true, true, true,    "<'all are true' is true>{3/3}('model' is true x3)")]
    public void Should_serialize_the_result_of_the_all_operation(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue(results => results.Metadata)
            .WhenFalse(results => results.Metadata)
            .CreateSpec("all are true");
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.DebuggerDisplay().Should().Be(expected);
    }


    [Theory]
    [InlineAutoData(false, false, false, "<'all are true' is false>{0/3}('left' is false x3, 'right' is false x3)")]
    [InlineAutoData(false, false, true, "<'all are true' is false>{1/3}('left' is false x2, 'right' is false x2)")]
    [InlineAutoData(false, true, false, "<'all are true' is false>{1/3}('left' is false x2, 'right' is false x2)")]
    [InlineAutoData(false, true, true, "<'all are true' is false>{2/3}('left' is false x1, 'right' is false x1)")]
    [InlineAutoData(true, false, false, "<'all are true' is false>{1/3}('left' is false x2, 'right' is false x2)")]
    [InlineAutoData(true, false, true, "<'all are true' is false>{2/3}('left' is false x1, 'right' is false x1)")]
    [InlineAutoData(true, true, false, "<'all are true' is false>{2/3}('left' is false x1, 'right' is false x1)")]
    [InlineAutoData(true, true, true, "<'all are true' is true>{3/3}('left' is true x3, 'right' is true x3)")]
    public void Should_serialize_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpecLeft = Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("left");

        var underlyingSpecRight = Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("right");

        var sut = Spec 
            .Build(underlyingSpecLeft & underlyingSpecRight)
            .AsAllSatisfied()
            .WhenTrue(results => results.Metadata)
            .WhenFalse(results => results.Metadata)
            .CreateSpec("all are true");

        bool[] models = [first, second, third];
        var result = sut.IsSatisfiedBy(models);

        result.DebuggerDisplay().Should().Be(expected);
    }


    [Theory]
    [InlineAutoData(false, false, false, "'all are true' is false")]
    [InlineAutoData(false, false, true,  "'all are true' is false")]
    [InlineAutoData(false, true, false,  "'all are true' is false")]
    [InlineAutoData(false, true, true,   "'all are true' is false")]
    [InlineAutoData(true, false, false,  "'all are true' is false")]
    [InlineAutoData(true, false, true,   "'all are true' is false")]
    [InlineAutoData(true, true, false,   "'all are true' is false")]
    [InlineAutoData(true, true, true,    "'all are true' is true")]
    public void Should_Describe_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpecLeft = Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("left");

        var underlyingSpecRight = Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("right");

        var sut = Spec 
            .Build(underlyingSpecLeft & underlyingSpecRight)
            .AsAllSatisfied()
            .WhenTrue(results => results.Metadata)
            .WhenFalse(results => results.Metadata)
            .CreateSpec("all are true");

        bool[] models = [first, second, third];
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
        result.ToString().Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "<all booleans are true>(is true or false)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("is true or false");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue(results => $"{results.Count()} true")
            .WhenFalse(results => $"{results.Count()} false")
            .CreateSpec("all booleans are true");

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }
    

    [Fact]
    public void Should_provide_a_debugger_description_of_the_specification()
    {
        const string expected = "<all booleans are true>(is true or false)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("is true or false");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue(results => $"{results.Count()} true")
            .WhenFalse(results => $"{results.Count()} false")
            .CreateSpec("all booleans are true");

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }
    [Fact]
    public void Should_provide_a_high_level_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "<high-level description>(True)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec).AsAllSatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("high-level description");

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "<all are true>(is true)";  
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("all are true");

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

        var sut = Spec
            .Build(throwingSpec).AsAllSatisfied()
            .WhenTrue(results => $"{results.Count()} true")
            .WhenFalse(results => $"{results.Count()} false")
            .CreateSpec("all booleans are true");

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>()
            .Where(ex => ex.Message.Contains("should be wrapped"));
    }
}