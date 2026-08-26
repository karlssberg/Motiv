using System.Text.Json;

namespace Motiv.Serialization.Sql;

/// <summary>
/// How the durable sink writes, and — the part that is not a preference — how long it keeps what it
/// wrote.
/// </summary>
/// <remarks>
/// Two of these have no default and are refused at construction: <see cref="Retention"/> and
/// <see cref="Dialect"/>. Everything else is a dial with a sensible setting.
/// </remarks>
public sealed class SqlDecisionSinkOptions
{
    private TimeSpan? _retention;
    private TimeSpan _purgeInterval = TimeSpan.FromHours(1);
    private int _purgeBatchSize = 5_000;

    /// <summary>
    /// How long a decision record is kept. <strong>Required</strong> — a sink constructed without one
    /// throws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Version history is kept forever; this is the record that is genuinely unbounded, because an
    /// audited rule on a hot path is millions of rows. So there is no default: a window defaulting to
    /// something sensible would be the product choosing an adopter's compliance posture for them, and
    /// one defaulting to zero would satisfy the letter of "a window was set" while deleting
    /// everything.
    /// </para>
    /// <para>
    /// GDPR minimisation pushes it short; a financial-audit regime pushes it to years. A record past
    /// the window cannot be replayed, which is the <em>correct</em> post-retention state rather than a
    /// loss.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive, or is infinite — <see cref="Timeout.InfiniteTimeSpan"/> being the
    /// obvious way to spell "keep forever", which is the one thing this must not allow.
    /// </exception>
    public TimeSpan? Retention
    {
        get => _retention;
        set => _retention = value is null || (value > TimeSpan.Zero && value < TimeSpan.MaxValue)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "Retention must be a positive, finite window. There is no 'keep forever' here — " +
                "an audited rule on a hot path is millions of rows.");
    }

    /// <summary>
    /// Which engine's SQL to write. <strong>Required</strong> — a sink constructed without one throws.
    /// </summary>
    /// <remarks>
    /// Not defaulted to <see cref="DecisionSqlDialect.Sqlite"/>, tempting as that is for the
    /// zero-config path: the connection factory says nothing about the engine behind it, so a default
    /// would be a guess that fails at the first write rather than at startup.
    /// </remarks>
    public DecisionSqlDialect? Dialect { get; set; }

    /// <summary>How often the background purge runs. Defaults to one hour.</summary>
    /// <remarks>
    /// The first pass waits out one interval rather than running at startup, so a host discovers an
    /// unreachable decision database through its readiness probe rather than through a purge failure
    /// in its first second.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan PurgeInterval
    {
        get => _purgeInterval;
        set => _purgeInterval = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "PurgeInterval must be positive.");
    }

    /// <summary>The most rows one delete statement takes. Defaults to 5,000.</summary>
    /// <remarks>
    /// A purge pass keeps issuing bounded deletes until nothing is left, so this is not a cap on what
    /// a pass removes — it is how long any one statement holds a lock. After a long outage the
    /// difference between the two is the difference between a slow purge and a stalled table.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int PurgeBatchSize
    {
        get => _purgeBatchSize;
        set => _purgeBatchSize = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "PurgeBatchSize must be at least 1.");
    }

    /// <summary>
    /// Whether the two tables are created on first use. Defaults to true — the zero-config path, and
    /// what makes <c>docker compose up</c> work against an empty file.
    /// </summary>
    /// <remarks>
    /// Turn it off where DDL is a deployment concern rather than an application one, and call
    /// <see cref="SqlDecisionSink.EnsureSchemaAsync"/> from a migration step instead.
    /// </remarks>
    public bool EnsureSchema { get; set; } = true;

    /// <summary>
    /// How the outcome, the referenced proposition versions and the captured input are serialised.
    /// </summary>
    /// <remarks>
    /// The captured input is whatever the adopter's posture kept of their model, so this is the seam
    /// where a converter for their own types goes. What comes back out is a <c>JsonElement</c>
    /// regardless: the alternative is a type discriminator in the log, which would pin the adopter's
    /// assembly identity into their compliance record.
    /// </remarks>
    public JsonSerializerOptions? JsonOptions { get; set; }

    /// <summary>The clock the retention cutoff is measured from. Injected so tests need not wait.</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;
}
