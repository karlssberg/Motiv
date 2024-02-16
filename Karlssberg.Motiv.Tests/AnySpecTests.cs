using FluentAssertions;
using Humanizer;

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
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        bool[] models = [first, second, third];

        var sut = Spec.Build(underlyingSpec)
            .AsAnySatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("any satisfied");
        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }
    
    [Fact]
    public void Should_provide_a_high_level_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "<high-level description>(boolean is true)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue("boolean is true")   
            .WhenFalse("boolean is false")
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec)
            .AsAnySatisfied()   
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("high-level description");

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "<any satisfied>{0/3}:false('model' is false x3)")]
    [InlineAutoData(false, false, true,  "<any satisfied>{1/3}:true('model' is true x1)")]
    [InlineAutoData(false, true, false,  "<any satisfied>{1/3}:true('model' is true x1)")]
    [InlineAutoData(false, true, true,   "<any satisfied>{2/3}:true('model' is true x2)")]
    [InlineAutoData(true, false, false,  "<any satisfied>{1/3}:true('model' is true x1)")]
    [InlineAutoData(true, false, true,   "<any satisfied>{2/3}:true('model' is true x2)")]
    [InlineAutoData(true, true, false,   "<any satisfied>{2/3}:true('model' is true x2)")]
    [InlineAutoData(true, true, true,    "<any satisfied>{3/3}:true('model' is true x3)")]
    public void Should_serialize_the_result_of_the_any_operation(
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
            .AsAnySatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("any satisfied");
            
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "<all true>{0/3}:false(False x3)")]
    [InlineAutoData(false, false, true,  "<all true>{1/3}:true(True x1)")]
    [InlineAutoData(false, true, false,  "<all true>{1/3}:true(True x1)")]
    [InlineAutoData(false, true, true,   "<all true>{2/3}:true(True x2)")]
    [InlineAutoData(true, false, false,  "<all true>{1/3}:true(True x1)")]
    [InlineAutoData(true, false, true,   "<all true>{2/3}:true(True x2)")]
    [InlineAutoData(true, true, false,   "<all true>{2/3}:true(True x2)")]
    [InlineAutoData(true, true, true,    "<all true>{3/3}:true(True x3)")]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("returns the model");

        bool[] models = [first, second, third];

        var sut = Spec
            .Build(underlyingSpec)
            .AsAnySatisfied()
            .WhenTrue("all true")
            .WhenFalse("some false")
            .CreateSpec();

        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "<all true>{0/3}:false(False x3)")]
    [InlineAutoData(false, false, true,  "<all true>{1/3}:true(True x1)")]
    [InlineAutoData(false, true, false,  "<all true>{1/3}:true(True x1)")]
    [InlineAutoData(false, true, true,   "<all true>{2/3}:true(True x2)")]
    [InlineAutoData(true, false, false,  "<all true>{1/3}:true(True x1)")]
    [InlineAutoData(true, false, true,   "<all true>{2/3}:true(True x2)")]
    [InlineAutoData(true, true, false,   "<all true>{2/3}:true(True x2)")]
    [InlineAutoData(true, true, true,    "<all true>{3/3}:true(True x3)")]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("is true");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAnySatisfied()
            .WhenTrue(results => results.Reasons.Humanize())
            .WhenFalse(results => results.Reasons.Humanize())
            .CreateSpec("all true");
        var result = sut.IsSatisfiedBy([first, second, third]);

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

        var sut = 
            Spec.Build(throwingSpec)
                .AsAnySatisfied()
                .WhenTrue("any true")
                .WhenFalse("all false")
                .CreateSpec();

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}