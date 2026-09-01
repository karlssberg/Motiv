using Bunit;
using Motiv.RuleAuthoring.Blazor.Pages;
using Shouldly;

namespace Motiv.RuleAuthoring.Blazor.Tests;

/// <summary>
/// The authoring loop as a reader meets it: write a document, validate it, evaluate it, explain it —
/// all in the component, over Motiv.Serialization.
/// </summary>
public class AuthorPageTests : BunitContext
{
    [Fact]
    public void Shows_the_document_it_authored_from_its_opening_draft()
    {
        var page = Render<Author>();

        page.Find("pre.json").TextContent.ShouldContain("\"andAlso\"");
        page.Find("pre.json").TextContent.ShouldContain("\"customer.is-active\"");
    }

    [Fact]
    public void Evaluates_the_opening_draft_against_the_first_sample_customer()
    {
        var page = Render<Author>();

        page.Find("p.verdict").TextContent.ShouldContain("True");
    }

    /// <remarks>
    /// The document is named, so <c>Reason</c> is the name alone and the causes are in the
    /// justification. A page that showed only the first would explain nothing.
    /// </remarks>
    [Fact]
    public void Shows_both_the_summary_and_the_causes_behind_it()
    {
        var page = Render<Author>();

        page.Find("pre.reason").TextContent.Trim().ShouldBe("customer.can-checkout == true");
        page.Find("pre.justification").TextContent.ShouldContain("is active == true");
        page.Find("pre.justification").TextContent.ShouldContain("is adult == true");
    }

    [Fact]
    public void Re_evaluates_when_the_author_picks_a_different_customer()
    {
        var page = Render<Author>();

        page.Find("label.customer select").Change("Bob");

        page.Find("p.verdict").TextContent.ShouldContain("False");
        page.Find("pre.justification").TextContent.ShouldContain("is adult == false");
    }

    [Fact]
    public void Reports_a_validation_error_with_the_path_Motiv_gave_it()
    {
        var page = Render<Author>();

        page.FindAll("select.spec")[1].Change("");

        page.Find(".errors li").TextContent.ShouldContain("$.rule.andAlso[1].spec");
    }

    /// <remarks>
    /// The clearest demonstration of the suffix rule on the page: rename the document and the
    /// summary follows the name, while the justification keeps naming the same operands.
    /// </remarks>
    [Fact]
    public void Follows_the_documents_name_when_the_author_renames_it()
    {
        var page = Render<Author>();

        page.Find("label.document-name input").Input("customer.may-order");

        page.Find("pre.reason").TextContent.Trim().ShouldBe("customer.may-order == true");
        page.Find("pre.justification").TextContent.ShouldContain("is active == true");
    }
}
