namespace Karlssberg.Motiv.Not;

/// <summary>Represents the result of a logical NOT operation on a boolean result.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
internal sealed class NotBooleanResult<TMetadata>(BooleanResultBase<TMetadata> operandResult) : BooleanResultBase<TMetadata>
{
    /// <summary>Gets the operand result that is being negated.</summary>
    public BooleanResultBase<TMetadata> OperandResult { get; } = operandResult.ThrowIfNull(nameof(operandResult));

    /// <summary>Gets a value indicating whether the negation is satisfied.</summary>
    public override bool Value { get; } = !operandResult.Value;

    /// <summary>Gets the description of the negation result.</summary>
    public override string Description => $"NOT:{IsSatisfiedDisplayText}({OperandResult})";

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } = [operandResult];
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands { get; } = [operandResult];

    /// <summary>Gets the reasons associated with the operand result.</summary>
    public override IEnumerable<string> GatherReasons() => OperandResult.GatherReasons();
}