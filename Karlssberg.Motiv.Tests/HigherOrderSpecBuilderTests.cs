using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class HigherOrderSpecBuilderTests
{
    [Fact]
    public void Should_mirror_the_outcome_of_an_all_satisfied_spec_metadata()
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .YieldWhenTrue(true.ToString())
            .YieldWhenFalse(false.ToString())
            .CreateSpec();

        var sut = underlyingSpec
            .BuildAllSatisfiedSpec()
            .Yield(GenerateReason)
            .CreateSpec();

        var result = sut.IsSatisfiedBy([false, false, false]);

        result.IsSatisfied.Should().BeFalse();
        result.GetMetadata().Should().HaveCount(1);
        result.GetMetadata().Should().AllBeEquivalentTo("3 are false");
    }
    private static string GenerateReason(bool allSatisfied, IEnumerable<BooleanResultWithModel<bool, string>> results) =>
        $"{results.Count(r => r.IsSatisfied == allSatisfied)} are {allSatisfied.ToString().ToLowerInvariant()}";
}