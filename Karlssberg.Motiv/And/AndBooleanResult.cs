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
    public override bool Value { get; } = leftOperandResult.Value && rightOperandResult.Value;

    /// <summary>Gets the result of the left operand.</summary>
    public BooleanResultBase<TMetadata> LeftOperandResult { get; } = leftOperandResult.ThrowIfNull(nameof(leftOperandResult));

    /// <summary>Gets the result of the right operand.</summary>
    public BooleanResultBase<TMetadata> RightOperandResult { get; } = rightOperandResult.ThrowIfNull(nameof(rightOperandResult));

    /// <summary>Gets an array containing the left and right operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => [LeftOperandResult, RightOperandResult];

    /// <summary>
    ///     Gets the determinative operand results, which are the operand results that have the same satisfaction status
    ///     as the overall result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults
        .Where(r => r.Value == Value);

    /// <inheritdoc />
    public override string Description => $"({LeftOperandResult}) AND:{IsSatisfiedDisplayText} ({RightOperandResult})";

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands.SelectMany(r => r.GatherReasons());
}