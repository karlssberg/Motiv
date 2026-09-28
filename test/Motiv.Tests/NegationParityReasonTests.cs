namespace Motiv.Tests;

/// <summary>
/// Pins the <c>Reason</c> of a result negated several times over. A run of negations folds to its
/// parity: an even number cancels out and leaves the operand's reason bare, an odd number leaves one
/// <c>!</c>. Mutation testing (#294) showed that no test negated more than twice, so the parity check
/// could be replaced (<c>count * 2</c> for <c>count % 2</c>) with the suite still green, and the two odd
/// arms for a leaf operand were never reached at all.
/// </summary>
public class NegationParityReasonTests
{
    private static PolicyBase<int, string> Minimal() =>
        Spec.Build((int n) => n > 0).Create("a");

    private static PolicyBase<int, string> Explained() =>
        Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create();

    [Fact]
    public void Should_leave_one_negation_of_a_named_leaf_negated_three_times()
    {
        var sut = Minimal().Not().Not().Not();

        sut.Evaluate(1).Reason.ShouldBe("!(a == true)");
    }

    [Fact]
    public void Should_cancel_every_negation_of_a_named_leaf_negated_four_times()
    {
        var sut = Minimal().Not().Not().Not().Not();

        sut.Evaluate(1).Reason.ShouldBe("a == true");
    }

    [Fact]
    public void Should_leave_one_bare_negation_of_an_explained_leaf_negated_three_times()
    {
        var sut = Explained().Not().Not().Not();

        sut.Evaluate(1).Reason.ShouldBe("!positive");
    }
}
