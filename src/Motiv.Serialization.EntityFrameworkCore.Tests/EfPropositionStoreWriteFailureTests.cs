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
/// rule side has no need of: proof that the concurrency token the whole design rests on is actually
/// live against the database, since a mapping that emits no DDL is exactly the kind of claim nothing
/// else would check.
/// </summary>
public class EfPropositionStoreWriteFailureTests
{
    private static StoredProposition Row(string name, int version, string documentJson = "{}") =>
        new(name, "customer", documentJson, version, null);

    [Fact]
    public async Task Should_refuse_an_update_whose_row_was_replaced_underneath_it()
    {
        // Arrange — the mechanism, not the store: `Version` is mapped as a concurrency token in
        // MotivStoreDbContext, and this is the direct proof that EF honours it against SQLite. A
        // tracked entity is read with no transaction open (SQLite releases its shared lock at the end
        // of the statement), a second connection replaces the row and commits, and the first then
        // saves its own edit against the version it originally read. Without the token that UPDATE
        // carries no version predicate and silently wins; with it, it matches no rows.
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await new EfPropositionStore(fixture.Factory).WriteAsync(PropositionBatch.Save(Row("a", 1)), default);

        await using var stale = fixture.Factory.CreateDbContext();
        var tracked = await stale.Propositions.SingleAsync(row => row.Name == "a");

        await using (var fresh = fixture.Factory.CreateDbContext())
        {
            var current = await fresh.Propositions.SingleAsync(row => row.Name == "a");
            current.Version = 2;
            await fresh.SaveChangesAsync();
        }

        tracked.Version = 2;
        tracked.DocumentJson = """{"stale":true}""";

        // Act
        var act = async () => await stale.SaveChangesAsync();

        // Assert — the loser matches no rows, which EF reports as this and nothing else
        await act.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Should_return_a_conflict_when_the_reread_after_rollback_finds_it_was_a_race()
    {
        // Arrange — the same shape as EfRuleStoreAppendFailureTests, and for the same reason: a
        // physical two-connection race cannot exercise this deterministically, because the pre-read
        // runs inside an open transaction and on SQLite a second connection cannot commit until it
        // ends. WriteAsync's own logic depends on two facts, not on timing: "SaveChangesAsync threw"
        // and "the fresh reread, after rollback, found the store past our version". Both are
        // reproduced directly: an interceptor throws the exception the concurrency token would
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
    /// Throws on the first save only, then behaves normally. Throws the concurrency subclass rather
    /// than the base type because that is what the token actually raises — and because the store's
    /// catch is written against the base, this is also what proves the subclass reaches it.
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
            throw new DbUpdateConcurrencyException(
                "injected: stands in for the concurrency token matching no rows, so the " +
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
