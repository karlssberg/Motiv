using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ResultDescriptionTests
{
    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "is false")]
    public void Should_generate_a_simple_description_reason(bool isTrue, string expected)
    {
        var spec = Spec
            .Build<object>(m => isTrue)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .CreateSpec($"always {expected}");

        var result = spec.IsSatisfiedBy(new object());

        result.Description.Reason.Should().Be(expected);
    }  
    
    [Theory]
    [InlineAutoData(true, "always true")]
    [InlineAutoData(false, "!always true")]
    public void Should_generate_a_simple_description_using_proposition_when_metadata_is_not_a_string(
        bool isTrue,
        string expected,
        object model)
    {
        var spec = Spec
            .Build<object>(m => isTrue)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec($"always true");

        var result = spec.IsSatisfiedBy(model);

        result.Description.Reason.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, "!left is true | !right is true")]
    [InlineAutoData(false, true, "!left is true & right is true")]
    [InlineAutoData(true, false, "left is true & !right is true")]
    [InlineAutoData(true, true, "right is true | left is true")]
    public void Should_generate_a_description_from_a_composition(
        bool leftResult,
        bool rightResult,
        string expected,
        bool model)
    {
        var left = Spec
            .Build<bool>(_ => leftResult)
            .CreateSpec("left is true");
        
        var right = Spec
            .Build<bool>(_ => rightResult)
            .CreateSpec("right is true");
        
        var spec = (left & !right) | (!left & right);

        var result = spec.IsSatisfiedBy(model);

        result.Description.Reason.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, false, "(!first | !second) & (!third | !forth)")]
    [InlineAutoData(false, false, false, true,  "!first | !second")]
    [InlineAutoData(false, false, true, false,  "!first | !second")]
    [InlineAutoData(false, false, true, true,   "!first | !second")]
    [InlineAutoData(false, true, false, false,  "!third | !forth")]
    [InlineAutoData(false, true, false, true,   "second & forth")]
    [InlineAutoData(false, true, true, false,   "second & third")]
    [InlineAutoData(false, true, true, true,    "second & (third | forth)")]
    [InlineAutoData(true, false, false, false,  "!third | !forth")]
    [InlineAutoData(true, false, false, true,   "first & forth")]
    [InlineAutoData(true, false, true, false,   "first & third")]
    [InlineAutoData(true, false, true, true,    "first & (third | forth)")]
    [InlineAutoData(true, true, false, false,   "!third | !forth")]
    [InlineAutoData(true, true, false, true,    "(first | second) & forth")]
    [InlineAutoData(true, true, true, false,    "(first | second) & third")]
    [InlineAutoData(true, true, true, true,     "(first | second) & (third | forth)")]
    public void Should_generate_a_description_from_a_complicated_composition(
        bool firstValue,
        bool secondValue,
        bool thirdValue,
        bool forthValue,
        string expected,
        bool model)
    {
        var first = Spec
            .Build<bool>(_ => firstValue)
            .CreateSpec("first");
        
        var second = Spec
            .Build<bool>(_ => secondValue)
            .CreateSpec("second");
        
        var third = Spec
            .Build<bool>(_ => thirdValue)
            .CreateSpec("third");
        
        var forth = Spec
            .Build<bool>(_ => forthValue)
            .CreateSpec("forth");
        
        var spec = (first | second) & (third | forth);
        
        var result = spec.IsSatisfiedBy(model);

        result.Description.Reason.Should().Be(expected);
    }
}