using FluentAssertions;

namespace Karlssberg.Motiv.Tests;
public class BooleanResultTests
{
    [Fact]
    public void Satisfied_ReturnsCorrectValue()
    {
        var result = new BooleanResult(true, "assertion");
        result.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Description_ReturnsCorrectValue()
    {
        var result = new BooleanResult(true, "assertion");
        result.Description.ToString().Should().Be("assertion");
    }

    [Fact]
    public void Explanation_ReturnsCorrectValue()
    {
        var result = new BooleanResult(true, "assertion");
        result.Explanation.ToString().Should().Be("assertion");
    }

    [Fact]
    public void Causes_ReturnsEmpty()
    {
        var result = new BooleanResult(true, "assertion");
        result.Causes.Should().BeEmpty();
    }

    [Fact]
    public void Underlying_ReturnsEmpty()
    {
        var result = new BooleanResult(true, "assertion");
        result.Underlying.Should().BeEmpty();
    }

    [Fact]
    public void MetadataTier_ReturnsCorrectValue()
    {
        var result = new BooleanResult(true, "assertion");
        result.MetadataTier.ToString().Should().Be("assertion");
    }

    [Fact]
    public void CausesWithMetadata_ReturnsEmpty()
    {
        var result = new BooleanResult(true, "assertion");
        result.CausesWithMetadata.Should().BeEmpty();
    }

    [Fact]
    public void UnderlyingWithMetadata_ReturnsEmpty()
    {
        var result = new BooleanResult(true, "assertion");
        result.UnderlyingWithMetadata.Should().BeEmpty();
    }

    [Fact]
    public void Satisfied_WithMetadata_ReturnsCorrectValue()
    {
        var result = new BooleanResult<bool>(true, "assertion", true);
        result.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Description_WithMetadata_ReturnsCorrectValue()
    {
        var result = new BooleanResult<bool>(true, "assertion", true);
        result.Description.ToString().Should().Be("assertion");
    }

    [Fact]
    public void Explanation_WithMetadata_ReturnsCorrectValue()
    {
        var result = new BooleanResult<bool>(true, "assertion", true);
        result.Explanation.ToString().Should().Be("assertion");
    }

    [Fact]
    public void Causes_WithMetadata_ReturnsEmpty()
    {
        var result = new BooleanResult<bool>(true, "assertion", true);
        result.Causes.Should().BeEmpty();
    }

    [Fact]
    public void Underlying_WithMetadata_ReturnsEmpty()
    {
        var result = new BooleanResult<bool>(true, "assertion", true);
        result.Underlying.Should().BeEmpty();
    }

    [Fact]
    public void MetadataTier_WithMetadata_ReturnsCorrectValue()
    {
        var result = new BooleanResult<bool>(true, "assertion", true);
        result.Metadata.Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public void CausesWithMetadata_WithMetadata_ReturnsEmpty()
    {
        var result = new BooleanResult<bool>(true, "assertion", true);
        result.CausesWithMetadata.Should().BeEmpty();
    }

    [Fact]
    public void UnderlyingWithMetadata_WithMetadata_ReturnsEmpty()
    {
        var result = new BooleanResult<bool>(true, "assertion", true);
        result.UnderlyingWithMetadata.Should().BeEmpty();
    }
}