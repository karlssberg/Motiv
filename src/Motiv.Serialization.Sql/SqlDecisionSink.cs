using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace Motiv.Serialization.Sql;

/// <summary>
/// A decision sink that appends to a SQL database and enforces a retention window on a loop of its
/// own. The durable half of the decision log: where <c>InMemoryDecisionSink</c> is the reference
/// implementation, this is the one a record survives a restart in.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Its own database.</strong> The decision log is a separate sink, a separate connection, and
/// may be a separate engine — its volume is machine-rate against the authoring store's human-rate, so
/// a decision-write storm co-located with authoring would degrade authoring reads; and its retention
/// is a compliance window against version history's <em>forever</em>. Point this at the authoring
/// database and both properties are lost.
/// </para>
/// <para>
/// <strong>No provider dependency.</strong> Written against <see cref="DbConnection"/>, with the
/// connection supplied by the adopter and the engine's SQL supplied by a
/// <see cref="DecisionSqlDialect"/>. A connection is opened per operation and closed after it, which
/// is what pooling is for and what keeps a long-idle sink from holding a dead socket.
/// </para>
/// <para>
/// <strong>It does not close the crash-loss window.</strong> The queue in front of it
/// (<c>DecisionLogOptions.QueueCapacity</c>) is bounded by construction; a durable sink narrows that
/// window, it does not close it. True zero-loss is an <c>IDecisionSink</c> over a durable
/// <em>queue</em> — an outbox or a broker — which is an adopter's implementation of this same seam.
/// </para>
/// </remarks>
public sealed class SqlDecisionSink : IDecisionSink, IAsyncDisposable
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly SqlDecisionSinkOptions _options;
    private readonly DecisionSqlDialect _dialect;
    private readonly DecisionStatements _statements;
    private readonly TimeSpan _retention;
    private readonly CancellationTokenSource _stopped = new();
    private readonly Task _purging;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    private long _purgedCount;
    private long _failedPurgeCount;
    private long _lastPurgeTicks;
    private bool _schemaReady;
    private bool _disposed;

    /// <summary>Creates a sink over <paramref name="connectionFactory"/>.</summary>
    /// <param name="connectionFactory">
    /// Opens a connection to the decision database — closed, not open; the sink opens and closes it.
    /// </param>
    /// <param name="options">
    /// The retention window and the dialect, both required, plus the purge dials.
    /// </param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="SqlDecisionSinkOptions.Retention"/> or <see cref="SqlDecisionSinkOptions.Dialect"/>
    /// was not set. <c>IDecisionSink</c> asks implementations to fail fast at construction rather than
    /// on the writer loop, and these two are exactly why: a missing retention window is not
    /// recoverable at 3am, and is entirely recoverable at startup.
    /// </exception>
    public SqlDecisionSink(Func<DbConnection> connectionFactory, SqlDecisionSinkOptions options)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _retention = options.Retention ?? throw new ArgumentException(
            $"A decision sink needs a {nameof(SqlDecisionSinkOptions.Retention)} window. Version " +
            "history is kept forever; decision records are not, so there is no default to fall back " +
            "on.", nameof(options));

        _dialect = options.Dialect ?? throw new ArgumentException(
            $"A decision sink needs a {nameof(SqlDecisionSinkOptions.Dialect)}. The connection " +
            "factory says nothing about the engine behind it, so there is nothing to infer one from.",
            nameof(options));

        _statements = new DecisionStatements(_dialect);

        // Started here, as DecisionLog starts its writer loop, and for the same reason one level up:
        // a purge an adopter has to register separately is a purge an adopter can omit, and an
        // omitted purge is the unbounded table the mandatory window exists to prevent.
        _purging = Task.Run(PurgeLoopAsync);
    }

    /// <summary>Decision records this sink has purged since it was created.</summary>
    public long PurgedCount => Interlocked.Read(ref _purgedCount);

    /// <summary>
    /// Purge passes that failed. A rising count is a window that is no longer being enforced, which
    /// is worth an alert: nothing else will say so, and the table simply grows.
    /// </summary>
    public long FailedPurgeCount => Interlocked.Read(ref _failedPurgeCount);

    /// <summary>When the last purge pass completed, or null if none has yet.</summary>
    public DateTimeOffset? LastPurgeUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastPurgeTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Creates the two tables and their indexes if they are absent, and does nothing if they are not.
    /// </summary>
    /// <remarks>
    /// Called for you before the first write when
    /// <see cref="SqlDecisionSinkOptions.EnsureSchema"/> is on. Call it yourself at startup to find
    /// out at startup — the writer loop swallows a failed batch into
    /// <c>DecisionLog.FailedBatchCount</c>, so a database that was never reachable is quieter there
    /// than it should be.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the bootstrap.</param>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await _schemaLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            foreach (var statement in _statements.Schema)
            {
                await using var command = Command(connection, statement);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
            return;

        await EnsureSchemaOnceAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // One transaction per batch, so a batch is all-or-nothing. That is what makes the failure
        // accounting honest: DecisionLog counts a refused batch and moves on, and a half-written
        // batch would leave the count describing something that did not happen.
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, _statements.InsertDecision, transaction);

        var parameters = DeclareParameters(command,
            DecisionSchema.Id,
            DecisionSchema.CorrelationId,
            DecisionSchema.TimestampUtc,
            DecisionSchema.Caller,
            DecisionSchema.RuleName,
            DecisionSchema.RuleVersion,
            DecisionSchema.BuildId,
            DecisionSchema.PropositionsJson,
            DecisionSchema.InputKind,
            DecisionSchema.InputJson,
            DecisionSchema.Satisfied,
            DecisionSchema.OutcomeJson);

        foreach (var record in records)
        {
            parameters[0].Value = _dialect.ToParameter(record.Id);
            parameters[1].Value = record.CorrelationId;
            parameters[2].Value = _dialect.ToParameter(record.TimestampUtc);
            parameters[3].Value = (object?)record.Caller ?? DBNull.Value;
            parameters[4].Value = record.RuleName;
            parameters[5].Value = record.RuleVersion;
            parameters[6].Value = record.BuildId;
            parameters[7].Value = Serialize(record.ReferencedPropositionVersions);
            parameters[8].Value = record.Input is null ? DBNull.Value : (int)record.Input.Kind;
            parameters[9].Value = record.Input is null ? DBNull.Value : Serialize(record.Input.Value);
            parameters[10].Value = _dialect.ToParameter(record.Outcome.Satisfied);
            parameters[11].Value = Serialize(record.Outcome);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gap);

        // Written with the same care as a batch, and for a sharper reason: a gap marker that failed
        // to persist turns a provable hole back into an invisible one, which is the exact ambiguity
        // the marker exists to remove.
        await EnsureSchemaOnceAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, _statements.InsertGap);

        var parameters = DeclareParameters(command,
            DecisionSchema.Id,
            DecisionSchema.FirstDroppedUtc,
            DecisionSchema.LastDroppedUtc,
            DecisionSchema.DroppedCount);

        parameters[0].Value = _dialect.ToParameter(Guid.NewGuid());
        parameters[1].Value = _dialect.ToParameter(gap.FirstDroppedUtc);
        parameters[2].Value = _dialect.ToParameter(gap.LastDroppedUtc);
        parameters[3].Value = gap.DroppedCount;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads one bounded page of decisions, newest first.</summary>
    /// <param name="query">The filters and the cap.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The matching records, newest first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public async Task<IReadOnlyList<DecisionRecord>> ReadAsync(
        DecisionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await EnsureSchemaOnceAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, _statements.SelectDecisions(query));

        if (query.CorrelationId is not null) Bind(command, "correlationId", query.CorrelationId);
        if (query.RuleName is not null) Bind(command, "ruleName", query.RuleName);
        if (query.FromUtc is { } from) Bind(command, "fromUtc", _dialect.ToParameter(from));
        if (query.ToUtc is { } to) Bind(command, "toUtc", _dialect.ToParameter(to));
        Bind(command, "limit", query.Limit);

        var records = new List<DecisionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            records.Add(ReadRecord(reader));

        return records;
    }

    /// <summary>Reads the most recent gap markers. Empty is the only healthy value.</summary>
    /// <param name="limit">The most markers to return.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The markers, most recent first.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is less than 1.</exception>
    public async Task<IReadOnlyList<DecisionGap>> ReadGapsAsync(
        int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        await EnsureSchemaOnceAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, _statements.SelectGaps);
        Bind(command, "limit", limit);

        var gaps = new List<DecisionGap>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            gaps.Add(new DecisionGap(
                _dialect.ReadTimestamp(reader, 1),
                _dialect.ReadTimestamp(reader, 2),
                reader.GetInt64(3)));
        }

        return gaps;
    }

    /// <summary>
    /// Deletes everything older than the retention window, in bounded statements, until nothing is
    /// left to take.
    /// </summary>
    /// <remarks>
    /// Runs on its own loop; this is here for a host that wants to force a pass — after a restore, or
    /// from an administrative endpoint.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the pass, leaving what it has already deleted deleted.</param>
    /// <returns>What the pass took, and the cutoff it took it against.</returns>
    public async Task<DecisionPurgeReport> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var now = _options.Clock();
        var cutoff = now - _retention;

        await EnsureSchemaOnceAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var records = await PurgeTableAsync(connection, _statements.PurgeDecisions, cutoff, cancellationToken)
            .ConfigureAwait(false);
        var gaps = await PurgeTableAsync(connection, _statements.PurgeGaps, cutoff, cancellationToken)
            .ConfigureAwait(false);

        Interlocked.Add(ref _purgedCount, records);
        Interlocked.Exchange(ref _lastPurgeTicks, now.ToUniversalTime().Ticks);

        return new DecisionPurgeReport(cutoff, records, gaps);
    }

    /// <summary>Stops the purge loop.</summary>
    /// <remarks>
    /// <strong>And nothing else.</strong> The write path stays open, because a container disposes
    /// singletons in reverse registration order — a sink registered before the <c>DecisionLog</c> that
    /// drains into it is torn down first, and a sink that refused to write after disposal would
    /// silently swallow the drain the log's own disposal exists to perform. A sink still writing after
    /// its purge has stopped is the right failure at shutdown; the reverse is not.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _stopped.CancelAsync().ConfigureAwait(false);

        try
        {
            await _purging.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The loop stopping is what was asked for.
        }
        finally
        {
            _stopped.Dispose();
            _schemaLock.Dispose();
        }
    }

    private async Task PurgeLoopAsync()
    {
        // The first pass waits out one interval rather than running at startup: a host should learn
        // that its decision database is unreachable from its readiness probe, not from a purge
        // failure in its first second.
        using var timer = new PeriodicTimer(_options.PurgeInterval);

        while (await SafeWaitAsync(timer).ConfigureAwait(false))
        {
            try
            {
                await PurgeAsync(_stopped.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The loop never dies. A purge loop that stopped on the first transient failure
                // would silently stop enforcing the window — the failure the window exists to
                // prevent, arriving quietly.
                Interlocked.Increment(ref _failedPurgeCount);
            }
        }
    }

    private async Task<bool> SafeWaitAsync(PeriodicTimer timer)
    {
        try
        {
            return await timer.WaitForNextTickAsync(_stopped.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<long> PurgeTableAsync(
        DbConnection connection, string statement, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var total = 0L;

        while (true)
        {
            await using var command = Command(connection, statement);
            Bind(command, "cutoff", _dialect.ToParameter(cutoff));
            Bind(command, "batch", _options.PurgeBatchSize);

            var deleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (deleted <= 0)
                return total;

            total += deleted;

            // A short pass ended on its own; only a full batch suggests there is more behind it.
            if (deleted < _options.PurgeBatchSize)
                return total;
        }
    }

    private async Task EnsureSchemaOnceAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady || !_options.EnsureSchema)
            return;

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionFactory()
            ?? throw new InvalidOperationException("The connection factory returned null.");

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private DecisionRecord ReadRecord(DbDataReader reader) =>
        new(
            _dialect.ReadGuid(reader, 0),
            reader.GetString(1),
            _dialect.ReadTimestamp(reader, 2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetString(6),
            Deserialize<IReadOnlyList<PropositionVersion>>(reader.GetString(7)) ?? [],
            ReadInput(reader),
            Deserialize<RuleEvaluationResult<object?>>(reader.GetString(11))!);

    private DecisionInput? ReadInput(DbDataReader reader)
    {
        // A null kind is "no capture posture applied", which is not the same as a posture that
        // captured null — hence a column of its own rather than an inference from the payload.
        if (reader.IsDBNull(8))
            return null;

        var kind = (DecisionInputKind)reader.GetInt32(8);
        var value = reader.IsDBNull(9) ? null : Deserialize<object>(reader.GetString(9));

        return kind switch
        {
            DecisionInputKind.Whole => DecisionInput.Whole(value),
            DecisionInputKind.Redacted => DecisionInput.Redacted(value),
            _ => DecisionInput.Reference(ReadReferenceKey(value))
        };
    }

    /// <summary>
    /// A reference capture is a string by construction, so it is handed back as one rather than as
    /// the <c>JsonElement</c> the other two postures unavoidably become.
    /// </summary>
    private static string ReadReferenceKey(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()!,
        string key => key,
        _ => value?.ToString() ?? string.Empty
    };

    private string Serialize<T>(T value) => JsonSerializer.Serialize(value, _options.JsonOptions);

    private T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options.JsonOptions);

    private static DbCommand Command(DbConnection connection, string sql, DbTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    private static DbParameter[] DeclareParameters(DbCommand command, params string[] names)
    {
        var parameters = new DbParameter[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@{names[index]}";
            command.Parameters.Add(parameter);
            parameters[index] = parameter;
        }

        return parameters;
    }

    private static void Bind(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = $"@{name}";
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
