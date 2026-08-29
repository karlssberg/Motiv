using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Studio.Tests;

public class JsonFileRuleStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"motiv-rules-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", DateTimeOffset.UnixEpoch, null, null, "test");

    [Fact]
    public async Task Should_round_trip_the_log_through_the_file()
    {
        // Arrange
        var store = new JsonFileRuleStore(_path);
        await store.AppendAsync([Row("a", 1), Row("b", 1)], default);
        await store.AppendAsync([Row("a", 2, """{"v":2}""")], default);

        // Act — a fresh instance over the same file is what a restart looks like
        var heads = new JsonFileRuleStore(_path).Load();

        // Assert
        heads.Count.ShouldBe(2);
        heads.Single(h => h.Name == "a").Version.ShouldBe(2);
        heads.Single(h => h.Name == "a").DocumentJson.ShouldBe<string?>("""{"v":2}""");
    }

    [Fact]
    public async Task Should_enforce_the_primary_key_across_instances()
    {
        // Arrange — two "replicas" over one file, both at v1
        var first = new JsonFileRuleStore(_path);
        await first.AppendAsync([Row("a", 1)], default);

        var second = new JsonFileRuleStore(_path);

        // Act
        await first.AppendAsync([Row("a", 2, """{"winner":true}""")], default);
        var loser = await second.AppendAsync([Row("a", 2, """{"loser":true}""")], default);

        // Assert
        loser.IsConflict.ShouldBeTrue();
        loser.CurrentVersion.ShouldBe(2);
        first.Load().Single().DocumentJson.ShouldBe<string?>("""{"winner":true}""");
    }

    [Fact]
    public void Should_return_an_empty_log_when_the_file_does_not_exist()
    {
        // Act / Assert — a first boot is not an error
        new JsonFileRuleStore(_path).Load().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_move_the_generation_forward_across_instances()
    {
        // Arrange
        var store = new JsonFileRuleStore(_path);
        await store.AppendAsync([Row("a", 1)], default);
        var generation = await store.GetGenerationAsync(default);

        // Act
        await new JsonFileRuleStore(_path).AppendAsync([Row("a", 2)], default);

        // Assert — the fencing token must be derived from the file, not from instance state
        (await new JsonFileRuleStore(_path).GetGenerationAsync(default))
            .ShouldBeGreaterThan(generation);
    }

    [Fact]
    public async Task Should_land_no_row_of_a_batch_when_one_of_them_conflicts()
    {
        // Arrange
        var store = new JsonFileRuleStore(_path);
        await store.AppendAsync([Row("a", 1)], default);

        // Act — "b" is new, but "a" v1 is taken, and the batch is all-or-nothing
        var result = await store.AppendAsync([Row("b", 1), Row("a", 1)], default);

        // Assert — the refusal must happen before the write, or "b" would be half-published
        result.IsConflict.ShouldBeTrue();
        new JsonFileRuleStore(_path).Load().Select(head => head.Name).ShouldBe(["a"]);
    }

    [Fact]
    public async Task Should_report_the_history_of_one_rule_oldest_first()
    {
        // Arrange — appended out of order, and with a second name to be filtered out
        var store = new JsonFileRuleStore(_path);
        await store.AppendAsync([Row("a", 2), Row("b", 1)], default);
        await store.AppendAsync([Row("a", 1)], default);

        // Act
        var history = await new JsonFileRuleStore(_path).HistoryAsync("a", default);

        // Assert
        history.Select(row => row.Version).ShouldBe([1, 2]);
        (await store.HistoryAsync("unknown", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_refuse_to_read_an_unreadable_log_rather_than_overwrite_it()
    {
        // Arrange — a hand-edited or half-written file
        await File.WriteAllTextAsync(_path, "{ not json");
        var store = new JsonFileRuleStore(_path);

        // Act / Assert — unlike the proposition store, appending over this would destroy the
        // published history, so it refuses instead of continuing with an empty log
        Should.Throw<InvalidOperationException>(() => store.Load());
    }
}
