using System.Linq;
using System.Threading.Tasks;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Testing;

/// <summary>
/// What it means to be an <see cref="IPropositionStore"/>, as one suite every implementation
/// derives from — the proposition-side twin of <see cref="RuleStoreConformance"/>.
/// </summary>
public abstract class PropositionStoreConformance : IAsyncLifetime
{
    /// <summary>The store under test. A fresh, empty one per test.</summary>
    protected IPropositionStore Store { get; private set; } = null!;

    /// <summary>Creates an empty store. Called once per test.</summary>
    protected abstract Task<IPropositionStore> CreateStoreAsync();

    /// <summary>Releases whatever <see cref="CreateStoreAsync"/> allocated. Does nothing by default.</summary>
    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    /// <summary>One proposition row, with a document that binds nowhere in particular.</summary>
    protected static StoredProposition Stored(string name, int version = 1) =>
        new(name, "customer", $$"""{ "rule": { "spec": "is-active", "name": "{{name}}" } }""", version, null);

    public async Task InitializeAsync() => Store = await CreateStoreAsync();

    public Task DisposeAsync() => DisposeStoreAsync();

    [Fact]
    public void Should_start_empty()
    {
        // Act & Assert
        Store.Load().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_round_trip_a_saved_proposition()
    {
        // Act
        await Store.WriteAsync(PropositionBatch.Save(Stored("customer.is-eligible")), default);

        // Assert
        var loaded = Store.Load();
        loaded.Count.ShouldBe(1);
        loaded[0].Name.ShouldBe("customer.is-eligible");
        loaded[0].ModelType.ShouldBe("customer");
        loaded[0].Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_replace_a_proposition_saved_under_the_same_name()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Act
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 2)), default);

        // Assert
        Store.Load().Count.ShouldBe(1);
        Store.Load()[0].Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_delete_by_name()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);
        await Store.WriteAsync(PropositionBatch.Save(Stored("b")), default);

        // Act
        await Store.WriteAsync(PropositionBatch.Delete("a"), default);

        // Assert
        Store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }

    [Fact]
    public async Task Should_ignore_deleting_an_absent_name()
    {
        // Act & Assert — the store is a dumb sink; the set decides what is legal. Not throwing is the assertion.
        await Store.WriteAsync(PropositionBatch.Delete("absent"), default);
    }

    [Fact]
    public async Task Should_write_a_save_and_a_delete_in_one_batch()
    {
        // Arrange — the batch shape is what makes an envelope all-or-nothing
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);

        // Act
        await Store.WriteAsync(new PropositionBatch([Stored("b")], ["a"]), default);

        // Assert
        Store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }

    [Fact]
    public async Task Should_read_the_same_rows_asynchronously_as_synchronously()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);

        // Act
        var asynchronous = await Store.LoadAsync(default);

        // Assert
        asynchronous.Select(row => row.Name).ShouldBe(Store.Load().Select(row => row.Name));
    }

    [Fact]
    public async Task Should_move_the_generation_when_a_write_lands()
    {
        // Arrange
        var before = await Store.GetGenerationAsync(default);

        // Act
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);

        // Assert
        (await Store.GetGenerationAsync(default)).ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task Should_leave_the_generation_still_when_a_batch_changes_nothing()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);
        var before = await Store.GetGenerationAsync(default);

        // Act — an empty batch is not a write
        await Store.WriteAsync(new PropositionBatch([], []), default);

        // Assert — a poller that rebuilt on this would rebuild forever
        (await Store.GetGenerationAsync(default)).ShouldBe(before);
    }
}
