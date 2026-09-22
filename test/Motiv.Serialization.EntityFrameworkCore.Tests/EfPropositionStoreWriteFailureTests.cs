using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// The branches of <see cref="EfPropositionStore.WriteAsync"/> that the conformance suite does not
/// reach — the proposition-side twin of <see cref="EfRuleStoreAppendFailureTests"/>: the
/// classification decision after a <see cref="DbUpdateException"/> (did we lose a real race, or did
/// something else go wrong?), both of its answers, and the empty-batch short-circuit. Plus one the
/// rule side already has in its own file: proof that the <c>(Name, Version)</c> primary key the whole
/// design rests on is actually enforced against the database, so a duplicate version surfaces as the
/// <see cref="DbUpdateException"/> the store classifies.
/// </summary>
public class EfPropositionStoreWriteFailureTests
{
    private static StoredProposition Row(string name, int version, string documentJson = "{}") =>
        new(name, "customer", documentJson, version, null);

    [Fact]
    public async Task Should_refuse_a_second_row_at_a_version_the_log_already_holds()
    {
        // Arrange — the mechanism, not the store: the (Name, Version) primary key is what makes the
        // version a compare-and-set across processes, and this is the direct proof that EF surfaces a
        // violation of it against SQLite. Two contexts each add ("a", 2); the second to save matches
        // an existing key and is refused, which EF reports as a DbUpdateException the store turns
        // into a conflict.
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await new EfPropositionStore(fixture.Factory).WriteAsync(PropositionBatch.Save(Row("a", 1)), default);

        var contender = StoredPropositionVersion.Saved(Row("a", 2), RuleChangeProvenance.System, DateTimeOffset.UtcNow);

        await using (var first = fixture.Factory.CreateDbContext())
        {
            first.PropositionVersions.Add(contender.ToRow());
            await first.SaveChangesAsync();
        }

        await using var second = fixture.Factory.CreateDbContext();
        second.PropositionVersions.Add((contender with { DocumentJson = """{"stale":true}""" }).ToRow());

        // Act
        var act = async () => await second.SaveChangesAsync();

        // Assert
        await act.ShouldThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Should_return_a_conflict_when_the_reread_after_rollback_finds_it_was_a_race()
    {
        // Arrange — the same shape as EfRuleStoreAppendFailureTests, and for the same reason: a
        // physical two-connection race cannot exercise this deterministically, because the pre-read
        // runs inside an open transaction and on SQLite a second connection cannot commit until it
        // ends. WriteAsync's own logic depends on two facts, not on timing: "SaveChangesAsync threw"
        // and "the fresh reread, after rollback, found the store past our version". Both are
        // reproduced directly: an interceptor throws the exception a primary-key violation would
        // raise, and the context factory — on its second call, which production code only reaches
        // after RollbackAsync — lets the "other replica" commit v2 through a separate context first.
        await using var fixture = await SqliteStoreFixture.CreateAsync();

        var seed = new EfPropositionStore(fixture.Factory);
        await seed.WriteAsync(PropositionBatch.Save(Row("a", 1)), default);

        var winner = new EfPropositionStore(fixture.Factory);
        var racingFactory = new RacingContextFactory(
            firstCall: fixture.FactoryWith(new ThrowOnFirstSaveInterceptor()),
            laterCalls: fixture.Factory,
            beforeSecondCall: () => winner.WriteAsync(
                PropositionBatch.Save(Row("a", 2, """{"winner":true}""")), default).GetAwaiter().GetResult());

        var loser = new EfPropositionStore(racingFactory);

        // Act — this replica also read v1 and also offers v2
        var result = await loser.WriteAsync(
            PropositionBatch.Save(Row("a", 2, """{"loser":true}""")), default);

        // Assert — reported as a conflict, not rethrown, and the winner's row is what actually landed
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("a");
        result.CurrentVersion.ShouldBe(2);

        var stored = winner.Load().ShouldHaveSingleItem();
        stored.Version.ShouldBe(2);
        stored.DocumentJson.ShouldBe("""{"winner":true}""");
    }

    [Fact]
    public async Task Should_rethrow_when_the_reread_after_rollback_finds_no_conflict()
    {
        // Arrange — the sibling branch: the save threw, but the reread finds the store exactly where
        // we left it, so this was not a race. A full disk or a dropped connection must surface as
        // what it is, not be dressed up as a version conflict a caller would then retry forever.
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = new EfPropositionStore(fixture.FactoryWith(new ThrowOnFirstSaveInterceptor()));

        // Act
        var act = async () => await store.WriteAsync(PropositionBatch.Save(Row("a", 1)), default);

        // Assert — and nothing landed, because the transaction was rolled back
        await act.ShouldThrowAsync<DbUpdateException>();
        new EfPropositionStore(fixture.Factory).Load().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_not_touch_the_database_for_an_empty_batch()
    {
        // Arrange — an empty batch returns Written without opening a context at all: moving the
        // generation would make every replica rebuild its whole world, on a timer, for nothing
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = new EfPropositionStore(fixture.Factory);
        var generationBefore = await store.GetGenerationAsync(default);

        // Act
        var result = await store.WriteAsync(new PropositionBatch([], []), default);

        // Assert
        result.IsConflict.ShouldBeFalse();
        (await store.GetGenerationAsync(default)).ShouldBe(generationBefore);
        store.Load().ShouldBeEmpty();
    }

    /// <summary>
    /// Throws on the first save only, then behaves normally. Throws <see cref="DbUpdateException"/>
    /// because that is what a duplicate <c>(Name, Version)</c> row raises — a primary-key violation,
    /// now that the version log carries no concurrency token — and the store's catch is written
    /// against exactly that.
    /// </summary>
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
                "injected: stands in for the (Name, Version) key refusing a duplicate row, so the " +
                "classification branch is reachable without timing dependence");
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
