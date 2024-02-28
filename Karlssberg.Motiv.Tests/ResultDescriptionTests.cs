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

        result.Reason.Should().Be(expected);
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

        result.Reason.Should().Be(expected);
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

        result.Reason.Should().Be(expected);
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
    public void Should_generate_a_description_from_a_complicated_composition_using_propositions(
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

        result.Reason.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, false, "(not first | not second) & (not third | not forth)")]
    [InlineAutoData(false, false, false, true,  "not first | not second")]
    [InlineAutoData(false, false, true, false,  "not first | not second")]
    [InlineAutoData(false, false, true, true,   "not first | not second")]
    [InlineAutoData(false, true, false, false,  "not third | not forth")]
    [InlineAutoData(false, true, false, true,   "is second & is forth")]
    [InlineAutoData(false, true, true, false,   "is second & is third")]
    [InlineAutoData(false, true, true, true,    "is second & (is third | is forth)")]
    [InlineAutoData(true, false, false, false,  "not third | not forth")]
    [InlineAutoData(true, false, false, true,   "is first & is forth")]
    [InlineAutoData(true, false, true, false,   "is first & is third")]
    [InlineAutoData(true, false, true, true,    "is first & (is third | is forth)")]
    [InlineAutoData(true, true, false, false,   "not third | not forth")]
    [InlineAutoData(true, true, false, true,    "(is first | is second) & is forth")]
    [InlineAutoData(true, true, true, false,    "(is first | is second) & is third")]
    [InlineAutoData(true, true, true, true,     "(is first | is second) & (is third | is forth)")]
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
            .WhenTrue("is first")
            .WhenFalse("not first")
            .CreateSpec();
        
        var second = Spec
            .Build<bool>(_ => secondValue)
            .WhenTrue("is second")
            .WhenFalse("not second")
            .CreateSpec();
        
        var third = Spec
            .Build<bool>(_ => thirdValue)
            .WhenTrue("is third")
            .WhenFalse("not third")
            .CreateSpec();
        
        var forth = Spec
            .Build<bool>(_ => forthValue)
            .WhenTrue("is forth")
            .WhenFalse("not forth")
            .CreateSpec();
        
        var spec = (first | second) & (third | forth);
        
        var result = spec.IsSatisfiedBy(model);

        result.Reason.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, false, 
        """
        some are false {
            1x (!first | !second) & (!third | !forth)
        }
        """)]
    [InlineAutoData(false, false, false, true,
        """
        some are false {
            1x !first | !second
        }
        """)]
    [InlineAutoData(false, false, true, false,
        """
        some are false {
            1x !first | !second
        }
        """)]
    [InlineAutoData(false, false, true, true,
        """
        some are false {
            1x !first | !second
        }
        """)]
    [InlineAutoData(false, true, false, false,
        """
        some are false {
            1x !third | !forth
        }
        """)]
    [InlineAutoData(false, true, false, true,
        """
        all are true {
            1x second & forth
        }
        """)]
    [InlineAutoData(false, true, true, false,
        """
        all are true {
            1x second & third
        }
        """)]
    [InlineAutoData(false, true, true, true,
        """
        all are true {
            1x second & (third | forth)
        }
        """)]
    [InlineAutoData(true, false, false, false,
        """
        some are false {
            1x !third | !forth
        }
        """)]
    [InlineAutoData(true, false, false, true,
        """
        all are true {
            1x first & forth
        }
        """)]
    [InlineAutoData(true, false, true, false,
        """
        all are true {
            1x first & third
        }
        """)]
    [InlineAutoData(true, false, true, true,
        """
        all are true {
            1x first & (third | forth)
        }
        """)]
    [InlineAutoData(true, true, false, false,
        """
        some are false {
            1x !third | !forth
        }
        """)]
    [InlineAutoData(true, true, false, true,
        """
        all are true {
            1x (first | second) & forth
        }
        """)]
    [InlineAutoData(true, true, true, false,
        """
        all are true {
            1x (first | second) & third
        }
        """)]
    [InlineAutoData(true, true, true, true,
        """
        all are true {
            1x (first | second) & (third | forth)
        }
        """)]
    public void Should_generate_a_description_from_a_complicated_composition_of_higher_order_spec(
        bool firstValue,
        bool secondValue,
        bool thirdValue,
        bool forthValue,  
        string expected)
    {
        var first = Spec
            .Build<bool>(val => firstValue & val)
            .CreateSpec("first");
        
        var second = Spec
            .Build<bool>(val => secondValue & val)
            .CreateSpec("second");
        
        var third = Spec
            .Build<bool>(val => thirdValue & val)
            .CreateSpec("third");
        
        var forth = Spec
            .Build<bool>(val => forthValue & val)
            .CreateSpec("forth");
        
        var underlying = (first | second) & (third | forth);
        var spec = Spec
            .Build(underlying)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("some are false")
            .CreateSpec();
        
        var result = spec.IsSatisfiedBy([true]);

        result.Description.Detailed.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, false, 
        """
        (!first | !second) &
        (!third | !forth)
        """)]
    [InlineAutoData(false, false, false, true,
        """
        (!first | !second) &
        (!third | forth)
        """)]
    [InlineAutoData(false, false, true, false,
        """
        (!first | !second) &
        (third | !forth)
        """)]
    [InlineAutoData(false, false, true, true,
        """
        (!first | !second) &
        (third | forth)
        """)]
    [InlineAutoData(false, true, false, false,
        """
        (!first | second) &
        (!third | !forth)
        """)]
    [InlineAutoData(false, true, false, true,
        """
        (!first | second) &
        (!third | forth)
        """)]
    [InlineAutoData(false, true, true, false,
        """
        (!first | second) &
        (third | !forth)
        """)]
    [InlineAutoData(false, true, true, true,
        """
        (!first | second) &
        (third | forth)
        """)]
    [InlineAutoData(true, false, false, false,
        """
        (first | !second) &
        (!third | !forth)
        """)]
    [InlineAutoData(true, false, false, true,
        """
        (first | !second) &
        (!third | forth)
        """)]
    [InlineAutoData(true, false, true, false,
        """
        (first | !second) &
        (third | !forth)
        """)]
    [InlineAutoData(true, false, true, true,
        """
        (first | !second) &
        (third | forth)
        """)]
    [InlineAutoData(true, true, false, false,
        """
        (first | second) &
        (!third | !forth)
        """)]
    [InlineAutoData(true, true, false, true,
        """
        (first | second) &
        (!third | forth)
        """)]
    [InlineAutoData(true, true, true, false,
        """
        (first | second) &
        (third | !forth)
        """)]
    [InlineAutoData(true, true, true, true,
        """
        (first | second) &
        (third | forth)
        """)]
    public void Should_generate_a_detailed_description_from_a_complicated_composition_of_a_first_order_expression(
        bool firstValue,
        bool secondValue,
        bool thirdValue,
        bool forthValue,  
        string expected)
    {
        var first = Spec
            .Build<bool>(val => firstValue & val)
            .CreateSpec("first");
        
        var second = Spec
            .Build<bool>(val => secondValue & val)
            .CreateSpec("second");
        
        var third = Spec
            .Build<bool>(val => thirdValue & val)
            .CreateSpec("third");
        
        var forth = Spec
            .Build<bool>(val => forthValue & val)
            .CreateSpec("forth");
        
        var spec = (first | !second) & !(third | !forth);
        var result = spec.IsSatisfiedBy(true);

        result.Description.Detailed.Should().Be(expected);
    }
    
}