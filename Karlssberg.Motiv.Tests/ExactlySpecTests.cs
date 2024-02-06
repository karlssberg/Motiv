using FluentAssertions;
using Humanizer;

namespace Karlssberg.Motiv.Tests;

public class ExactlySpecTests
{
    [Theory]
    [AutoParams(2, 4, true)]
    [AutoParams(4, 3, false)]
    [AutoParams(1, 2, false)]
    [AutoParams(1, 3, false)]
    public void Should_perform_the_logical_operation_NSatisfied(
        int first,
        int second,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<int>(i => i % 2 == 0)
            .YieldWhenTrue(i => $"{i} is even")
            .YieldWhenFalse(i => $"{i} is odd")
            .CreateSpec("is even spec");

        var sut = underlyingSpec.Exactly(2, "a pair of even numbers");

        var result = sut.IsSatisfiedBy([first, second]);

        result.Value.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams(1, 3, 5, 7, false, "1, 3, 5, and 7 do not contain a pair of even numbers", "1 is odd, 3 is odd, 5 is odd, and 7 is odd")]
    [AutoParams(1, 3, 5, 6, false, "1, 3, 5, and 6 do not contain a pair of even numbers", "1 is odd, 3 is odd, 5 is odd, and 6 is even")]
    [AutoParams(1, 3, 4, 6, true, "4 and 6 are a pair of even numbers", "4 is even and 6 is even")]
    [AutoParams(1, 4, 6, 8, false, "1, 4, 6, and 8 do not contain a pair of even numbers", "1 is odd, 4 is even, 6 is even, and 8 is even")]
    public void Should_mirror_the_outcome_of_an_all_satisfied_spec_metadata(
        int first,
        int second,
        int third,
        int fourth,
        bool expected,
        string expectedShallowReasons,
        string expectedDeepReason)
    {
        var underlyingSpec = Spec
            .Build<int>(n => n % 2 == 0)
            .YieldWhenTrue(n => $"{n} is even")
            .YieldWhenFalse(n => $"{n} is odd")
            .CreateSpec("is even spec");

        var sut = underlyingSpec
            .Exactly(2, "a pair of even numbers")
            .YieldWhenTrue((satisfied, _) =>
                $"{satisfied.Humanize()} are a pair of even numbers")
            .YieldWhenFalse((satisfied, unsatisfied) => 
                $"{(satisfied.Union(unsatisfied)).Humanize()} do not contain a pair of even numbers");

        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Value.Should().Be(expected);
        result.Reasons.Humanize().Should().Be(expectedShallowReasons);
        result.GetRootCauses().Humanize().Should().Be(expectedDeepReason);
    }
    
    [Theory]
    [AutoParams(2, true, true, "2_SATISFIED{2/2}:true(true x2)")]
    [AutoParams(2, true, false, "2_SATISFIED{1/2}:false(true and false)")]
    [AutoParams(2, false, true, "2_SATISFIED{1/2}:false(false and true)")]
    [AutoParams(2, false, false, "2_SATISFIED{0/2}:false(false x2)")]
    public void Should_serialize_the_result_of_the_NSatisfied_operation_when_metadata_is_a_string(
        int n,
        bool first,
        bool second,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString().ToLowerInvariant())
            .YieldWhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();

        bool[] models = [first, second];

        var sut = underlyingSpec.Exactly(n, "n booleans are true");
        var result = sut.IsSatisfiedBy(models);

        result.Description.Should().Be(expected);
    }

    private static string GenerateReason(bool allSatisfied, IEnumerable<BooleanResultWithModel<int, string>> results)
    {
        var count = results.Count(r => r.Value == allSatisfied);
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
            .All("all numbers are even")
            .YieldWhenTrue(results => $"{results.Count()} are true")
            .YieldWhenFalse(results => $"{results.Count()} are false");

        var result = sut.IsSatisfiedBy([1, 3, 5]);

        result.Value.Should().BeFalse();
        result.GetMetadata().Should().HaveCount(1);
        result.GetMetadata().Should().AllBeEquivalentTo("3 are false");
    }
    
    [Fact]
    public void Should_describe_an_NSatisfied_spec()
    {
        var underlyingSpec = Spec
            .Build<int>(n => n % 2 == 0)
            .YieldWhenTrue(true)
            .YieldWhenFalse(false)
            .CreateSpec("is even");

        var sut = underlyingSpec
            .Exactly(2, "a pair of even numbers");

        sut.Description.Should().Be("<a pair of even numbers>(is even)");
    }
}