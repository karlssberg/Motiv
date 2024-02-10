using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ReasonsTests
{
    [Theory]
    [InlineAutoData(1, "is odd")]
    [InlineAutoData(2, "is even")]
    public void Should_provide_a_reason_for_a_spec_result(int n, string expected)
    {
        var spec = Spec.Build<int>(i => i % 2 == 0)
            .YieldWhenTrue("is even")
            .YieldWhenFalse("is odd")
            .CreateSpec();
        
        var result = spec.IsSatisfiedBy(n);
        result.Reasons.Should().ContainSingle(expected);
    }
} 