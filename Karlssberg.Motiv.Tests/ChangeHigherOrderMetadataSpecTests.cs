using FluentAssertions;
using Humanizer;

namespace Karlssberg.Motiv.Tests;

public class ChangeHigherOrderMetadataSpecTests
{
    [Theory]
    [AutoParams(1, 3, 5, 7, "is not a pair of even numbers")]
    [AutoParams(1, 3, 5, 8, "is not a pair of even numbers")]
    [AutoParams(1, 3, 6, 8, "is a pair of even numbers")]
    [AutoParams(1, 3, 5, 7, "is not a pair of even numbers")]
    public void Should_supplant_metadata_from_a_higher_order_spec(int first, int second, int third, int fourth, string expected)
    {
        var underlyingSpec = Spec
            .Build<int>(i => i % 2 == 0)
            .YieldWhenTrue(i => $"{i} is even")
            .YieldWhenFalse(i => $"{i} is odd")
            .CreateSpec("is even spec");

        var sut = underlyingSpec
            .ToNSatisfiedSpec(2, "a pair of even numbers")
            .YieldWhenTrue("is a pair of even numbers")
            .YieldWhenFalse("is not a pair of even numbers");

        var result = sut.IsSatisfiedBy([first, second, third, fourth]);
        
        result.Reasons.Should().BeEquivalentTo(expected);
    }
    
    public void Should_provide_a_description()
    {
        var underlyingSpec = Spec
            .Build<int>(i => i % 2 == 0)
            .YieldWhenTrue("is even")
            .YieldWhenFalse("is odd")
            .CreateSpec("is even spec");

        var sut = underlyingSpec
            .ToNSatisfiedSpec(2, "a pair of even numbers")
            .YieldWhenTrue("is a pair of even numbers")
            .YieldWhenFalse("is not a pair of even numbers");

        sut.Description.Should().Be("is even spec");
    }
}