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
}
