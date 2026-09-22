using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// The two branches of <see cref="EfRuleStore.AppendAsync"/> that <see cref="CrossReplicaTests"/>
/// does not reach: the classification decision after a <see cref="DbUpdateException"/> (did we
/// lose a real race, or did something else go wrong?), and the empty-batch short-circuit.
/// </summary>
public class EfRuleStoreAppendFailureTests
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", Epoch, null, null, "test");

    [Fact]
    public async Task Should_return_a_conflict_when_the_reread_after_rollback_finds_it_was_a_race()
    {
        // Arrange — CrossReplicaTests.Should_leave_nothing_live_when_a_persist_fails already covers
        // the sibling branch (the reread finds nothing, so the DbUpdateException is rethrown). This
        // covers the other side: the reread finds our exact (Name, Version), so it was a real race
        // rather than some other failure, and AppendAsync must report it as a conflict instead.
        //
        // A physical two-connection race cannot exercise this deterministically here: the pre-read
        // runs inside an open transaction, so on SQLite a second connection cannot commit until that
        // transaction ends, and CI also runs Windows where the timing would differ again. Nothing in
        // AppendAsync's own logic depends on wall-clock timing, though — it depends on two facts:
        // "SaveChangesAsync threw DbUpdateException" and "the fresh reread, after rollback, saw a
        // row we now recognise as ours". Both are reproduced directly below, without any thread race:
        // a SaveChangesInterceptor throws DbUpdateException on the first save, and the context
        // factory — on its second call, which production code only reaches after RollbackAsync, so
        // no lock is held — inserts and commits the "other replica"'s row through a separate,
        // unintercepted context before handing back a context that can see it. What this proves is
        // the classification decision itself, not that a real race can happen; that a real race can
        // happen is what Should_let_exactly_one_of_two_replicas_take_a_version proves.
        await using var fixture = await SqliteStoreFixture.CreateAsync();

        var seed = new EfRuleStore(fixture.Factory);
        await seed.AppendAsync([Row("a", 1)], default);

        var winner = new EfRuleStore(fixture.Factory);
        var racingFactory = new RacingContextFactory(
            firstCall: fixture.FactoryWith(new ThrowOnFirstSaveInterceptor()),
            laterCalls: fixture.Factory,
            beforeSecondCall: () => winner.AppendAsync(
                [Row("a", 2, """{"winner":true}""")], default).GetAwaiter().GetResult());

        var loser = new EfRuleStore(racingFactory);

        // Act — this replica also believes v1 is the head and also offers v2
        var result = await loser.AppendAsync([Row("a", 2, """{"loser":true}""")], default);

        // Assert — reported as a conflict, not rethrown, and the winner's row is what actually landed
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("a");
        result.CurrentVersion.ShouldBe(2);

        var history = await winner.HistoryAsync("a", default);
        history.Select(row => row.Version).ShouldBe([1, 2]);
        history[1].DocumentJson!.ShouldBe("""{"winner":true}""");
    }

    [Fact]
    public async Task Should_not_touch_the_database_for_an_empty_batch()
    {
        // Arrange — an empty batch returns Appended without opening a context at all: moving the
        // generation would make every replica rebuild its whole world, on a timer, for nothing
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = new EfRuleStore(fixture.Factory);
        var generationBefore = await store.GetGenerationAsync(default);

        // Act
        var result = await store.AppendAsync([], default);

        // Assert
        result.IsConflict.ShouldBeFalse();
        (await store.GetGenerationAsync(default)).ShouldBe(generationBefore);
        store.Load().ShouldBeEmpty();
    }

    /// <summary>Throws <see cref="DbUpdateException"/> on the first save only, then behaves normally.</summary>
    private sealed class ThrowOnFirstSaveInterceptor : SaveChangesInterceptor
    {
        private bool _thrown;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_thrown)
                return base.SavingChangesAsync(eventData, result, cancellationToken);

            _thrown = true;
            throw new DbUpdateException(
                "injected: stands in for a real cross-connection race so the classification " +
                "branch below is reachable without timing dependence");
        }
    }

    /// <summary>
    /// Hands out <paramref name="firstCall"/>'s context once, then <paramref name="laterCalls"/>'s
    /// from then on — running <paramref name="beforeSecondCall"/> just before the second handout,
    /// which is exactly when production code has already rolled back and so holds no lock.
    /// </summary>
    private sealed class RacingContextFactory(
        IDbContextFactory<MotivStoreDbContext> firstCall,
        IDbContextFactory<MotivStoreDbContext> laterCalls,
        Action beforeSecondCall) : IDbContextFactory<MotivStoreDbContext>
    {
        private int _calls;

        public MotivStoreDbContext CreateDbContext()
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == 1)
                return firstCall.CreateDbContext();

            if (call == 2)
                beforeSecondCall();

            return laterCalls.CreateDbContext();
        }
    }
}
