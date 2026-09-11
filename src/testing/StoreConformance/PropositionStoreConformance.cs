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
        await Store.WriteAsync(PropositionBatch.Delete("a", version: 1), default);

        // Assert
        Store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }

    [Fact]
    public async Task Should_refuse_deleting_an_absent_name()
    {
        // Act — a store holding no row is at no version at all, so no deletion can name its version.
        // A writer arriving here observed a row that another writer has already removed.
        var result = await Store.WriteAsync(PropositionBatch.Delete("absent", version: 1), default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("absent");
        result.CurrentVersion.ShouldBe(0);
    }

    [Fact]
    public async Task Should_write_a_save_and_a_delete_in_one_batch()
    {
        // Arrange — the batch shape is what makes an envelope all-or-nothing
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);

        // Act
        var result = await Store.WriteAsync(
            new PropositionBatch([Stored("b")], [new PropositionDeletion("a", 1)]), default);

        // Assert
        result.IsConflict.ShouldBeFalse();
        Store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }

    [Fact]
    public async Task Should_refuse_a_save_at_the_version_the_store_already_holds()
    {
        // Arrange — two replicas both read v1 and both compute v2; this is the second one
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 2)), default);

        // Act
        var result = await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 2)), default);

        // Assert — the lost update is what this refusal exists to make impossible
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("a");
        result.CurrentVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Should_refuse_a_save_at_a_version_the_store_has_already_passed()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 5)), default);

        // Act
        var result = await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 3)), default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.CurrentVersion.ShouldBe(5);
    }

    [Fact]
    public async Task Should_refuse_creating_a_name_the_store_already_holds()
    {
        // Arrange — a create always writes version 1
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Act
        var result = await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.CurrentVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Should_accept_a_save_whose_version_skips_ahead()
    {
        // Act — the predicate is "past the head", not "the head plus one": the importer copies rows
        // at the versions they already carry into a store that holds nothing.
        var result = await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 7)), default);

        // Assert
        result.IsConflict.ShouldBeFalse();
        Store.Load()[0].Version.ShouldBe(7);
    }

    [Fact]
    public async Task Should_refuse_a_deletion_at_a_stale_version()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 2)), default);

        // Act — a writer that read v1 and is only now withdrawing it
        var result = await Store.WriteAsync(PropositionBatch.Delete("a", version: 1), default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.CurrentVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Should_refuse_a_deletion_at_version_zero()
    {
        // Act — "the store holds no row" and "version 0" are the same value on the stored side, so
        // a deletion claiming version 0 would pass an equality check against an absent name. No row
        // has ever carried version 0 (creates start at 1), so no deletion can honestly name it: it is
        // refused outright rather than let through to remove nothing.
        var result = await Store.WriteAsync(PropositionBatch.Delete("absent", version: 0), default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("absent");
        result.CurrentVersion.ShouldBe(0);
    }

    [Fact]
    public async Task Should_refuse_a_batch_that_names_one_proposition_twice()
    {
        // Act — a batch that cannot say what it wants for a name is the same stale-writer signal as
        // one that wants something the store has moved past
        var result = await Store.WriteAsync(
            new PropositionBatch([Stored("a", version: 1), Stored("a", version: 2)], []), default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("a");
    }

    [Fact]
    public async Task Should_leave_the_whole_batch_unwritten_when_one_entry_conflicts()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Act — "b" is perfectly writable; "a" is not, and the batch is all-or-nothing
        var result = await Store.WriteAsync(
            new PropositionBatch([Stored("b", version: 1), Stored("a", version: 1)], []), default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        Store.Load().Select(proposition => proposition.Name).ShouldBe(["a"]);
    }

    [Fact]
    public async Task Should_leave_the_generation_still_when_a_batch_is_refused()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);
        var before = await Store.GetGenerationAsync(default);

        // Act
        var result = await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Assert — nothing landed, so nothing for a replica to converge on
        result.IsConflict.ShouldBeTrue();
        (await Store.GetGenerationAsync(default)).ShouldBe(before);
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
