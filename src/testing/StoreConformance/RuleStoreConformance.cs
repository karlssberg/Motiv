using System;
using System.Linq;
using System.Threading.Tasks;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Testing;

/// <summary>
/// What it means to be an <see cref="IRuleStore"/>, as one suite every implementation derives from.
/// </summary>
/// <remarks>
/// <see cref="InMemoryRuleStore"/> claims that "a test written against it holds against Postgres".
/// This class is what makes that claim structural rather than a comment: the same behaviours run
/// against the in-memory store, the JSON file store and the EF Core store, so a divergence between
/// them is a failing test rather than a discovery in production.
/// </remarks>
public abstract class RuleStoreConformance : IAsyncLifetime
{
    // Built by hand rather than via DateTimeOffset.UnixEpoch — that static field is unavailable on
    // net472/netstandard2.0, two of Motiv.Serialization.Tests' target frameworks.
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The store under test. A fresh, empty one per test.</summary>
    protected IRuleStore Store { get; private set; } = null!;

    /// <summary>Creates an empty store. Called once per test.</summary>
    protected abstract Task<IRuleStore> CreateStoreAsync();

    /// <summary>Releases whatever <see cref="CreateStoreAsync"/> allocated. Does nothing by default.</summary>
    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    /// <summary>One version row, with the identity facts a test does not care about filled in.</summary>
    protected static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", Epoch, null, null, "test");

    public async Task InitializeAsync() => Store = await CreateStoreAsync();

    public Task DisposeAsync() => DisposeStoreAsync();

    [Fact]
    public async Task Should_project_the_head_from_the_highest_version()
    {
        // Arrange
        await Store.AppendAsync([Row("a", 2, """{"v":2}""")], default);
        await Store.AppendAsync([Row("a", 3, """{"v":3}""")], default);

        // Act
        var heads = Store.Load();

        // Assert — head is a projection, never a stored duplicate, so it cannot diverge
        heads.ShouldHaveSingleItem();
        heads[0].Version.ShouldBe(3);
        heads[0].DocumentJson!.ShouldBe("""{"v":3}""");
    }

    [Fact]
    public async Task Should_keep_a_null_document_as_a_head_rather_than_an_absent_row()
    {
        // Arrange — a revert records that the rule went back to the compiled default
        await Store.AppendAsync([Row("a", 1, """{"v":1}""")], default);
        await Store.AppendAsync([Row("a", 2, documentJson: null)], default);

        // Act
        var heads = Store.Load();

        // Assert
        heads.ShouldHaveSingleItem();
        heads[0].Version.ShouldBe(2);
        heads[0].DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_reject_a_duplicate_name_and_version_as_a_conflict()
    {
        // Arrange — this is the cross-process compare-and-set: two replicas both computing next = 2
        await Store.AppendAsync([Row("a", 1)], default);
        await Store.AppendAsync([Row("a", 2, """{"winner":true}""")], default);

        // Act
        var result = await Store.AppendAsync([Row("a", 2, """{"loser":true}""")], default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("a");
        result.CurrentVersion.ShouldBe(2);
        Store.Load()[0].DocumentJson!.ShouldBe("""{"winner":true}""");
    }

    [Fact]
    public async Task Should_append_a_whole_batch_or_none_of_it()
    {
        // Arrange — an envelope's rows must not land half-way; the second row conflicts
        await Store.AppendAsync([Row("b", 1)], default);

        // Act
        var result = await Store.AppendAsync([Row("a", 1), Row("b", 1)], default);

        // Assert — 'a' must not have landed
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("b");
        Store.Load().ShouldHaveSingleItem();
        Store.Load()[0].Name.ShouldBe("b");
    }

    [Fact]
    public async Task Should_move_the_generation_forward_on_every_successful_append()
    {
        // Arrange
        var before = await Store.GetGenerationAsync(default);

        // Act
        await Store.AppendAsync([Row("a", 1)], default);
        var after = await Store.GetGenerationAsync(default);

        // Assert
        after.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task Should_not_move_the_generation_on_a_rejected_append()
    {
        // Arrange
        await Store.AppendAsync([Row("a", 1)], default);
        var before = await Store.GetGenerationAsync(default);

        // Act
        await Store.AppendAsync([Row("a", 1)], default);

        // Assert — a rejected write changed nothing, so replicas must not be told to rebuild
        (await Store.GetGenerationAsync(default)).ShouldBe(before);
    }

    [Fact]
    public async Task Should_return_the_whole_history_of_a_name_in_version_order()
    {
        // Arrange
        await Store.AppendAsync([Row("a", 2)], default);
        await Store.AppendAsync([Row("a", 1)], default);

        // Act
        var history = await Store.HistoryAsync("a", default);

        // Assert — kept forever, in order, so "what did v1 say?" is always answerable
        history.Select(row => row.Version).ShouldBe([1, 2]);
    }

    [Fact]
    public async Task Should_read_the_same_heads_asynchronously_as_synchronously()
    {
        // Arrange — Load and LoadAsync are separate methods for startup vs refresh, not two answers
        await Store.AppendAsync([Row("a", 1)], default);

        // Act
        var asynchronous = await Store.LoadAsync(default);

        // Assert
        asynchronous.Select(head => head.Name).ShouldBe(Store.Load().Select(head => head.Name));
    }
}
