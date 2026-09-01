using Motiv.RuleAuthoring.Blazor.Authoring;
using Shouldly;

namespace Motiv.RuleAuthoring.Blazor.Tests;

public class RuleDocumentWriterTests
{
    [Fact]
    public void Writes_a_spec_node_as_the_rule_root()
    {
        var draft = DraftNode.Spec("customer.is-active");

        var document = RuleDocumentWriter.Write(draft, "customer.can-checkout");

        document.Json.ShouldBe(
            """
            {
              "name": "customer.can-checkout",
              "rule": {
                "spec": "customer.is-active"
              }
            }
            """);
    }

    [Fact]
    public void Reports_the_root_node_at_the_rule_path()
    {
        var draft = DraftNode.Spec("customer.is-active");

        var document = RuleDocumentWriter.Write(draft, "customer.can-checkout");

        document.NodesByPath["$.rule"].ShouldBeSameAs(draft);
    }

    [Fact]
    public void Writes_an_operator_node_as_an_array_of_operands()
    {
        var draft = DraftNode.Operator(
            DraftNodeKind.AndAlso,
            DraftNode.Spec("customer.is-active"),
            DraftNode.Spec("customer.is-adult"));

        var document = RuleDocumentWriter.Write(draft, "customer.can-checkout");

        document.Json.ShouldBe(
            """
            {
              "name": "customer.can-checkout",
              "rule": {
                "andAlso": [
                  {
                    "spec": "customer.is-active"
                  },
                  {
                    "spec": "customer.is-adult"
                  }
                ]
              }
            }
            """);
    }

    [Fact]
    public void Reports_each_operand_at_its_indexed_path()
    {
        var active = DraftNode.Spec("customer.is-active");
        var adult = DraftNode.Spec("customer.is-adult");
        var draft = DraftNode.Operator(DraftNodeKind.AndAlso, active, adult);

        var document = RuleDocumentWriter.Write(draft, "customer.can-checkout");

        document.NodesByPath["$.rule.andAlso[0]"].ShouldBeSameAs(active);
        document.NodesByPath["$.rule.andAlso[1]"].ShouldBeSameAs(adult);
    }

    [Fact]
    public void Writes_a_negation_as_a_single_nested_node()
    {
        var inner = DraftNode.Spec("customer.is-active");
        var draft = DraftNode.Operator(DraftNodeKind.Not, inner);

        var document = RuleDocumentWriter.Write(draft, "customer.is-dormant");

        document.Json.ShouldBe(
            """
            {
              "name": "customer.is-dormant",
              "rule": {
                "not": {
                  "spec": "customer.is-active"
                }
              }
            }
            """);
        document.NodesByPath["$.rule.not"].ShouldBeSameAs(inner);
    }
}
