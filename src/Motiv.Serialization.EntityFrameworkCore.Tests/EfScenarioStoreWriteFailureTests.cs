using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// A write that fails for a reason other than another replica moving the row is an operational
/// failure, and it surfaces as one: only a concurrency miss, or a failure after which the row
/// stands somewhere other than where the write assumed, is a conflict.
/// </summary>
public class EfScenarioStoreWriteFailureTests
{
    private static StoredScenario Scenario(string id = "s1") =>
        new("can-checkout", id, "Scenario", """{ "age": 30 }""", ExpectedSatisfied: null, SourceDecisionId: null, Version: 0, Author: "alice", TimestampUtc: DateTimeOffset.MinValue);

    [Fact]
    public async Task Should_surface_a_create_that_fails_for_a_reason_that_is_not_a_collision()
    {
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = new EfScenarioStore(fixture.FactoryWith(new ThrowOnSaveInterceptor()));

        var act = async () => await store.PutAsync(Scenario(), baseVersion: 0, default);

        (await act.ShouldThrowAsync<DbUpdateException>()).Message.ShouldContain("injected");
    }

    [Fact]
    public async Task Should_surface_an_update_that_fails_while_the_row_stands_where_the_write_assumed()
    {
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        (await new EfScenarioStore(fixture.Factory).PutAsync(Scenario(), baseVersion: 0, default)).Version.ShouldBe(1);
        var store = new EfScenarioStore(fixture.FactoryWith(new ThrowOnSaveInterceptor()));

        var act = async () => await store.PutAsync(Scenario(), baseVersion: 1, default);

        (await act.ShouldThrowAsync<DbUpdateException>()).Message.ShouldContain("injected");
    }

    [Fact]
    public async Task Should_surface_a_delete_that_fails_while_the_row_stands_where_the_write_assumed()
    {
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        (await new EfScenarioStore(fixture.Factory).PutAsync(Scenario(), baseVersion: 0, default)).Version.ShouldBe(1);
        var store = new EfScenarioStore(fixture.FactoryWith(new ThrowOnSaveInterceptor()));

        var act = async () => await store.DeleteAsync("can-checkout", "s1", baseVersion: 1, default);

        (await act.ShouldThrowAsync<DbUpdateException>()).Message.ShouldContain("injected");
    }

    [Fact]
    public async Task Should_still_report_a_concurrency_miss_as_a_conflict()
    {
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        (await new EfScenarioStore(fixture.Factory).PutAsync(Scenario(), baseVersion: 0, default)).Version.ShouldBe(1);
        var store = new EfScenarioStore(fixture.FactoryWith(new ThrowOnSaveInterceptor(concurrency: true)));

        var result = await store.PutAsync(Scenario(), baseVersion: 1, default);

        result.IsConflict.ShouldBeTrue();
        result.CurrentVersion.ShouldBe(1);
    }

    /// <summary>Stands in for the database refusing the write: a disk full, a missing table, a read-only file.</summary>
    private sealed class ThrowOnSaveInterceptor(bool concurrency = false) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw (concurrency
                ? new DbUpdateConcurrencyException("injected: the concurrency token missed")
                : new DbUpdateException("injected: the database refused the write"));
    }
}
