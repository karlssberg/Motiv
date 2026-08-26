namespace Motiv.Serialization.Sql;

/// <summary>
/// The names of the two tables and their columns, in one place so a statement and a reader cannot
/// drift apart by a typo.
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
    /// payload a query filters on rather than reads, and "show me the declines" should not be a table
    /// scan through serialised justification trees.
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
}
