namespace Karlssberg.Motiv.Not;

/// <summary>Represents the result of a logical NOT operation on a boolean result.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
public sealed class NotBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="NotBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="operandResult">The operand result to negate.</param>
    internal NotBooleanResult(BooleanResultBase<TMetadata> operandResult)
    {
        OperandResult = operandResult.ThrowIfNull(nameof(operandResult));
        UnderlyingResults = [operandResult];
        DeterminativeOperands = [operandResult];
        Value = !operandResult.Value;
    }

    /// <summary>Gets the operand result that is being negated.</summary>
    public BooleanResultBase<TMetadata> OperandResult { get; }

    /// <summary>Gets a value indicating whether the negation is satisfied.</summary>
    public override bool Value { get; }

    /// <summary>Gets the description of the negation result.</summary>
    public override string Description => $"NOT:{IsSatisfiedDisplayText}({OperandResult})";

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands { get; }

    /// <summary>Gets the reasons associated with the operand result.</summary>
    public override IEnumerable<string> GatherReasons() => OperandResult.GatherReasons();
}