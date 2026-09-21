namespace Motiv.Serialization;

/// <summary>
/// The decision log read back. Deliberately not on <see cref="IDecisionSink"/>: a sink that forwards
/// to a SIEM has nothing to read, and a query on the sink interface would make it lie. A sink that
/// keeps records implements this too, and a host registers it with <c>AddDecisionSource</c>.
/// </summary>
public interface IDecisionSource
{
    /// <summary>One record by its id, or null when the log holds none — retention may have purged it.</summary>
    Task<DecisionRecord?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>A bounded page of records, newest first.</summary>
    Task<IReadOnlyList<DecisionRecord>> QueryAsync(DecisionQuery query, CancellationToken cancellationToken);
}
