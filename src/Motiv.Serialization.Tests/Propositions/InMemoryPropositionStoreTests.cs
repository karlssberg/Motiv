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
    public async Task Should_round_trip_a_saved_proposition()
    {
        // Arrange
        var store = new InMemoryPropositionStore();

        // Act
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.is-eligible")), default);

        // Assert
        var loaded = store.Load();
        loaded.Count.ShouldBe(1);
        loaded[0].Name.ShouldBe("customer.is-eligible");
        loaded[0].ModelType.ShouldBe("customer");
        loaded[0].Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_replace_a_proposition_saved_under_the_same_name()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Act
        await store.WriteAsync(PropositionBatch.Save(Stored("a", version: 2)), default);

        // Assert
        store.Load().Count.ShouldBe(1);
        store.Load()[0].Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_delete_by_name()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(PropositionBatch.Save(Stored("a")), default);
        await store.WriteAsync(PropositionBatch.Save(Stored("b")), default);

        // Act
        await store.WriteAsync(PropositionBatch.Delete("a"), default);

        // Assert
        store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }

    [Fact]
    public async Task Should_ignore_deleting_an_absent_name()
    {
        // Arrange
        var store = new InMemoryPropositionStore();

        // Act & Assert — the store is a dumb sink; the set decides what is legal. Not throwing is the assertion.
        await store.WriteAsync(PropositionBatch.Delete("absent"), default);
    }

    [Fact]
    public async Task Should_write_a_save_and_a_delete_in_one_batch()
    {
        // Arrange — the batch shape is what makes an envelope all-or-nothing
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(PropositionBatch.Save(Stored("a")), default);

        // Act
        await store.WriteAsync(
            new PropositionBatch([Stored("b")], ["a"]), default);

        // Assert
        store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }
}
