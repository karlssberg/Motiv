using Motiv.Serialization;
using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

public class HigherOrderBuildTests
{
    private static SpecBase<int, string> IsEven { get; } =
        Spec.Build((int i) => i % 2 == 0).WhenTrue("even").WhenFalse("odd").Create();

    private static readonly IEnumerable<int> AllEven = [2, 4, 6];
    private static readonly IEnumerable<int> SomeEven = [1, 2, 3];
    private static readonly IEnumerable<int> NoneEven = [1, 3, 5];

    [Fact]
    public void Should_build_all_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsAllSatisfied, null);
        var expected = Spec.Build(IsEven).AsAllSatisfied()
            .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }

    [Fact]
    public void Should_build_any_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsAnySatisfied, null);
        var expected = Spec.Build(IsEven).AsAnySatisfied()
            .WhenTrue("any satisfied").WhenFalse("none satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }

    [Fact]
    public void Should_build_exactly_n_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsNSatisfied, 2);
        var expected = Spec.Build(IsEven).AsNSatisfied(2)
            .WhenTrue("exactly 2 satisfied").WhenFalse("not exactly 2 satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }

    [Fact]
    public void Should_build_at_least_n_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsAtLeastNSatisfied, 2);
        var expected = Spec.Build(IsEven).AsAtLeastNSatisfied(2)
            .WhenTrue("at least 2 satisfied").WhenFalse("fewer than 2 satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }

    [Fact]
    public void Should_build_at_most_n_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsAtMostNSatisfied, 1);
        var expected = Spec.Build(IsEven).AsAtMostNSatisfied(1)
            .WhenTrue("at most 1 satisfied").WhenFalse("more than 1 satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }
}
