namespace Karlssberg.Motiv;

public interface IHigherOrderLogicalOperatorResult<TMetadata>
{
    /// <summary>Gets the collection of operand results.</summary>
    /// +
    IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>Gets the determinative operand results that have the same satisfaction as the overall result.</summary>
    IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands { get; }

    /// <inheritdoc cref="BooleanResultBase{TMetadata}.Satisfied" />
    bool Satisfied { get; }

    /// <inheritdoc cref="BooleanResultBase{TMetadata}.Description" />
    string Description { get; }

    /// <summary>Gets the unique specific underlying reasons why the condition is satisfied or not.</summary>
    IEnumerable<Reason> Reasons { get; }
}