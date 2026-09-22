namespace Motiv.Serialization.Tests;

public class NamespacePrefixTests
{
    [Theory]
    [InlineData("", "anything.at.all", true)]
    [InlineData("pricing", "pricing", true)]
    [InlineData("pricing", "pricing.eu.vat", true)]
    [InlineData("pricing", "pricingx", false)]
    [InlineData("pricing.eu", "pricing", false)]
    [InlineData("Pricing", "pricing.eu", false)]
    public void Should_cover_only_whole_segment_prefixes(string prefix, string name, bool expected)
    {
        // Act & Assert
        NamespacePrefix.Covers(prefix, name).ShouldBe(expected);
    }
}
