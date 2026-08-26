namespace Motiv.Serialization.Sql;

/// <summary>
/// The two tables, their columns, and the order those columns appear in. One place, so a statement, a
/// parameter and a reader cannot drift apart by a typo or a miscounted ordinal.
/// </summary>
/// <remarks>
/// <para>
/// Two tables, mirroring <c>InMemoryDecisionSink</c>'s <c>Records</c> and <c>Gaps</c> and for the same
/// reason: a gap is evidence <em>about</em> the log rather than a decision, and counting one among
/// decisions would corrupt every query the log exists to answer.
/// </para>
/// <para>
/// The envelope is in columns because it is what "why was <em>this</em> customer declined, on the 3rd,
/// at 14:07?" filters on. The outcome, the referenced proposition versions and the captured input are
/// JSON text, following the authoring store's document-as-text reasoning exactly: nothing queries
/// <em>into</em> them, so a native JSON column would fork the schema per provider for a capability
/// never used.
/// </para>
/// </remarks>
internal static class DecisionSchema
{
    internal const string DecisionTable = "MotivDecision";
    internal const string GapTable = "MotivDecisionGap";

    internal const string Id = "Id";
    internal const string CorrelationId = "CorrelationId";
    internal const string TimestampUtc = "TimestampUtc";
    internal const string Caller = "Caller";
    internal const string RuleName = "RuleName";
    internal const string RuleVersion = "RuleVersion";
    internal const string BuildId = "BuildId";
    internal const string PropositionsJson = "PropositionsJson";
    internal const string InputKind = "InputKind";
    internal const string InputJson = "InputJson";

    /// <summary>
    /// Lifted out of the outcome JSON and given its own column: it is the one field inside the
    /// payload a query filters on rather than reads, and "show me the declines" should be a predicate
    /// the database can apply rather than a scan through serialised justification trees. Narrowed by
    /// the timestamp index rather than one of its own — a two-valued column is poor index material,
    /// and every question that asks it also names a window.
    /// </summary>
    internal const string Satisfied = "Satisfied";

    internal const string OutcomeJson = "OutcomeJson";

    internal const string FirstDroppedUtc = "FirstDroppedUtc";
    internal const string LastDroppedUtc = "LastDroppedUtc";
    internal const string DroppedCount = "DroppedCount";

    /// <summary>The purge's own predicate, and every time-range question.</summary>
    internal const string TimestampIndex = "IX_MotivDecision_TimestampUtc";

    /// <summary>The pivot from one decision to every rule that took part in it.</summary>
    internal const string CorrelationIndex = "IX_MotivDecision_CorrelationId";

    /// <summary>The purge's predicate on the gap table.</summary>
    internal const string GapTimestampIndex = "IX_MotivDecisionGap_LastDroppedUtc";

    /// <summary>
    /// Every column of <see cref="DecisionTable"/>. The insert, the select and the parameter set are
    /// all generated from this, so adding a column is one edit rather than four synchronised ones —
    /// and the read side resolves ordinals by name, so nothing here is coupled to a literal index.
    /// </summary>
    internal static readonly string[] DecisionColumns =
    [
        Id, CorrelationId, TimestampUtc, Caller, RuleName, RuleVersion, BuildId, PropositionsJson,
        InputKind, InputJson, Satisfied, OutcomeJson
    ];

    /// <summary>Every column of <see cref="GapTable"/>.</summary>
    internal static readonly string[] GapColumns = [Id, FirstDroppedUtc, LastDroppedUtc, DroppedCount];
}
