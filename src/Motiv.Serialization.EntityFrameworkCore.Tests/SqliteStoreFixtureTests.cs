using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// That the fixture cleans up after itself without reaching outside itself to do it.
/// </summary>
/// <remarks>
/// <para>
/// <see href="https://github.com/karlssberg/Motiv/issues/219">#219</see>: a conformance test failed
/// once in a full-solution run and never again alone, because teardown released connection handles
/// with <c>SqliteConnection.ClearAllPools()</c> — which is process-global, while each fixture owns a
/// private GUID-named database file. xunit runs test classes in parallel, so one class's teardown
/// reached into every other live fixture's pool, disposing the <c>SQLitePCL.sqlite3</c> handle
/// underneath a connection another class had already leased and was about to open.
/// </para>
/// <para>
/// The reason anyone reaches for that clear is this test's subject: a pooled connection keeps the
/// database file open, so teardown's <c>File.Delete</c> fails on Windows. Take no pooled connection
/// and there is nothing to release — which is what <c>Pooling=False</c> buys, and what
/// <c>SqliteDecisionFixture</c> in Motiv.Serialization.Sql.Tests had been doing alone all along.
/// </para>
/// <para>
/// The guard against the clear itself returning is <c>PoolClearGateTests</c>, compiled into this
/// assembly from <c>src/testing/PoolClearGate</c>. The two are a pair: that one says the shortcut is
/// not taken, this one says the shortcut is not needed.
/// </para>
/// </remarks>
public class SqliteStoreFixtureTests
{
    /// <summary>
    /// Teardown removes the file, having opened real connections over it first.
    /// </summary>
    /// <remarks>
    /// Deterministic on every platform, but its teeth are on Windows, which refuses to delete a file
    /// with an open handle — there the failure arrives as an <c>IOException</c> out of
    /// <c>DisposeAsync</c> rather than as this assertion. macOS and Linux unlink an open file happily,
    /// so a green run here is not local evidence that the handle was released; CI is.
    /// </remarks>
    [Fact]
    public async Task Should_delete_its_database_file_at_teardown()
    {
        // Arrange — a write and a read, so connections are genuinely opened and returned
        var fixture = await SqliteStoreFixture.CreateAsync();
        var store = new EfPropositionStore(fixture.Factory);
        await store.WriteAsync(
            PropositionBatch.Save(new StoredProposition("a", "customer", "{}", 1, null)), default);
        store.Load().Count.ShouldBe(1);

        var path = fixture.DatabasePath;
        File.Exists(path).ShouldBeTrue();

        // Act
        await fixture.DisposeAsync();

        // Assert
        File.Exists(path).ShouldBeFalse(
            "teardown must be able to delete the file without a process-global pool clear (#219)");
    }
}
