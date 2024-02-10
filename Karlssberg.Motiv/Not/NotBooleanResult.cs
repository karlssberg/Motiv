namespace Karlssberg.Motiv.Not;

/// <summary>Represents the result of a logical NOT operation on a boolean result.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
internal sealed class NotBooleanResult<TMetadata>(BooleanResultBase<TMetadata> operandResult) : BooleanResultBase<TMetadata>
{
    /// <summary>Gets the operand result that is being negated.</summary>
    public BooleanResultBase<TMetadata> OperandResult => operandResult;

    /// <summary>Gets a value indicating whether the negation is satisfied.</summary>
    public override bool Satisfied => !operandResult.Satisfied;

    /// <summary>Gets the description of the negation result.</summary>
    public override string Description => $"NOT:{IsSatisfiedDisplayText}({OperandResult})";

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults
    {
        get { yield return operandResult; }
    }

    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults;

    /// <summary>Gets the reasons associated with the operand result.</summary>
    public override IEnumerable<Reason> ReasonHierarchy => OperandResult.ReasonHierarchy;
}