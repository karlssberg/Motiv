namespace Karlssberg.Motiv.XOr;

/// <summary>Represents the result of a logical XOR (exclusive OR) operation.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
public sealed class XOrBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="XOrBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="leftOperandResult">The result of the left operand.</param>
    /// <param name="rightOperandResult">The result of the right operand.</param>
    internal XOrBooleanResult(
        BooleanResultBase<TMetadata> leftOperandResult,
        BooleanResultBase<TMetadata> rightOperandResult)
    {
        Satisfied = leftOperandResult.Satisfied ^ rightOperandResult.Satisfied;
        LeftOperandResult = leftOperandResult ?? throw new ArgumentNullException(nameof(leftOperandResult));
        RightOperandResult = rightOperandResult ?? throw new ArgumentNullException(nameof(rightOperandResult));
    }

    /// <summary>Gets a value indicating whether the XOR operation is satisfied.</summary>
    public override bool Satisfied { get; }

    /// <summary>Gets the result of the left operand.</summary>
    public BooleanResultBase<TMetadata> LeftOperandResult { get; }

    /// <summary>Gets the result of the right operand.</summary>
    public BooleanResultBase<TMetadata> RightOperandResult { get; }

    /// <summary>Gets an array containing the left and right operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => [LeftOperandResult, RightOperandResult];

    /// <summary>Gets the determinative operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults;

    /// <summary>Gets the description of the XOR operation.</summary>
    public override string Description => $"({LeftOperandResult}) XOR:{IsSatisfiedDisplayText} ({RightOperandResult})";

    /// <summary>Gets the reasons for the XOR operation result.</summary>
    public override IEnumerable<Reason> GatherReasons() => DeterminativeOperands.SelectMany(r => r.GatherReasons());
}