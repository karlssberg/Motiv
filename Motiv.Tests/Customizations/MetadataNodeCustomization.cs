using AutoFixture;
using AutoFixture.Kernel;
using Motiv.Shared;

namespace Motiv.Tests.Customizations;

public class TestMetadataNode<TMetadata>(TMetadata metadata)
    : MetadataNode<TMetadata>(metadata, []);

public class MetadataNodeCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new TypeRelay(typeof(MetadataNode<>), typeof(TestMetadataNode<>)));
    }
}
