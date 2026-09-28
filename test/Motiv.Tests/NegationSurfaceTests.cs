using Motiv.Traversal;

namespace Motiv.Tests;

/// <summary>
/// Pins parts of the negation surface that mutation testing (#294) found no test reaching: that a
/// negation reports itself as not collapsible through the public <see cref="IBooleanOperationSpec" />;
/// that negating a named proposition built over a negated policy writes its statement bare
/// (<c>!x</c>) — a negation beneath the name is not a binary operation, so it earns the name no
/// parentheses; and that the async <c>Description.Detailed</c> of a negated leaf gains a <c>!</c> and
/// loses it again when negated twice, as the sync one already does.
/// </summary>
public class NegationSurfaceTests
{
    private static PolicyBase<int, string> Policy(string name) =>
        Spec.Build((int n) => n > 0).Create(name);

    private static AsyncPolicyBase<int, string> AsyncPolicy(string name) =>
        Spec.BuildAsync((int _) => new ValueTask<bool>(true)).Create(name);

    [Fact]
    public void Should_report_a_negated_policy_as_not_collapsible()
    {
        IBooleanOperationSpec sut = (IBooleanOperationSpec)Policy("a").Not();

        sut.IsCollapsable.ShouldBeFalse();
    }

    [Fact]
    public void Should_report_a_negated_spec_as_not_collapsible()
    {
        SpecBase<int, string> operand = Policy("a");

        IBooleanOperationSpec sut = (IBooleanOperationSpec)operand.Not();

        sut.IsCollapsable.ShouldBeFalse();
    }

    [Fact]
    public void Should_not_parenthesise_the_negated_name_of_a_proposition_built_over_a_negated_policy()
    {
        var named = Spec.Build(Policy("a").Not()).Create("x");

        var sut = named.Not();

        sut.Description.Statement.ShouldBe("!x");
    }

    [Fact]
    public void Should_prefix_the_detailed_description_of_a_negated_async_leaf()
    {
        var sut = AsyncPolicy("a").Not();

        sut.Description.Detailed.ShouldBe("!a");
    }

    [Fact]
    public void Should_strip_the_prefix_from_the_detailed_description_of_a_doubly_negated_async_leaf()
    {
        var sut = AsyncPolicy("a").Not().Not();

        sut.Description.Detailed.ShouldBe("a");
    }
}
