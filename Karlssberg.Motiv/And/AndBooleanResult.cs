namespace Karlssberg.Motiv.And;

/// <summary>
///     Represents the result of a boolean AND operation between two <see cref="BooleanResultBase{TMetadata}" />
///     objects.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class AndBooleanResult<TMetadata> : BooleanResultBase<TMetadata>, ILogicalOperatorResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="AndBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="leftOperandResult">The result of the left operand.</param>
    /// <param name="rightOperandResult">The result of the right operand.</param>
    internal AndBooleanResult(
        BooleanResultBase<TMetadata> leftOperandResult,
        BooleanResultBase<TMetadata> rightOperandResult)
    {
        LeftOperandResult = leftOperandResult.ThrowIfNull(nameof(leftOperandResult));
        RightOperandResult = rightOperandResult.ThrowIfNull(nameof(rightOperandResult));

        Value = leftOperandResult.Value && rightOperandResult.Value;
    }

    /// <summary>Gets the result of the left operand.</summary>
    public BooleanResultBase<TMetadata> LeftOperandResult { get; }

    /// <summary>Gets the result of the right operand.</summary>
    public BooleanResultBase<TMetadata> RightOperandResult { get; }

    /// <summary>Gets an array containing the left and right operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => [LeftOperandResult, RightOperandResult];

    /// <summary>
    ///     Gets the determinative operand results, which are the operand results that have the same satisfaction status
    ///     as the overall result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults
        .Where(r => r.Value == Value);

    /// <inheritdoc />
    public override bool Value { get; }

    /// <inheritdoc />
    public override string Description => $"({LeftOperandResult}) AND:{IsSatisfiedDisplayText} ({RightOperandResult})";

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands.SelectMany(r => r.GatherReasons());
}