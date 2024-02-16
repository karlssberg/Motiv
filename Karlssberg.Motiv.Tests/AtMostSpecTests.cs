using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AtMostSpecTests
{
    [Theory]
    [InlineAutoData(false, false, false, false, true )]
    [InlineAutoData(false, false, false, true,  false)]
    [InlineAutoData(false, false, true,  false, false)]
    [InlineAutoData(false, false, true,  true,  false)]
    [InlineAutoData(false, true,  false, false, false)]
    [InlineAutoData(false, true,  false, true,  false)]
    [InlineAutoData(false, true,  true,  false, false)]
    [InlineAutoData(false, true,  true,  true,  false)]
    [InlineAutoData(true,  false, false, false, false)]
    [InlineAutoData(true,  false, false, true,  false)]
    [InlineAutoData(true,  false, true,  false, false)]
    [InlineAutoData(true,  false, true,  true,  false)]
    [InlineAutoData(true,  true,  false, false, false)]
    [InlineAutoData(true,  true,  false, true,  false)]
    [InlineAutoData(true,  true,  true,  false, false)]
    [InlineAutoData(true,  true,  true,  true,  false)]
    public void Should_perform_the_logical_operation_at_most_when_0_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("returns the model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(0)
            .WhenTrue("none are satisfied")
            .WhenFalse("one or more are satisfied")
            .CreateSpec();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, true )]
    [InlineAutoData(false, false, false, true,  true )]
    [InlineAutoData(false, false, true,  false, true )]
    [InlineAutoData(false, false, true,  true,  false)]
    [InlineAutoData(false, true,  false, false, true )]
    [InlineAutoData(false, true,  false, true,  false)]
    [InlineAutoData(false, true,  true,  false, false)]
    [InlineAutoData(false, true,  true,  true,  false)]
    [InlineAutoData(true,  false, false, false, true )]
    [InlineAutoData(true,  false, false, true,  false)]
    [InlineAutoData(true,  false, true,  false, false)]
    [InlineAutoData(true,  false, true,  true,  false)]
    [InlineAutoData(true,  true,  false, false, false)]
    [InlineAutoData(true,  true,  false, true,  false)]
    [InlineAutoData(true,  true,  true,  false, false)]
    [InlineAutoData(true,  true,  true,  true,  false)]
    public void Should_perform_the_logical_operation_at_most_when_1_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("returns the model");

        var sut = Spec.Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("one is satisfied")
            .WhenFalse("none or more than one is not satisfied")
            .CreateSpec();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, true )]
    [InlineAutoData(false, false, false, true,  true )]
    [InlineAutoData(false, false, true,  false, true )]
    [InlineAutoData(false, false, true,  true,  true )]
    [InlineAutoData(false, true,  false, false, true )]
    [InlineAutoData(false, true,  false, true,  true )]
    [InlineAutoData(false, true,  true,  false, true )]
    [InlineAutoData(false, true,  true,  true,  false)]
    [InlineAutoData(true,  false, false, false, true )]
    [InlineAutoData(true,  false, false, true,  true )]
    [InlineAutoData(true,  false, true,  false, true )]
    [InlineAutoData(true,  false, true,  true,  false)]
    [InlineAutoData(true,  true,  false, false, true )]
    [InlineAutoData(true,  true,  false, true,  false)]
    [InlineAutoData(true,  true,  true,  false, false)]
    [InlineAutoData(true,  true,  true,  true,  false)]
    public void Should_perform_the_logical_operation_at_most_when_2_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("returns the model");

        var sut = Spec.Build(underlyingSpec)
            .AsAtMostNSatisfied(2)
            .WhenTrue("at most two are satisfied")
            .WhenFalse("more than two are satisfied")
            .CreateSpec();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, true )]
    [InlineAutoData(false, false, false, true,  true )]
    [InlineAutoData(false, false, true,  false, true )]
    [InlineAutoData(false, false, true,  true,  true )]
    [InlineAutoData(false, true,  false, false, true )]
    [InlineAutoData(false, true,  false, true,  true )]
    [InlineAutoData(false, true,  true,  false, true )]
    [InlineAutoData(false, true,  true,  true,  true )]
    [InlineAutoData(true,  false, false, false, true )]
    [InlineAutoData(true,  false, false, true,  true )]
    [InlineAutoData(true,  false, true,  false, true )]
    [InlineAutoData(true,  false, true,  true,  true )]
    [InlineAutoData(true,  true,  false, false, true )]
    [InlineAutoData(true,  true,  false, true,  true )]
    [InlineAutoData(true,  true,  true,  false, true )]
    [InlineAutoData(true,  true,  true,  true,  true )]
    public void Should_perform_the_logical_operation_at_most_when_the_set_size_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("returns the model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(4)
            .WhenTrue("at most four are satisfied")
            .WhenFalse("more than four are satisfied")
            .CreateSpec();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "<at most one is satisfied>{0/3}:true")]
    [InlineAutoData(false, false, true,  "<at most one is satisfied>{1/3}:true(is satisfied x1)")]
    [InlineAutoData(false, true,  false, "<at most one is satisfied>{1/3}:true(is satisfied x1)")]
    [InlineAutoData(false, true,  true,  "<at most one is satisfied>{2/3}:false(is satisfied x2)")]
    [InlineAutoData(true,  false, false, "<at most one is satisfied>{1/3}:true(is satisfied x1)")]
    [InlineAutoData(true,  false, true,  "<at most one is satisfied>{2/3}:false(is satisfied x2)")]
    [InlineAutoData(true,  true,  false, "<at most one is satisfied>{2/3}:false(is satisfied x2)")]
    [InlineAutoData(true,  true,  true,  "<at most one is satisfied>{3/3}:false(is satisfied x3)")]
    public void Should_serialize_the_result_of_the_at_most_of_1_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue("is satisfied")
            .WhenFalse("is not satisfied")
            .CreateSpec("returns the model");;

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .CreateSpec();
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "<at most one is satisfied>{0/3}:true")]
    [InlineAutoData(false, false, true,  "<at most one is satisfied>{1/3}:true(True x1)")]
    [InlineAutoData(false, true,  false, "<at most one is satisfied>{1/3}:true(True x1)")]
    [InlineAutoData(false, true,  true,  "<at most one is satisfied>{2/3}:false(True x2)")]
    [InlineAutoData(true,  false, false, "<at most one is satisfied>{1/3}:true(True x1)")]
    [InlineAutoData(true,  false, true,  "<at most one is satisfied>{2/3}:false(True x2)")]
    [InlineAutoData(true,  true,  false, "<at most one is satisfied>{2/3}:false(True x2)")]
    [InlineAutoData(true,  true,  true,  "<at most one is satisfied>{3/3}:false(True x3)")]
    public void Should_serialize_the_result_of_the_at_most_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .CreateSpec();
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "<at most one is satisfied>{0/3}:true")]
    [InlineAutoData(false, false, true,  "<at most one is satisfied>{1/3}:true('underlying model' is true x1)")]
    [InlineAutoData(false, true,  false, "<at most one is satisfied>{1/3}:true('underlying model' is true x1)")]
    [InlineAutoData(false, true,  true,  "<at most one is satisfied>{2/3}:false('underlying model' is true x2)")]
    [InlineAutoData(true,  false, false, "<at most one is satisfied>{1/3}:true('underlying model' is true x1)")]
    [InlineAutoData(true,  false, true,  "<at most one is satisfied>{2/3}:false('underlying model' is true x2)")]
    [InlineAutoData(true,  true,  false, "<at most one is satisfied>{2/3}:false('underlying model' is true x2)")]
    [InlineAutoData(true,  true,  true,  "<at most one is satisfied>{3/3}:false('underlying model' is true x3)")]
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
            .CreateSpec("underlying model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .CreateSpec();
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "<at most one is satisfied>(underlying spec description)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue("underlying model is true")
            .WhenFalse("underlying model is false")
            .CreateSpec("underlying spec description");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .CreateSpec();

        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "<at most one is satisfied>(True)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .CreateSpec();

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
            .Build(throwingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .CreateSpec();

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}