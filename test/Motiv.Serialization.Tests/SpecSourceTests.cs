using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class SpecSourceTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).Create("is positive");

    [Fact]
    public void Should_expose_a_registry_as_a_spec_source()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-positive", IsPositive);

        // Act
        ISpecSource source = registry;

        // Assert
        source.Find("is-positive").ShouldNotBeNull();
        source.Find("absent").ShouldBeNull();
    }

    [Fact]
    public void Should_resolve_registered_collections_through_the_source()
    {
        // Arrange
        var registry = new SpecRegistry();
        registry.RegisterCollection<Basket, int>("items", basket => basket.Items);

        // Act
        ISpecSource source = registry;

        // Assert
        source.FindCollection<Basket>("items").ShouldNotBeNull();
        source.FindCollection<Basket>("absent").ShouldBeNull();
    }

    private sealed record Basket(IReadOnlyList<int> Items);
}
