namespace Motiv.Serialization.AspNetCore.Tests;

public class GrantEvaluatorTests
{
    [Theory]
    [InlineData(GrantVerb.Publish, GrantVerb.Read, true)]    // ladder: publish covers read
    [InlineData(GrantVerb.Publish, GrantVerb.Author, true)]
    [InlineData(GrantVerb.Author, GrantVerb.Publish, false)] // never upward
    [InlineData(GrantVerb.Read, GrantVerb.Author, false)]
    public void Should_apply_the_verb_ladder(GrantVerb held, GrantVerb required, bool expected)
    {
        // Arrange
        var grants = new[] { new NamespaceGrant("pricing", held) };

        // Act & Assert
        GrantEvaluator.IsGranted(grants, required, "pricing.eu.vat").ShouldBe(expected);
    }

    [Fact]
    public void Should_deny_outside_the_granted_prefix()
    {
        // Arrange
        var grants = new[] { new NamespaceGrant("pricing", GrantVerb.Publish) };

        // Act & Assert
        GrantEvaluator.IsGranted(grants, GrantVerb.Author, "fraud.velocity").ShouldBeFalse();
    }

    [Fact]
    public void Should_report_author_anywhere_from_any_author_or_publish_grant()
    {
        // Act & Assert
        GrantEvaluator.CanAuthorAnywhere([new NamespaceGrant("pricing", GrantVerb.Author)]).ShouldBeTrue();
        GrantEvaluator.CanAuthorAnywhere([new NamespaceGrant("pricing", GrantVerb.Read)]).ShouldBeFalse();
        GrantEvaluator.CanAuthorAnywhere([]).ShouldBeFalse();
    }
}
