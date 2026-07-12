using Motiv.Shared;

namespace Motiv.Tests.Shared;

public class MetadataNodeTests
{
    [Fact]
    public void Metadata_ShouldBeEmpty_WhenMetadataSourceIsNull()
    {
        var node = new MetadataNode<string>((IEnumerable<string>)null!, []);

        node.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void Underlying_ShouldBeEmpty_WhenMetadataSourceIsNull()
    {
        var node = new MetadataNode<string>((IEnumerable<string>)null!, []);

        node.Underlying.ShouldBeEmpty();
    }
}
