namespace Motiv.Serialization.Sql;

/// <summary>What one pass of the retention purge took, and the cutoff it took it against.</summary>
/// <remarks>
/// Returned rather than logged so a host that wants to surface the purge has something to surface.
/// The purge has no <c>motiv.rules.*</c> instrument of its own — that contract lives in one assembly
/// and this one is downstream of it — so these numbers, and the readings on
/// <see cref="SqlDecisionSink"/>, are how an operator learns the window is being enforced.
/// </remarks>
/// <param name="CutoffUtc">Everything older than this was taken.</param>
/// <param name="RecordsPurged">Decision records deleted.</param>
/// <param name="GapsPurged">
/// Gap markers deleted. A marker for a hole among records that have themselves aged out would leave
/// the log claiming a gap in a period it no longer covers.
/// </param>
public sealed record DecisionPurgeReport(
    DateTimeOffset CutoffUtc,
    long RecordsPurged,
    long GapsPurged);
