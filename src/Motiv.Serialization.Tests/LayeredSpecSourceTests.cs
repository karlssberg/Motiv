using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class LayeredSpecSourceTests
{
    private static SpecBase<int, string> Compiled { get; } = Spec.Build((int n) => n > 0).Create("compiled");
    private static SpecBase<int, string> Authored { get; } = Spec.Build((int n) => n > 100).Create("authored");

    /// <summary>A minimal overlay standing in for the proposition store.</summary>
    private sealed class StubOverlay(params SpecRegistryEntry[] entries) : ISpecSource
    {
        public SpecRegistryEntry? Find(string name) =>
            entries.FirstOrDefault(entry => entry.Name == name);

        public CollectionBinding<TParent>? FindCollection<TParent>(string path) => null;
    }

    private static SpecRegistryEntry Entry(string name, SpecBase<int, string> spec) =>
        new SpecRegistry().Register(name, spec).Find(name)!;

    [Fact]
    public void Should_prefer_the_overlay_over_the_registry()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-big", Compiled);
        var source = new LayeredSpecSource(new StubOverlay(Entry("is-big", Authored)), registry);

        // Act
        var entry = source.Find("is-big");

        // Assert
        entry.ShouldNotBeNull();
        entry.Spec.ShouldBeSameAs(Authored);
    }

    [Fact]
    public void Should_fall_through_to_the_registry_when_the_overlay_is_empty()
    {
        // Arrange — this is what revert relies on: remove the overlay entry and the compiled spec reappears
        var registry = new SpecRegistry().Register("is-big", Compiled);
        var source = new LayeredSpecSource(new StubOverlay(), registry);

        // Act
        var entry = source.Find("is-big");

        // Assert
        entry.ShouldNotBeNull();
        entry.Spec.ShouldBeSameAs(Compiled);
    }

    [Fact]
    public void Should_report_an_unknown_name_as_null()
    {
        // Arrange
        var source = new LayeredSpecSource(new StubOverlay(), new SpecRegistry());

        // Act & Assert
        source.Find("absent").ShouldBeNull();
    }

    [Fact]
    public void Should_resolve_collections_from_the_registry_only()
    {
        // Arrange — collections are compiled-only, so the overlay must not be consulted
        var registry = new SpecRegistry();
        registry.RegisterCollection<Basket, int>("items", basket => basket.Items);
        var source = new LayeredSpecSource(new StubOverlay(), registry);

        // Act & Assert
        source.FindCollection<Basket>("items").ShouldNotBeNull();
    }

    private sealed record Basket(IReadOnlyList<int> Items);
}
