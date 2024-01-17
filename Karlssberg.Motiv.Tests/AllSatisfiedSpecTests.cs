using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AllSatisfiedSpecTests
{
    [Theory]
    [AutoParams(false, false, false, false)]
    [AutoParams(false, false, true, false)]
    [AutoParams(false, true, false, false)]
    [AutoParams(false, true, true, false)]
    [AutoParams(true, false, false, false)]
    [AutoParams(true, false, true, false)]
    [AutoParams(true, true, false, false)]
    [AutoParams(true, true, true, true)]
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
            .BuildAllSatisfiedSpec()
            .YieldWhenAnything((allSatisfied, results) =>
                $"{results.Count(r => r.IsSatisfied == allSatisfied)} are {allSatisfied.ToString().ToLowerInvariant()}")
            .CreateSpec();
        
        var result = sut.IsSatisfiedBy(models);

        result.IsSatisfied.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, "ALL:false(false, false, false)")]
    [AutoParams(false, false, true, "ALL:false(false, false, true)")]
    [AutoParams(false, true, false, "ALL:false(false, true, false)")]
    [AutoParams(false, true, true, "ALL:false(false, true, true)")]
    [AutoParams(true, false, false, "ALL:false(true, false, false)")]
    [AutoParams(true, false, true, "ALL:false(true, false, true)")]
    [AutoParams(true, true, false, "ALL:false(true, true, false)")]
    [AutoParams(true, true, true, "ALL:true(true, true, true)")]
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
            .CreateSpec();;

        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAllSatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, "ALL:false(false, false, false)")]
    [AutoParams(false, false, true, "ALL:false(false, false, true)")]
    [AutoParams(false, true, false, "ALL:false(false, true, false)")]
    [AutoParams(false, true, true, "ALL:false(false, true, true)")]
    [AutoParams(true, false, false, "ALL:false(true, false, false)")]
    [AutoParams(true, false, true, "ALL:false(true, false, true)")]
    [AutoParams(true, true, false, "ALL:false(true, true, false)")]
    [AutoParams(true, true, true, "ALL:true(true, true, true)")]
    public void Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString().ToLowerInvariant())
            .YieldWhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();;

        bool[] models = [first, second, third];

        var sut = underlyingSpec.ToAllSatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Theory]
    [AutoParams(false, false, false, "ALL:false(model:false, model:false, model:false)")]
    [AutoParams(false, false, true, "ALL:false(model:false, model:false, model:true)")]
    [AutoParams(false, true, false, "ALL:false(model:false, model:true, model:false)")]
    [AutoParams(false, true, true, "ALL:false(model:false, model:true, model:true)")]
    [AutoParams(true, false, false, "ALL:false(model:true, model:false, model:false)")]
    [AutoParams(true, false, true, "ALL:false(model:true, model:false, model:true)")]
    [AutoParams(true, true, false, "ALL:false(model:true, model:true, model:false)")]
    [AutoParams(true, true, true, "ALL:true(model:true, model:true, model:true)")]
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

        var sut = underlyingSpec.ToAllSatisfiedSpec();
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "ALL(underlying spec description)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec("underlying spec description");

        var sut = underlyingSpec
            .BuildAllSatisfiedSpec()
            .YieldWhenAllTrue("any true")
            .YieldWhenAnyFalse("any false")
            .CreateSpec();
        
        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "ALL(True)";
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        var sut = underlyingSpec
            .BuildAllSatisfiedSpec()
            .YieldWhenAnyTrue(true)
            .YieldWhenAllFalse(false)
            .CreateSpec();
            
        sut.Description.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }
    
    [Theory]
    [AutoParams]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpec<object, string>(
            "throws",
            new Exception("should be wrapped"));

        var sut = throwingSpec
            .BuildAllSatisfiedSpec()
            .YieldWhenAnyTrue(results => $"{results.Count()} true")
            .YieldWhenAnyFalse(results => $"{results.Count()} false")
            .CreateSpec();

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains(sut.Description));
        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("AllSatisfiedSpec<Object, String, String>"));
        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
}