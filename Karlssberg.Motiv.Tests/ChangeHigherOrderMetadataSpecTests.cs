using FluentAssertions;
using Humanizer;

namespace Karlssberg.Motiv.Tests;

public class ChangeHigherOrderMetadataSpecTests
{
    [Theory]
    [InlineAutoData(1, 3, 5, 7, "is not a pair of even numbers")]
    [InlineAutoData(1, 3, 5, 8, "is not a pair of even numbers")]
    [InlineAutoData(1, 3, 6, 8, "is a pair of even numbers")]
    [InlineAutoData(1, 3, 5, 7, "is not a pair of even numbers")]
    public void Should_supplant_metadata_from_a_higher_order_spec(int first, int second, int third, int fourth, string expected)
    {
        var underlyingSpec = Spec
            .Build<int>(i => i % 2 == 0)
            .WhenTrue(i => $"{i} is even")
            .WhenFalse(i => $"{i} is odd")
            .CreateSpec("is even spec");

        var sut = Spec
            .Build(underlyingSpec)
            .AsNSatisfied(2)
            .WhenTrue("is a pair of even numbers")
            .WhenFalse("is not a pair of even numbers")
            .CreateSpec();

        var result = sut.IsSatisfiedBy([first, second, third, fourth]);
        
        result.Explanation.Assertions.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Should_preserve_the_description_of_the_underlying_()
    {
        var underlyingSpec = Spec
            .Build<int>(i => i % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .CreateSpec("is even spec");

        var sut = Spec
            .Build(underlyingSpec)
            .AsNSatisfied(2)
            .WhenTrue("is a pair of even numbers")
            .WhenFalse("is not a pair of even numbers")
            .CreateSpec();

        sut.Proposition.Assertion.Should().Be("is a pair of even numbers");
    }

    [Theory]
    [InlineAutoData(true, true, true, "third true yield")]
    [InlineAutoData(true, true, false, "third false yield")]
    [InlineAutoData(true, false, true, "third false yield")]
    [InlineAutoData(true, false, false, "third false yield")]
    [InlineAutoData(false, true, true, "third false yield")]
    [InlineAutoData(false, true, false, "third false yield")]
    [InlineAutoData(false, false, true, "third false yield")]
    [InlineAutoData(false, false, false, "third false yield")]
    public void Should_only_yield_the_most_recent_when_multiple_yields_are_chained(bool first, bool second, bool third, string expected)
    {
        var underlying = Spec
            .Build<bool>(b => b)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .CreateSpec("is even spec");

        var firstSpec = Spec  
            .Build(underlying)
            .AsAllSatisfied()
            .WhenTrue("first true yield")
            .WhenFalse("first false yield")
            .CreateSpec("first spec");
            
        var secondSpec = Spec
            .Build(firstSpec)
            .WhenTrue("second true yield")
            .WhenFalse("second false yield")
            .CreateSpec("second spec");
            
        var sut = Spec
            .Build(secondSpec)
            .WhenTrue("third true yield")
            .WhenFalse("third false yield")
            .CreateSpec("third spec");

        var result = sut.IsSatisfiedBy([first, second, third]);
        
        result.Explanation.Assertions.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineAutoData(true, true, true, "is even")]
    [InlineAutoData(true, true, false, "is odd")]
    [InlineAutoData(true, false, true, "is odd")]
    [InlineAutoData(true, false, false, "is odd")]
    [InlineAutoData(false, true, true, "is odd")]
    [InlineAutoData(false, true, false, "is odd")]
    [InlineAutoData(false, false, true, "is odd")]
    [InlineAutoData(false, false, false, "is odd")]
    public void Should_yield_the_most_deeply_nested_reason_when_requested(bool first, bool second, bool third, string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(b => b)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .CreateSpec("is even spec");

        var firstSpec = Spec
            .Build(underlyingSpec).AsAllSatisfied()
            .WhenTrue("first true yield")
            .WhenFalse("first false yield")
            .CreateSpec("all even");
            
        var secondSpec = Spec
            .Build(firstSpec)
            .WhenTrue("second true yield")
            .WhenFalse("second false yield")
            .CreateSpec("all even");
            
        var sut = Spec
            .Build(secondSpec)
            .WhenTrue("third true yield")
            .WhenFalse("third false yield")
            .CreateSpec("all even");

        var result = sut.IsSatisfiedBy([first, second, third]);
        
        result.Explanation.DeepAssertions.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineAutoData(2, 4, 6, 8, true, "all even")]
    [InlineAutoData(2, 4, 6, 9, false, "not all even: 9 is odd")]
    [InlineAutoData(1, 4, 6, 9, false, "not all even: 1 and 9 are odd")]
    [InlineAutoData(1, 3, 6, 9, false, "not all even: 1, 3, and 9 are odd")]
    [InlineAutoData(1, 3, 5, 9, false, "not all even: 1, 3, 5, and 9 are odd")]
    public void Should_allow_regular_true_yield_to_be_used_with_a_higher_order_yield_false(
        int first, 
        int second, 
        int third,
        int forth,
        bool expected,
        string expectedReason)
    {
        var underlyingSpec = Spec
            .Build<int>(i => i % 2 == 0)
            .WhenTrue(i => $"{i} is even")
            .WhenFalse(i => $"{i} is odd")
            .CreateSpec("is even spec");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue("all even")
            .WhenFalse(results =>
            {
                var serializedModels = results.CausalModels.Humanize();
                var modelCount = results.CausalModels.Count();
                var isOrAre = "is".ToQuantity(modelCount, ShowQuantityAs.None);
                
                return $"not all even: {serializedModels} {isOrAre} odd";
            })
            .CreateSpec();

        var act = sut.IsSatisfiedBy([first, second, third, forth]);
            
        act.Explanation.Assertions.Should().BeEquivalentTo(expectedReason);
        act.Satisfied.Should().Be(expected);
    }
}