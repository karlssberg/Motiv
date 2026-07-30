using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class InMemoryPropositionStoreTests
{
    private static StoredProposition Stored(string name, int version = 1) =>
        new(name, "customer", $$"""{ "rule": { "spec": "is-active", "name": "{{name}}" } }""", version, null);

    [Fact]
    public void Should_start_empty()
    {
        // Act & Assert
        new InMemoryPropositionStore().Load().ShouldBeEmpty();
    }

    [Fact]
    public void Should_round_trip_a_saved_proposition()
    {
        // Arrange
        var store = new InMemoryPropositionStore();

        // Act
        store.Save(Stored("customer.is-eligible"));

        // Assert
        var loaded = store.Load();
        loaded.Count.ShouldBe(1);
        loaded[0].Name.ShouldBe("customer.is-eligible");
        loaded[0].ModelType.ShouldBe("customer");
        loaded[0].Version.ShouldBe(1);
    }

    [Fact]
    public void Should_replace_a_proposition_saved_under_the_same_name()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        store.Save(Stored("a", version: 1));

        // Act
        store.Save(Stored("a", version: 2));

        // Assert
        store.Load().Count.ShouldBe(1);
        store.Load()[0].Version.ShouldBe(2);
    }

    [Fact]
    public void Should_delete_by_name()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        store.Save(Stored("a"));
        store.Save(Stored("b"));

        // Act
        store.Delete("a");

        // Assert
        store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }

    [Fact]
    public void Should_ignore_deleting_an_absent_name()
    {
        // Arrange
        var store = new InMemoryPropositionStore();

        // Act
        var delete = () => store.Delete("absent");

        // Assert — the store is a dumb sink; the set decides what is legal
        delete.ShouldNotThrow();
    }
}
