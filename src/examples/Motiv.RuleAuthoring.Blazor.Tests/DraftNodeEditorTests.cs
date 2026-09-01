using Bunit;
using Motiv.RuleAuthoring.Blazor.Authoring;
using Motiv.RuleAuthoring.Blazor.Components;
using Motiv.RuleAuthoring.Blazor.Domain;
using Motiv.Serialization;
using Shouldly;

namespace Motiv.RuleAuthoring.Blazor.Tests;

public class DraftNodeEditorTests : BunitContext
{
    private static readonly string[] SpecNames = ["customer.is-active", "customer.is-adult"];

    private IRenderedComponent<DraftNodeEditor> RenderEditor(
        DraftNode node,
        params LocatedError[] errors) =>
        Render<DraftNodeEditor>(parameters => parameters
            .Add(editor => editor.Node, node)
            .Add(editor => editor.Errors, errors)
            .Add(editor => editor.SpecNames, SpecNames));

    /// <remarks>
    /// The model refuses a removal its operator still needs, so an enabled control for it would be a
    /// button that does nothing.
    /// </remarks>
    [Fact]
    public void Disables_remove_on_operands_the_operator_still_needs()
    {
        var editor = RenderEditor(DraftNode.Operator(
            DraftNodeKind.AndAlso,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult")));

        editor.FindAll("button.remove").ShouldAllBe(button => button.HasAttribute("disabled"));
    }

    [Fact]
    public void Enables_remove_once_an_operand_can_be_spared()
    {
        var node = DraftNode.Operator(
            DraftNodeKind.AndAlso,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));
        node.AddOperand();

        var editor = RenderEditor(node);

        editor.FindAll("button.remove").ShouldAllBe(button => !button.HasAttribute("disabled"));
    }

    /// <remarks>
    /// A negation holds exactly one operand, so there is nothing for "add operand" to do.
    /// </remarks>
    [Fact]
    public void Disables_add_operand_on_a_negation()
    {
        var editor = RenderEditor(DraftNode.Operator(DraftNodeKind.Not, DraftNode.Spec("customer.is-active")));

        editor.Find("button:not(.remove)").HasAttribute("disabled").ShouldBeTrue();
    }

    /// <remarks>
    /// This is what the path arithmetic is for: an error reaches the control its node owns, and no
    /// other node's.
    /// </remarks>
    [Fact]
    public void Renders_an_error_against_the_node_it_was_located_on()
    {
        var offending = DraftNode.Spec("");
        var node = DraftNode.Operator(DraftNodeKind.AndAlso, DraftNode.Spec("customer.is-active"), offending);
        var error = new LocatedError(
            new RuleError("$.rule.andAlso[1].spec", RuleErrorCode.InvalidNode, "value must be a non-empty string"),
            offending);

        var editor = RenderEditor(node, error);

        var reported = editor.FindAll("p.error");
        reported.Count.ShouldBe(1);
        reported[0].TextContent.ShouldContain("value must be a non-empty string");
    }

    [Fact]
    public void Offers_no_remove_on_the_root_node()
    {
        var editor = RenderEditor(DraftNode.Spec("customer.is-active"));

        editor.FindAll("button.remove").ShouldBeEmpty();
    }

    [Fact]
    public void Reseeds_the_operands_when_the_author_changes_the_kind()
    {
        var node = DraftNode.Spec("customer.is-active");
        var editor = RenderEditor(node);

        editor.Find("select.kind").Change(nameof(DraftNodeKind.AndAlso));

        node.Kind.ShouldBe(DraftNodeKind.AndAlso);
        node.Children.Count.ShouldBe(2);
    }

    [Fact]
    public void Records_the_proposition_the_author_chose()
    {
        var node = DraftNode.Spec("");
        var editor = RenderEditor(node);

        editor.Find("select.spec").Change("customer.is-adult");

        node.SpecName.ShouldBe("customer.is-adult");
    }

    [Fact]
    public void Appends_an_unfinished_operand_on_request()
    {
        var node = DraftNode.Operator(
            DraftNodeKind.AndAlso,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));
        var editor = RenderEditor(node);

        editor.Find("button:not(.remove)").Click();

        node.Children.Count.ShouldBe(3);
        node.Children[2].SpecName.ShouldBe("");
    }

    [Fact]
    public void Drops_the_operand_the_author_removed()
    {
        var node = DraftNode.Operator(
            DraftNodeKind.AndAlso,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));
        node.AddOperand();
        var editor = RenderEditor(node);

        editor.FindAll("button.remove")[2].Click();

        node.Children.Count.ShouldBe(2);
        node.Children.Select(child => child.SpecName)
            .ShouldBe(["customer.is-active", "customer.is-adult"]);
    }

    [Fact]
    public void Notifies_its_owner_that_the_draft_needs_reauthoring()
    {
        var node = DraftNode.Spec("");
        var changes = 0;
        var editor = Render<DraftNodeEditor>(parameters => parameters
            .Add(e => e.Node, node)
            .Add(e => e.Errors, Array.Empty<LocatedError>())
            .Add(e => e.SpecNames, SpecNames)
            .Add(e => e.Changed, () => changes++));

        editor.Find("select.spec").Change("customer.is-adult");

        changes.ShouldBe(1);
    }
}
