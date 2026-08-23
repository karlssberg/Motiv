using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// The bundle's cross-process obligations: two stores over one database are two replicas.
/// </summary>
/// <remarks>
/// Deliberately sequential rather than thread-racing. The lost update ticket 21 describes is a
/// <em>stale</em> replica computing the same next version, not a nanosecond-level tie — and a
/// thread-racing test against SQLite would trade a real assertion for a flaky one, on a CI that
/// also runs Windows.
/// </remarks>
public class CrossReplicaTests
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", Epoch, null, null, "test");

    [Fact]
    public async Task Should_let_exactly_one_of_two_replicas_take_a_version()
    {
        // Arrange — both replicas hold v1 and both compute next = 2
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var replicaA = new EfRuleStore(fixture.Factory);
        var replicaB = new EfRuleStore(fixture.Factory);
        await replicaA.AppendAsync([Row("a", 1)], default);

        // Act
        var first = await replicaA.AppendAsync([Row("a", 2, """{"winner":true}""")], default);
        var second = await replicaB.AppendAsync([Row("a", 2, """{"loser":true}""")], default);

        // Assert — one published, one rejected, and the log says so
        first.IsConflict.ShouldBeFalse();
        second.IsConflict.ShouldBeTrue();
        second.CurrentVersion.ShouldBe(2);

        var history = await replicaB.HistoryAsync("a", default);
        history.Select(row => row.Version).ShouldBe([1, 2]);
        history[1].DocumentJson!.ShouldBe("""{"winner":true}""");
    }

    [Fact]
    public async Task Should_report_the_current_version_to_a_stale_writer()
    {
        // Arrange — a replica that has not refreshed since v3 was published
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = new EfRuleStore(fixture.Factory);
        await store.AppendAsync([Row("a", 1)], default);
        await store.AppendAsync([Row("a", 2)], default);
        await store.AppendAsync([Row("a", 3)], default);

        // Act — it still believes the head is v1, so it offers v2
        var result = await store.AppendAsync([Row("a", 2)], default);

        // Assert — the rejection carries where the store actually is, so the caller can rebase
        result.IsConflict.ShouldBeTrue();
        result.CurrentVersion.ShouldBe(3);
    }

    [Fact]
    public async Task Should_leave_nothing_live_when_a_persist_fails()
    {
        // Arrange — Author is NOT NULL, so this batch fails at the database rather than at the
        // conflict check: the path that must rethrow rather than report a version conflict
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = new EfRuleStore(fixture.Factory);
        await store.AppendAsync([Row("a", 1)], default);
        var generationBefore = await store.GetGenerationAsync(default);

        var illegal = new StoredRuleVersion("b", 1, "{}", null!, Epoch, null, null, null);

        // Act
        var act = async () => await store.AppendAsync([illegal], default);

        // Assert — it throws rather than returning a conflict, and nothing landed
        await act.ShouldThrowAsync<DbUpdateException>();
        (await store.GetGenerationAsync(default)).ShouldBe(generationBefore);
        store.Load().ShouldHaveSingleItem().Name.ShouldBe("a");
    }

    [Fact]
    public async Task Should_move_one_generation_without_moving_the_other()
    {
        // Arrange — the two stores are never written in the same transaction, so their generations
        // are independent; a shared counter would make every proposition write rebuild every rule
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var rules = new EfRuleStore(fixture.Factory);
        var propositions = new EfPropositionStore(fixture.Factory);
        var propositionGenerationBefore = await propositions.GetGenerationAsync(default);

        // Act
        await rules.AppendAsync([Row("a", 1)], default);

        // Assert
        (await rules.GetGenerationAsync(default)).ShouldBeGreaterThan(0);
        (await propositions.GetGenerationAsync(default)).ShouldBe(propositionGenerationBefore);
    }
}
