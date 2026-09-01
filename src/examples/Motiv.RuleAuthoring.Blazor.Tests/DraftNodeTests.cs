using Motiv.RuleAuthoring.Blazor.Authoring;
using Shouldly;

namespace Motiv.RuleAuthoring.Blazor.Tests;

public class DraftNodeTests
{
    /// <remarks>
    /// The schema requires at least two operands of <c>and</c>, <c>or</c> and friends, so an editor
    /// that turned a spec node into one and left it empty would put the author in a state the
    /// document can never be valid from.
    /// </remarks>
    [Fact]
    public void Seeds_two_operands_when_a_spec_becomes_a_binary_operator()
    {
        var node = DraftNode.Spec("customer.is-active");

        node.ChangeKindTo(DraftNodeKind.AndAlso);

        node.Children.Count.ShouldBe(2);
    }

    [Fact]
    public void Seeds_one_operand_when_a_spec_becomes_a_negation()
    {
        var node = DraftNode.Spec("customer.is-active");

        node.ChangeKindTo(DraftNodeKind.Not);

        node.Children.Count.ShouldBe(1);
    }

    [Fact]
    public void Keeps_the_operands_already_authored_when_switching_between_operators()
    {
        var kept = DraftNode.Spec("customer.is-active");
        var node = DraftNode.Operator(DraftNodeKind.And, kept, DraftNode.Spec("customer.is-adult"));

        node.ChangeKindTo(DraftNodeKind.OrElse);

        node.Children.Count.ShouldBe(2);
        node.Children[0].ShouldBeSameAs(kept);
    }

    [Fact]
    public void Drops_the_operands_when_an_operator_becomes_a_spec()
    {
        var node = DraftNode.Operator(
            DraftNodeKind.And,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));

        node.ChangeKindTo(DraftNodeKind.Spec);

        node.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Refuses_to_remove_an_operand_a_binary_operator_still_needs()
    {
        var node = DraftNode.Operator(
            DraftNodeKind.And,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));

        node.RemoveOperand(node.Children[0]).ShouldBeFalse();
        node.Children.Count.ShouldBe(2);
    }

    [Fact]
    public void Removes_an_operand_above_the_minimum()
    {
        var node = DraftNode.Operator(
            DraftNodeKind.And,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));
        node.AddOperand();

        node.RemoveOperand(node.Children[2]).ShouldBeTrue();
        node.Children.Count.ShouldBe(2);
    }
}
