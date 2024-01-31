using FluentAssertions;
using Humanizer;

namespace Karlssberg.Motiv.Tests;

public class NSatisfiedSpecBuilderTests
{
    [Theory]
    [AutoParams(1, 2, 4, 6, false, "1 is false", "1 is odd")]
    [AutoParams(1, 3, 4, 6, false, "2 are false", "1 is odd and 3 is odd")]
    [AutoParams(1, 3, 4, 9, false, "3 are false", "1 is odd, 3 is odd, and 9 is odd")]
    [AutoParams(2, 4, 6, 8, true, "4 are true", "2 is even, 4 is even, 6 is even, and 8 is even")]
    public void Should_mirror_the_outcome_of_an_all_satisfied_spec_metadata(
        int first,
        int second,
        int third,
        int fourth,
        bool expected,
        string expectedSuperficialReasons,
        string expectedReason)
    {
        var underlyingSpec = Spec
            .Build<int>(n => n % 2 == 0)
            .YieldWhenTrue(n => $"{n} is even")
            .YieldWhenFalse(n => $"{n} is odd")
            .CreateSpec("is even spec");

        var sut = underlyingSpec
            .ToAllSatisfiedSpec("all numbers are even")
            .Yield(GenerateReason);

        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.IsSatisfied.Should().Be(expected);
        result.Reasons.Humanize().Should().Be(expectedReason);
        result.GetSuperficialReasons().Should().HaveCount(1);
        result.GetSuperficialReasons().Should().AllBeEquivalentTo(expectedSuperficialReasons);
    }

    private static string GenerateReason(bool allSatisfied, IEnumerable<BooleanResultWithModel<int, string>> results)
    {
        var count = results.Count(r => r.IsSatisfied == allSatisfied);
        var trueOrFalse = allSatisfied.ToString().ToLowerInvariant();

        return $"{"is".ToQuantity(count)} {trueOrFalse}";
    }

    [Fact]
    public void Should_mirror_the_outcome_of_an_all_satisfied_spec_metadata2()
    {
        var underlyingSpec = Spec
            .Build<int>(n => n % 2 == 0)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("is even");

        var sut = underlyingSpec
            .ToAllSatisfiedSpec("all numbers are even")
            .YieldWhenTrue(results => $"{results.Count()} are true")
            .YieldWhenFalse(results => $"{results.Count()} are false");

        var result = sut.IsSatisfiedBy([1, 3, 5]);

        result.IsSatisfied.Should().BeFalse();
        result.GetMetadata().Should().HaveCount(1);
        result.GetMetadata().Should().AllBeEquivalentTo("3 are false");
    }
}