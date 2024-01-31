namespace Karlssberg.Motiv;

public interface ILogicalOperatorResult<TMetadata>
{
    bool IsSatisfied { get; }

    string Description { get; }

    IEnumerable<string> Reasons { get; }

    IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>Gets the determinative operand results that have the same satisfaction as the overall result.</summary>
    IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands { get; }
}