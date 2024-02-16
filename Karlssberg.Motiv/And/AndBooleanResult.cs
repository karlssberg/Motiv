namespace Karlssberg.Motiv.And;

/// <summary>
///     Represents the result of a boolean AND operation between two <see cref="BooleanResultBase{TMetadata}" />
///     objects.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class AndBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> leftOperandResult,
    BooleanResultBase<TMetadata> rightOperandResult)
    : BooleanResultBase<TMetadata>, ILogicalOperatorResult<TMetadata>
{
    /// <inheritdoc />
    public override bool Satisfied { get; } = leftOperandResult.Satisfied && rightOperandResult.Satisfied;

    /// <summary>Gets the result of the left operand.</summary>
    public BooleanResultBase<TMetadata> LeftOperandResult { get; } = leftOperandResult;

    /// <summary>Gets the result of the right operand.</summary>
    public BooleanResultBase<TMetadata> RightOperandResult { get; } = rightOperandResult;

    /// <summary>Gets an array containing the left and right operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => [LeftOperandResult, RightOperandResult];

    /// <summary>
    ///     Gets the determinative operand results, which are the operand results that have the same satisfaction status
    ///     as the overall result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> Causes => UnderlyingResults
        .Where(r => r.Satisfied == Satisfied);

    /// <inheritdoc />
    public override string Description => $"({LeftOperandResult}) AND:{IsSatisfiedDisplayText} ({RightOperandResult})";

    /// <inheritdoc />
    public override IEnumerable<Reason> ReasonHierarchy => Causes
        .SelectMany(r => r.ReasonHierarchy);
}