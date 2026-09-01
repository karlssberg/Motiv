using Motiv.RuleAuthoring.Blazor.Authoring;
using Motiv.RuleAuthoring.Blazor.Domain;
using Motiv.Serialization;
using Shouldly;

namespace Motiv.RuleAuthoring.Blazor.Tests;

public class AuthoringSessionTests
{
    private readonly AuthoringSession _session = new();

    private static readonly Customer Eligible = new()
    {
        Name = "Ada",
        IsActive = true,
        Age = 36,
        OrderCount = 4
    };

    [Fact]
    public void Accepts_a_well_formed_document()
    {
        var draft = DraftNode.Operator(
            DraftNodeKind.AndAlso,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));

        var outcome = _session.Author(draft, "customer.can-checkout", Eligible);

        outcome.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Summarises_a_satisfied_composition_by_the_documents_own_name()
    {
        var draft = DraftNode.Operator(
            DraftNodeKind.AndAlso,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));

        var outcome = _session.Author(draft, "customer.can-checkout", Eligible);

        outcome.Satisfied.ShouldBe(true);
        outcome.Reason.ShouldNotBeNull().ShouldBe("customer.can-checkout == true");
    }

    /// <remarks>
    /// The document is named, so Motiv's suffix rule makes <c>Reason</c> the name and nothing else.
    /// The operands that caused the outcome survive only in the justification tree, which is why the
    /// sample renders both and not just the summary.
    /// </remarks>
    [Fact]
    public void Keeps_the_contributing_operands_in_the_justification()
    {
        var draft = DraftNode.Operator(
            DraftNodeKind.AndAlso,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));

        var outcome = _session.Author(draft, "customer.can-checkout", Eligible);

        outcome.Justification.ShouldNotBeNull().ShouldBe(
            """
            customer.can-checkout == true
                AND ALSO
                    is active == true
                    is adult == true
            """);
    }

    [Fact]
    public void Locates_an_unknown_spec_on_the_draft_node_that_named_it()
    {
        var known = DraftNode.Spec("customer.is-active");
        var unknown = DraftNode.Spec("customer.is-astronaut");
        var draft = DraftNode.Operator(DraftNodeKind.AndAlso, known, unknown);

        var outcome = _session.Author(draft, "customer.can-checkout", Eligible);

        var error = outcome.Errors.ShouldHaveSingleItem();
        error.Error.Code.ShouldBe(RuleErrorCode.UnknownSpec);
        error.Node.ShouldBeSameAs(unknown);
    }

    [Fact]
    public void Refuses_an_operator_with_a_single_operand()
    {
        var draft = DraftNode.Operator(DraftNodeKind.AndAlso, DraftNode.Spec("customer.is-active"));

        var outcome = _session.Author(draft, "customer.can-checkout", Eligible);

        outcome.Errors.ShouldNotBeEmpty();
        outcome.Satisfied.ShouldBeNull();
    }

    /// <remarks>
    /// An unfinished spec node — one whose proposition the author has not chosen yet — is reported
    /// at <c>$.rule.andAlso[1].spec</c>, a path that names a property rather than the node. The
    /// error still has to reach the control the author is looking at.
    /// </remarks>
    [Fact]
    public void Locates_an_error_reported_against_a_node_property_on_that_node()
    {
        var chosen = DraftNode.Spec("customer.is-active");
        var unchosen = DraftNode.Spec("");
        var draft = DraftNode.Operator(DraftNodeKind.AndAlso, chosen, unchosen);

        var outcome = _session.Author(draft, "customer.can-checkout", Eligible);

        var error = outcome.Errors.ShouldHaveSingleItem();
        error.Error.Path.ShouldBe("$.rule.andAlso[1].spec");
        error.Node.ShouldBeSameAs(unchosen);
    }
}
