using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class SpecRegistryTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).Create("is positive");

    [Fact]
    public void Should_find_a_registered_spec_by_name()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-positive", IsPositive);

        // Act
        var entry = registry.Find("is-positive");

        // Assert
        entry.ShouldNotBeNull();
        entry.Name.ShouldBe("is-positive");
        entry.ModelType.ShouldBe(typeof(int));
        entry.MetadataType.ShouldBe(typeof(string));
        entry.IsAsync.ShouldBeFalse();
        entry.Spec.ShouldBeSameAs(IsPositive);
    }

    [Fact]
    public void Should_record_the_metadata_type_of_a_metadata_spec()
    {
        // Arrange
        var hasFlag = Spec
            .Build((int n) => n != 0)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("has flag");

        var registry = new SpecRegistry().Register("has-flag", hasFlag);

        // Act
        var entry = registry.Find("has-flag");

        // Assert
        entry.ShouldNotBeNull();
        entry.MetadataType.ShouldBe(typeof(int));
    }

    [Fact]
    public void Should_record_async_registrations_as_async()
    {
        // Arrange
        var isPositiveAsync = Spec
            .BuildAsync((int n) => Task.FromResult(n > 0))
            .Create("is positive async");

        var registry = new SpecRegistry().Register("is-positive-async", isPositiveAsync);

        // Act
        var entry = registry.Find("is-positive-async");

        // Assert
        entry.ShouldNotBeNull();
        entry.IsAsync.ShouldBeTrue();
        entry.ModelType.ShouldBe(typeof(int));
    }

    [Fact]
    public void Should_return_null_for_an_unregistered_name()
    {
        // Arrange
        var registry = new SpecRegistry();

        // Act
        var entry = registry.Find("missing");

        // Assert
        entry.ShouldBeNull();
    }

    [Fact]
    public void Should_throw_when_registering_a_duplicate_name()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-positive", IsPositive);

        // Act
        var act = () => registry.Register("is-positive", IsPositive);

        // Assert
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("is-positive");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_throw_when_registering_a_blank_name(string name)
    {
        // Arrange
        var registry = new SpecRegistry();

        // Act
        var act = () => registry.Register(name, IsPositive);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Should_count_registrations()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("is-positive-2", IsPositive);

        // Act & Assert
        registry.Count.ShouldBe(2);
    }

    [Fact]
    public void Should_expose_registered_entries_with_their_descriptions()
    {
        // Arrange
        var isPositive = Spec.Build((int n) => n > 0).WhenTrue("yes").WhenFalse("no").Create();
        var isEven = Spec.Build((int n) => n % 2 == 0).WhenTrue("yes").WhenFalse("no").Create();

        var registry = new SpecRegistry()
            .Register("is-positive", isPositive, "Whether the number is positive")
            .Register("is-even", isEven);

        // Act
        var entries = registry.Entries.OrderBy(e => e.Name).ToArray();

        // Assert
        entries.Length.ShouldBe(2);
        entries[0].Name.ShouldBe("is-even");
        entries[0].Description.ShouldBeNull();
        entries[1].Name.ShouldBe("is-positive");
        entries[1].Description!.ShouldBe("Whether the number is positive");
    }

    private sealed record Order(decimal Total);
    private sealed record Cart(IReadOnlyList<Order> Orders);

    [Fact]
    public void Should_find_a_registered_collection_and_report_its_element_type()
    {
        var registry = new SpecRegistry().RegisterCollection<Cart, Order>("orders", c => c.Orders);

        var binding = registry.FindCollection<Cart>("orders");

        binding.ShouldNotBeNull();
        binding.ElementType.ShouldBe(typeof(Order));
    }

    [Fact]
    public void Should_return_null_for_an_unregistered_collection_path()
    {
        var registry = new SpecRegistry().RegisterCollection<Cart, Order>("orders", c => c.Orders);
        registry.FindCollection<Cart>("items").ShouldBeNull();
    }

    [Fact]
    public void Should_scope_collections_by_parent_type()
    {
        var registry = new SpecRegistry().RegisterCollection<Cart, Order>("orders", c => c.Orders);
        registry.FindCollection<Order>("orders").ShouldBeNull();
    }

    [Fact]
    public void Should_reject_a_duplicate_collection_registration()
    {
        var registry = new SpecRegistry().RegisterCollection<Cart, Order>("orders", c => c.Orders);
        Should.Throw<ArgumentException>(() => registry.RegisterCollection<Cart, Order>("orders", c => c.Orders));
    }

    [Fact]
    public void Should_reject_an_empty_collection_path()
    {
        Should.Throw<ArgumentException>(() =>
            new SpecRegistry().RegisterCollection<Cart, Order>(" ", c => c.Orders));
    }
}
