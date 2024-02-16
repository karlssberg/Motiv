namespace Karlssberg.Motiv;

public interface ILogicalOperatorResult<TMetadata>
{
    bool Satisfied { get; }

    string Description { get; }

    IEnumerable<Reason> ReasonHierarchy { get; }

    IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>Gets the determinative operand results that have the same satisfaction as the overall result.</summary>
    IEnumerable<BooleanResultBase<TMetadata>> Causes { get; }
}