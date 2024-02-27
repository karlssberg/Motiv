using FluentAssertions;
using Humanizer;
using Karlssberg.Motiv.HigherOrder;

namespace Karlssberg.Motiv.Tests;

public class ExactlySpecTests
{
    [Theory]
    [InlineAutoData(2, 4, true)]
    [InlineAutoData(4, 3, false)]
    [InlineAutoData(1, 2, false)]
    [InlineAutoData(1, 3, false)]
    public void Should_perform_the_logical_operation_NSatisfied(
        int first,
        int second,
        bool expected)
    {
        var isEven = Spec
            .Build<int>(i => i % 2 == 0)
            .WhenTrue(i => $"{i} is even")
            .WhenFalse(i => $"{i} is odd")
            .CreateSpec("is even spec");

        var sut = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue("is a pair of even numbers")
            .WhenFalse("is not a pair of even numbers")
            .CreateSpec();

        var result = sut.IsSatisfiedBy([first, second]);

        result.Satisfied.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(1, 3, 5, 7, false, "The pack does not contain exactly a pair of even numbers", "1 is odd, 3 is odd, 5 is odd, and 7 is odd")]
    [InlineAutoData(1, 3, 5, 6, false, "The pack does not contain exactly a pair of even numbers", "6 is even")]
    [InlineAutoData(1, 3, 4, 6, true, "4 and 6 are a pair of even numbers", "4 is even and 6 is even")]
    [InlineAutoData(1, 4, 6, 8, false, "The pack does not contain exactly a pair of even numbers", "4 is even, 6 is even, and 8 is even")]
    public void Should_mirror_the_outcome_of_an_all_satisfied_spec_metadata(
        int first,
        int second,
        int third,
        int fourth,
        bool expected,
        string expectedShallowReasons,
        string expectedDeepReason)
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => $"{n} is even")
            .WhenFalse(n => $"{n} is odd")
            .CreateSpec("is even spec");

        var sut = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue((results) =>
                $"{results.CausalModels.Humanize()} are a pair of even numbers")
            .WhenFalse("The pack does not contain exactly a pair of even numbers")
            .CreateSpec("a pair of even numbers");

        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
        result.Explanation.Assertions.Humanize().Should().Be(expectedShallowReasons);
        result.Explanation.DeepAssertions.Humanize().Should().Be(expectedDeepReason);
    }
    
    [Theory]
    [InlineAutoData(true, true, """
                                            2 even
                                                2x true
                                            """)]
    [InlineAutoData(true, false, """
                                            1 even and 1 odd
                                                1x true
                                            """)]
    [InlineAutoData(false, true, """
                                            1 even and 1 odd
                                                1x true
                                            """)]
    [InlineAutoData(false, false, """
                                            0 even and 2 odd
                                                2x false
                                            """)]
    public void Should_serialize_the_result_of_the_NSatisfied_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        string expected)
    {
        var isEven = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();

        var sut = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue("2 even")
            .WhenFalse(results => $"{results.TrueCount()} even and {results.FalseCount()} odd")
            .CreateSpec();
        
        var result = sut.IsSatisfiedBy([first, second]);

        result.Description.Detailed.Should().Be(expected);
    }

    private static string GenerateReason(bool allSatisfied, IEnumerable<BooleanResult<int, string>> results)
    {
        var count = results.Count(r => r.Satisfied == allSatisfied);
        var trueOrFalse = allSatisfied.ToString().ToLowerInvariant();

        return $"{"is".ToQuantity(count)} {trueOrFalse}";
    }
    
    [Fact]
    public void Should_describe_an_NSatisfied_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("is even");

        var sut = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("a pair of even numbers");

        sut.Proposition.Assertion.Should().Be("a pair of even numbers");
    }
}