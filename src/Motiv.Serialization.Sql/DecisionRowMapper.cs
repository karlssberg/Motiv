using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace Motiv.Serialization.Sql;

/// <summary>
/// The one place a <see cref="DecisionRecord"/> becomes a row and a row becomes a record again.
/// </summary>
/// <remarks>
/// <para>
/// Split out of the sink because it is the part that has to agree with itself: a field written to one
/// column and read from another compiles, writes, and only shows up as a corrupted audit record. Both
/// directions go through <see cref="DecisionSchema"/> by name — parameters are looked up by column
/// name, and ordinals come from <see cref="DbDataReader.GetOrdinal"/> — so neither side is coupled to
/// a literal index or to the order columns happen to appear in.
/// </para>
/// <para>
/// What survives the round trip is faithful with one exception, and it is a deliberate one:
/// <c>DecisionInput.Value</c> and the outcome's <c>Values</c> are <see cref="object"/>, so a
/// <see cref="DecisionInputKind.Whole"/> or <see cref="DecisionInputKind.Redacted"/> capture comes
/// back as a <see cref="JsonElement"/> rather than the adopter's own type. The alternative is a type
/// discriminator in the log, which would pin the adopter's assembly identity into their compliance
/// record.
/// </para>
/// </remarks>
/// <param name="dialect">Converts the values this engine does not bind natively.</param>
/// <param name="json">How the payloads are serialised.</param>
internal sealed class DecisionRowMapper(DecisionSqlDialect dialect, JsonSerializerOptions? json)
{
    /// <summary>Fills a decision's parameters, by column name.</summary>
    /// <param name="parameters">The declared parameter set.</param>
    /// <param name="record">The record to write.</param>
    public void Write(IReadOnlyDictionary<string, DbParameter> parameters, DecisionRecord record)
    {
        parameters[DecisionSchema.Id].Value = dialect.ToParameter(record.Id);
        parameters[DecisionSchema.CorrelationId].Value = record.CorrelationId;
        parameters[DecisionSchema.TimestampUtc].Value = dialect.ToParameter(record.TimestampUtc);
        parameters[DecisionSchema.Caller].Value = (object?)record.Caller ?? DBNull.Value;
        parameters[DecisionSchema.RuleName].Value = record.RuleName;
        parameters[DecisionSchema.RuleVersion].Value = record.RuleVersion;
        parameters[DecisionSchema.BuildId].Value = record.BuildId;
        parameters[DecisionSchema.PropositionsJson].Value = Serialize(record.ReferencedPropositionVersions);
        parameters[DecisionSchema.InputKind].Value =
            record.Input is null ? DBNull.Value : (int)record.Input.Kind;
        parameters[DecisionSchema.InputJson].Value =
            record.Input is null ? DBNull.Value : Serialize(record.Input.Value);
        parameters[DecisionSchema.Satisfied].Value = dialect.ToParameter(record.Outcome.Satisfied);
        parameters[DecisionSchema.OutcomeJson].Value = Serialize(record.Outcome);
    }

    /// <summary>Fills a gap marker's parameters, by column name.</summary>
    /// <param name="parameters">The declared parameter set.</param>
    /// <param name="gap">The marker to write.</param>
    public void WriteGap(IReadOnlyDictionary<string, DbParameter> parameters, DecisionGap gap)
    {
        parameters[DecisionSchema.Id].Value = dialect.ToParameter(Guid.NewGuid());
        parameters[DecisionSchema.FirstDroppedUtc].Value = dialect.ToParameter(gap.FirstDroppedUtc);
        parameters[DecisionSchema.LastDroppedUtc].Value = dialect.ToParameter(gap.LastDroppedUtc);
        parameters[DecisionSchema.DroppedCount].Value = gap.DroppedCount;
    }

    /// <summary>Reads the row the reader is positioned on.</summary>
    /// <param name="reader">The open reader.</param>
    /// <returns>The record.</returns>
    public DecisionRecord Read(DbDataReader reader) =>
        new(
            dialect.ReadGuid(reader, Ordinal(reader, DecisionSchema.Id)),
            reader.GetString(Ordinal(reader, DecisionSchema.CorrelationId)),
            dialect.ReadTimestamp(reader, Ordinal(reader, DecisionSchema.TimestampUtc)),
            String(reader, DecisionSchema.Caller),
            reader.GetString(Ordinal(reader, DecisionSchema.RuleName)),
            reader.GetInt32(Ordinal(reader, DecisionSchema.RuleVersion)),
            reader.GetString(Ordinal(reader, DecisionSchema.BuildId)),
            Deserialize<IReadOnlyList<PropositionVersion>>(
                reader.GetString(Ordinal(reader, DecisionSchema.PropositionsJson))) ?? [],
            ReadInput(reader),
            Deserialize<RuleEvaluationResult<object?>>(
                reader.GetString(Ordinal(reader, DecisionSchema.OutcomeJson)))!);

    /// <summary>Reads the gap marker the reader is positioned on.</summary>
    /// <param name="reader">The open reader.</param>
    /// <returns>The marker.</returns>
    public DecisionGap ReadGap(DbDataReader reader) =>
        new(
            dialect.ReadTimestamp(reader, Ordinal(reader, DecisionSchema.FirstDroppedUtc)),
            dialect.ReadTimestamp(reader, Ordinal(reader, DecisionSchema.LastDroppedUtc)),
            reader.GetInt64(Ordinal(reader, DecisionSchema.DroppedCount)));

    private DecisionInput? ReadInput(DbDataReader reader)
    {
        // A null kind is "no capture posture applied", which is not the same as a posture that
        // captured null — hence a column of its own rather than an inference from the payload.
        var kindOrdinal = Ordinal(reader, DecisionSchema.InputKind);
        if (reader.IsDBNull(kindOrdinal))
            return null;

        var kind = (DecisionInputKind)reader.GetInt32(kindOrdinal);
        var value = String(reader, DecisionSchema.InputJson) is { } payload
            ? Deserialize<object>(payload)
            : null;

        return kind switch
        {
            DecisionInputKind.Whole => DecisionInput.Whole(value),
            DecisionInputKind.Redacted => DecisionInput.Redacted(value),
            _ => DecisionInput.Reference(ReferenceKey(value))
        };
    }

    /// <summary>
    /// A reference capture is a string by construction, so it is handed back as one rather than as
    /// the <see cref="JsonElement"/> the other two postures unavoidably become.
    /// </summary>
    private static string ReferenceKey(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()!,
        string key => key,
        _ => value?.ToString() ?? string.Empty
    };

    private static int Ordinal(DbDataReader reader, string column) => reader.GetOrdinal(column);

    private static string? String(DbDataReader reader, string column)
    {
        var ordinal = Ordinal(reader, column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private string Serialize<T>(T value) => JsonSerializer.Serialize(value, json);

    private T? Deserialize<T>(string payload) => JsonSerializer.Deserialize<T>(payload, json);
}
