using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class InMemoryRuleStoreTests
{
    // Built by hand rather than via DateTimeOffset.UnixEpoch — that static field is unavailable on
    // net472/netstandard2.0, two of this project's target frameworks.
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", Epoch, null, null, "test");

    [Fact]
    public async Task Should_project_the_head_from_the_highest_version()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 2, """{"v":2}""")], default);
        await store.AppendAsync([Row("a", 3, """{"v":3}""")], default);

        // Act
        var heads = store.Load();

        // Assert — head is a projection, never a stored duplicate, so it cannot diverge
        heads.ShouldHaveSingleItem();
        heads[0].Version.ShouldBe(3);
        heads[0].DocumentJson!.ShouldBe("""{"v":3}""");
    }

    [Fact]
    public async Task Should_keep_a_null_document_as_a_head_rather_than_an_absent_row()
    {
        // Arrange — a revert records that the rule went back to the compiled default
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 1, """{"v":1}""")], default);
        await store.AppendAsync([Row("a", 2, documentJson: null)], default);

        // Act
        var heads = store.Load();

        // Assert
        heads.ShouldHaveSingleItem();
        heads[0].Version.ShouldBe(2);
        heads[0].DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_reject_a_duplicate_name_and_version_as_a_conflict()
    {
        // Arrange — this is the cross-process compare-and-set: two replicas both computing next = 2
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 1)], default);
        await store.AppendAsync([Row("a", 2, """{"winner":true}""")], default);

        // Act
        var result = await store.AppendAsync([Row("a", 2, """{"loser":true}""")], default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("a");
        result.CurrentVersion.ShouldBe(2);
        store.Load()[0].DocumentJson!.ShouldBe("""{"winner":true}""");
    }

    [Fact]
    public async Task Should_append_a_whole_batch_or_none_of_it()
    {
        // Arrange — an envelope's rows must not land half-way; the second row conflicts
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("b", 1)], default);

        // Act
        var result = await store.AppendAsync([Row("a", 1), Row("b", 1)], default);

        // Assert — 'a' must not have landed
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("b");
        store.Load().ShouldHaveSingleItem();
        store.Load()[0].Name.ShouldBe("b");
    }

    [Fact]
    public async Task Should_move_the_generation_forward_on_every_successful_append()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var before = await store.GetGenerationAsync(default);

        // Act
        await store.AppendAsync([Row("a", 1)], default);
        var after = await store.GetGenerationAsync(default);

        // Assert
        after.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task Should_not_move_the_generation_on_a_rejected_append()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 1)], default);
        var before = await store.GetGenerationAsync(default);

        // Act
        await store.AppendAsync([Row("a", 1)], default);

        // Assert — a rejected write changed nothing, so replicas must not be told to rebuild
        (await store.GetGenerationAsync(default)).ShouldBe(before);
    }

    [Fact]
    public async Task Should_return_the_whole_history_of_a_name_in_version_order()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 2)], default);
        await store.AppendAsync([Row("a", 1)], default);

        // Act
        var history = await store.HistoryAsync("a", default);

        // Assert — kept forever, in order, so "what did v1 say?" is always answerable
        history.Select(row => row.Version).ShouldBe([1, 2]);
    }
}
