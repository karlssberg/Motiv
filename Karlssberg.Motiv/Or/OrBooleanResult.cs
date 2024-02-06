namespace Karlssberg.Motiv.Or;

/// <summary>Represents a boolean result that is the logical OR of two operand results.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class OrBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="OrBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="leftOperandResult">The result of the left operand.</param>
    /// <param name="rightOperandResult">The result of the right operand.</param>
    internal OrBooleanResult(
        BooleanResultBase<TMetadata> leftOperandResult,
        BooleanResultBase<TMetadata> rightOperandResult)
    {
        LeftOperandResult = leftOperandResult;
        RightOperandResult = rightOperandResult;
        Value = leftOperandResult.Value || rightOperandResult.Value;
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
    public override string Description => $"({LeftOperandResult}) OR:{IsSatisfiedDisplayText} ({RightOperandResult})";

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands.SelectMany(r => r.GatherReasons());
}