using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// Seam: schema bootstrap for the zero-config path. <c>docker compose up</c> needs no migration step
/// and no database server, so the sample creates its own schema on startup — but it is also the
/// headline two-replica demo, and two replicas start at the same time.
/// </summary>
/// <remarks>
/// <para>
/// A bare <see cref="RelationalDatabaseFacadeExtensions.EnsureCreatedAsync"/> races: the first
/// instance creates the file and begins issuing <c>CREATE TABLE</c>, and the second sees a database
/// that exists with no tables in it yet and issues the same statements — one of them loses with
/// "table already exists". Scale the demo to two instances over a shared volume, or point two at one
/// PostgreSQL, and that is a cold-start crash on first boot.
/// </para>
/// <para>
/// So the schema is verified rather than assumed, and the verification — not the exception — decides.
/// A failure that left the schema complete was another instance getting there first, which is
/// logged and continued past. A failure that did not is rethrown with its original stack: a bad
/// connection string, an unwritable path and a permission error all land here, and every one of them
/// must still take the process down loudly. All three tables are checked, because a check of one
/// proves nothing about the other two.
/// </para>
/// <para>
/// The window this does not close: an instance that fails while another is still <em>mid</em>-creation
/// would find the schema genuinely incomplete and rethrow. On SQLite that cannot happen — the
/// creation runs as one transaction, so a rival either sees no tables or sees all of them, which is
/// why <c>StoreSchemaTests</c> can race eight instances and expect every one of them to come up.
/// Anything that widened that window would want a bounded retry here rather than a wider catch.
/// </para>
/// <para>
/// A production host does none of this — it applies adopter-owned migrations instead, which carry
/// their own history table and their own concurrency story. <c>EnsureCreated</c> and migrations
/// deliberately do not mix.
/// </para>
/// </remarks>
public static class StoreSchema
{
    /// <summary>The three tables the stores need, created if they are not already there.</summary>
    /// <param name="context">A context over the store's database.</param>
    /// <param name="logger">Where a lost creation race is reported.</param>
    /// <param name="cancellationToken">Cancels the creation and the verification.</param>
    /// <exception cref="InvalidOperationException">
    /// The schema is incomplete and nothing explained why — an existing database that holds other
    /// tables, which <c>EnsureCreated</c> takes as "already created" and leaves alone.
    /// </exception>
    public static async Task EnsureCreatedAsync(
        MotivStoreDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        Exception? creationFailure = null;
        try
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Held, not swallowed: whether it mattered is decided below, by looking at the schema.
            creationFailure = exception;
        }

        var missing = await MissingTablesAsync(context, cancellationToken);

        if (missing.Count == 0)
        {
            if (creationFailure is not null)
            {
                logger.LogWarning(
                    creationFailure,
                    "Creating the Motiv store schema failed, but all three tables are present and " +
                    "readable — another instance created them first. Continuing.");
            }

            return;
        }

        // The schema is not usable. If something threw, that is the real diagnosis and it is
        // rethrown with its original stack rather than replaced by a summary of the symptom.
        if (creationFailure is not null)
            ExceptionDispatchInfo.Capture(creationFailure).Throw();

        throw new InvalidOperationException(
            $"The Motiv store schema is incomplete: {string.Join(", ", missing)} could not be read, " +
            "and creating the schema reported no error. EnsureCreated treats a database that already " +
            "holds any table as already created, so this is what a store pointed at somebody else's " +
            "database looks like. Point it at its own database, or create the schema with migrations.");
    }

    /// <summary>
    /// Which of the three tables cannot be read. A trivial query per table rather than a metadata
    /// lookup, because that is provider-agnostic and answers the question actually being asked —
    /// can the stores use this schema.
    /// </summary>
    private static async Task<IReadOnlyList<string>> MissingTablesAsync(
        MotivStoreDbContext context, CancellationToken cancellationToken)
    {
        (string Table, Func<Task> Probe)[] probes =
        [
            ("MotivRuleVersion", () => context.RuleVersions.AsNoTracking().AnyAsync(cancellationToken)),
            ("MotivProposition", () => context.Propositions.AsNoTracking().AnyAsync(cancellationToken)),
            ("MotivStoreGeneration", () => context.StoreGenerations.AsNoTracking().AnyAsync(cancellationToken)),
        ];

        var missing = new List<string>();
        foreach (var (table, probe) in probes)
        {
            try
            {
                await probe();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                missing.Add(table);
            }
        }

        return missing;
    }
}
